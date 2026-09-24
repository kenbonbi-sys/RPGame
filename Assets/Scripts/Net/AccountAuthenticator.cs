using System;
using System.Collections.Generic;
using FishNet.Authenticating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Logs players in before they may do anything else (online phase 2, Docs/KeHoach-Online.md):
    ///   client: LoginHello (name, login or new character)
    ///   server: LoginChallenge (the account's salt, a one-time nonce)
    ///   client: LoginProof (HMAC of the nonce with the key made from the password; a new character also sends the key)
    ///   server: LoginResult, then FishNet lets the connection in or drops it.
    /// The password never leaves the player's machine. The host's own player logs in the same way.
    /// </summary>
    public class AccountAuthenticator : Authenticator
    {
        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        /// <summary>Server: where accounts live.</summary>
        public ServerStore Store;
        /// <summary>Server: a reason to turn a name away before its password is checked (already playing, server full), or null.</summary>
        public Func<string, string> Refuse;
        /// <summary>Server: a player is in (their account; true when it was just made).</summary>
        public event Action<NetworkConnection, AccountRecord, bool> Admitted;
        /// <summary>Client: the server's answer arrived.</summary>
        public static event Action<LoginResult> ResultReceived;

        class Pending
        {
            public string name;
            public LoginMode mode;
            public bool create;
            public byte[] salt;
            public byte[] nonce;
            public AccountRecord account;
        }

        readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
        byte[] clientSalt, clientKey;

        public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);
            networkManager.ServerManager.RegisterBroadcast<LoginHello>(OnHello, false);
            networkManager.ServerManager.RegisterBroadcast<LoginProof>(OnProof, false);
            networkManager.ServerManager.OnRemoteConnectionState += OnRemoteState;
            networkManager.ClientManager.RegisterBroadcast<LoginChallenge>(OnChallenge);
            networkManager.ClientManager.RegisterBroadcast<LoginResult>(OnResult);
            networkManager.ClientManager.OnClientConnectionState += OnClientState;
        }

        void OnDestroy()
        {
            var nm = NetworkManager;
            if (nm == null) return;
            if (nm.ServerManager != null)
            {
                nm.ServerManager.UnregisterBroadcast<LoginHello>(OnHello);
                nm.ServerManager.UnregisterBroadcast<LoginProof>(OnProof);
                nm.ServerManager.OnRemoteConnectionState -= OnRemoteState;
            }
            if (nm.ClientManager != null)
            {
                nm.ClientManager.UnregisterBroadcast<LoginChallenge>(OnChallenge);
                nm.ClientManager.UnregisterBroadcast<LoginResult>(OnResult);
                nm.ClientManager.OnClientConnectionState -= OnClientState;
            }
        }

        // ================================================================== client
        void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            clientSalt = clientKey = null;
            NetworkManager.ClientManager.Broadcast(new LoginHello
            {
                name = LoginCrypto.NormalizeName(LoginInfo.Name),
                mode = LoginInfo.Mode,
                version = NetProtocol.Version
            });
        }

        void OnChallenge(LoginChallenge c, Channel channel)
        {
            byte[] key = null;
            try { key = LoginInfo.KeyFor(c.salt, c.iterations); }
            catch (Exception e) { Debug.LogException(e); }
            if (key == null)
            {
                // the remembered key belongs to another salt (the account was made again): ask for the password
                LoginInfo.LastResult = new LoginResult { code = LoginCode.WrongPassword, message = "Hãy nhập mật khẩu." };
                NetworkManager.ClientManager.StopConnection();
                return;
            }
            clientSalt = c.salt;
            clientKey = key;
            bool sendsKey = LoginInfo.Mode != LoginMode.Login;
            NetworkManager.ClientManager.Broadcast(new LoginProof
            {
                proof = LoginCrypto.Proof(key, c.nonce),
                key = sendsKey ? key : null
            });
        }

        void OnResult(LoginResult r, Channel channel)
        {
            LoginInfo.LastResult = r;
            if (r.code == LoginCode.Ok) LoginInfo.Accepted(r.name, clientSalt, clientKey);
            ResultReceived?.Invoke(r);
        }

        // ================================================================== server
        void OnRemoteState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Started) pending.Remove(conn.ClientId);
        }

        void OnHello(NetworkConnection conn, LoginHello hello, Channel channel)
        {
            if (conn.IsAuthenticated || Store == null)
            {
                conn.Disconnect(true);
                return;
            }
            if (hello.version != NetProtocol.Version)
            {
                Fail(conn, LoginCode.Refused, hello.version < NetProtocol.Version
                    ? "Game của bạn cũ hơn máy chủ. Hãy tải bản mới."
                    : "Máy chủ đang chạy bản cũ hơn game của bạn.");
                return;
            }
            string name = LoginCrypto.NormalizeName(hello.name);
            string bad = LoginCrypto.CheckName(name);
            if (bad != null)
            {
                Fail(conn, LoginCode.Refused, bad);
                return;
            }
            string refusal = Refuse != null ? Refuse(name) : null;
            if (refusal != null)
            {
                Fail(conn, LoginCode.Refused, refusal);
                return;
            }
            var p = new Pending { name = name, mode = hello.mode, nonce = LoginCrypto.RandomBytes(LoginCrypto.NonceBytes) };
            var account = Store.LoadAccount(name);
            if (account != null)
            {
                if (hello.mode == LoginMode.Register)
                {
                    Fail(conn, LoginCode.NameTaken, $"Tên \"{name}\" đã có người dùng. Hãy chọn tên khác.");
                    return;
                }
                if (account.banned)
                {
                    Fail(conn, LoginCode.Refused, "Tài khoản này đã bị khóa.");
                    return;
                }
                p.account = account;
                p.salt = account.Salt;
            }
            else
            {
                if (hello.mode == LoginMode.Login)
                {
                    Fail(conn, LoginCode.NoSuchCharacter, $"Chưa có nhân vật tên \"{name}\".");
                    return;
                }
                p.create = true;
                p.salt = LoginCrypto.RandomBytes(LoginCrypto.SaltBytes);
            }
            pending[conn.ClientId] = p;
            NetworkManager.ServerManager.Broadcast(conn, new LoginChallenge
            {
                salt = p.salt,
                nonce = p.nonce,
                iterations = p.account != null && p.account.iterations > 0 ? p.account.iterations : LoginCrypto.Iterations
            }, false);
        }

        void OnProof(NetworkConnection conn, LoginProof proof, Channel channel)
        {
            if (conn.IsAuthenticated || !pending.TryGetValue(conn.ClientId, out var p))
            {
                conn.Disconnect(true);
                return;
            }
            pending.Remove(conn.ClientId);
            if (p.create)
            {
                if (proof.key == null || proof.key.Length != LoginCrypto.KeyBytes || !LoginCrypto.Verify(proof.key, p.nonce, proof.proof))
                {
                    Fail(conn, LoginCode.Refused, "Không tạo được nhân vật.");
                    return;
                }
                var made = Store.CreateAccount(p.name, p.salt, proof.key, LoginCrypto.Iterations);
                if (made == null)
                {
                    Fail(conn, LoginCode.NameTaken, $"Tên \"{p.name}\" vừa có người dùng. Hãy chọn tên khác.");
                    return;
                }
                Debug.Log($"[Server] new character \"{made.name}\"");
                Succeed(conn, made, true);
                return;
            }
            if (!LoginCrypto.Verify(p.account.Key, p.nonce, proof.proof))
            {
                Debug.Log($"[Server] wrong password for \"{p.name}\" from player {conn.ClientId}");
                Fail(conn, LoginCode.WrongPassword, "Sai mật khẩu.");
                return;
            }
            // checked again: the same name may have logged in from elsewhere meanwhile
            string refusal = Refuse != null ? Refuse(p.name) : null;
            if (refusal != null)
            {
                Fail(conn, LoginCode.Refused, refusal);
                return;
            }
            p.account.lastLogin = DateTime.Now.ToString("o");
            Store.SaveAccount(p.account);
            Succeed(conn, p.account, false);
        }

        void Succeed(NetworkConnection conn, AccountRecord account, bool created)
        {
            bool gm = Store.IsGm(account.name);
            NetworkManager.ServerManager.Broadcast(conn, new LoginResult { code = LoginCode.Ok, name = account.name, created = created, gm = gm }, false);
            // FishNet lets the connection in; the session learns who it is before the hero is made
            Admitted?.Invoke(conn, account, created);
            OnAuthenticationResult?.Invoke(conn, true);
        }

        void Fail(NetworkConnection conn, LoginCode code, string message)
        {
            NetworkManager.ServerManager.Broadcast(conn, new LoginResult { code = code, message = message }, false);
            // after the answer, so it leaves before the connection is dropped
            OnAuthenticationResult?.Invoke(conn, false);
        }
    }
}
