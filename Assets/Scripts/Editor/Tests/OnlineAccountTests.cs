using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Online accounts and the server's disk (online phase 2, Docs/KeHoach-Online.md): the login
    /// proof, names, the account and character files, backups, game masters, and what a player's
    /// machine remembers.
    /// </summary>
    public class OnlineAccountTests
    {
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "rtt_test_accounts_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            LoginInfo.ResetForTests();
            LoginInfo.FolderOverride = Path.Combine(folder, "client");
        }

        [TearDown]
        public void TearDown()
        {
            LoginInfo.ResetForTests();
            LoginInfo.FolderOverride = null;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void AProofShowsThePasswordWithoutSendingIt()
        {
            var salt = LoginCrypto.RandomBytes(LoginCrypto.SaltBytes);
            var nonce = LoginCrypto.RandomBytes(LoginCrypto.NonceBytes);
            var key = LoginCrypto.DeriveKey("rồng lửa", salt, 1000);
            Assert.AreEqual(LoginCrypto.KeyBytes, key.Length);
            var proof = LoginCrypto.Proof(key, nonce);
            Assert.IsTrue(LoginCrypto.Verify(key, nonce, proof), "the right password");
            Assert.IsFalse(LoginCrypto.Verify(LoginCrypto.DeriveKey("rong lua", salt, 1000), nonce, proof), "another password");
            Assert.IsFalse(LoginCrypto.Verify(key, LoginCrypto.RandomBytes(LoginCrypto.NonceBytes), proof), "an old proof for another nonce");
            CollectionAssert.AreEqual(key, LoginCrypto.DeriveKey("rồng lửa", salt, 1000), "the same password and salt make the same key");
            CollectionAssert.AreNotEqual(key, LoginCrypto.DeriveKey("rồng lửa", LoginCrypto.RandomBytes(16), 1000), "another salt, another key");
        }

        [Test]
        public void NamesAreCheckedAndComparedWithoutCase()
        {
            Assert.AreEqual("Bé Mai", LoginCrypto.NormalizeName("  Bé   Mai "));
            Assert.AreEqual(LoginCrypto.NameKey("Bé Mai"), LoginCrypto.NameKey("bé mai"), "case does not make another account");
            Assert.AreNotEqual(LoginCrypto.NameKey("Bé Mai"), LoginCrypto.NameKey("Be Mai"), "accents do");
            Assert.IsNull(LoginCrypto.CheckName("Khang_99"));
            Assert.IsNull(LoginCrypto.CheckName("Trưởng Làng"));
            Assert.IsNotNull(LoginCrypto.CheckName("ab"), "too short");
            Assert.IsNotNull(LoginCrypto.CheckName("Tên Này Dài Quá Mức Cho Phép"), "too long");
            Assert.IsNotNull(LoginCrypto.CheckName("<b>GM</b>"), "no markup");
            Assert.IsNotNull(LoginCrypto.CheckPassword("123"));
            Assert.IsNull(LoginCrypto.CheckPassword("1234"));
        }

        [Test]
        public void TheServerKeepsAccountsAndCharacters()
        {
            var store = new ServerStore(folder);
            var salt = LoginCrypto.RandomBytes(16);
            var key = LoginCrypto.DeriveKey("matkhau", salt, 1000);
            var a = store.CreateAccount("Khang", salt, key, 1000);
            Assert.NotNull(a);
            Assert.IsNull(store.CreateAccount("KHANG", salt, key, 1000), "the name is taken whatever the case");
            var back = store.LoadAccount("khang");
            Assert.AreEqual("Khang", back.name);
            CollectionAssert.AreEqual(key, back.Key, "only the key is kept");
            StringAssert.DoesNotContain("matkhau", File.ReadAllText(Directory.GetFiles(store.AccountsDir)[0]), "never the password");

            Assert.IsNull(store.LoadCharacter("Khang"), "a new character has no save yet");
            var f = new SaveFile { level = 4 };
            f.Set("player", "{\"level\":4}");
            store.SaveCharacter("Khang", f);
            var loaded = store.LoadCharacter("KHANG");
            Assert.NotNull(loaded);
            Assert.AreEqual(4, loaded.level);
            Assert.AreEqual("Khang", loaded.character);
            Assert.AreEqual("{\"level\":4}", loaded.Get("player"));

            // a damaged file falls back to the previous version
            f.level = 5;
            store.SaveCharacter("Khang", f);
            string path = Directory.GetFiles(store.CharactersDir, "*.json")[0];
            File.WriteAllText(path, "{ broken");
            Assert.AreEqual(4, store.LoadCharacter("Khang").level, "read from the .bak");
        }

        [Test]
        public void BackupsAreDailyAndTheOldestGo()
        {
            var store = new ServerStore(folder);
            store.CreateAccount("Khang", LoginCrypto.RandomBytes(16), LoginCrypto.RandomBytes(32), 1000);
            for (int i = 0; i < ServerStore.BackupsKept + 3; i++)
                Directory.CreateDirectory(Path.Combine(store.BackupsDir, $"2020-01-{i + 1:00}"));
            string today = store.Backup();
            Assert.IsTrue(File.Exists(Path.Combine(today, "accounts", Path.GetFileName(Directory.GetFiles(store.AccountsDir)[0]))), "today's copy has the accounts");
            Assert.AreEqual(ServerStore.BackupsKept, Directory.GetDirectories(store.BackupsDir).Length, "only the newest are kept");
            Assert.AreEqual(today, store.Backup(), "once a day");
        }

        [Test]
        public void GameMastersAreListedInAFile()
        {
            var store = new ServerStore(folder);
            Assert.IsFalse(store.IsGm("Khang"));
            File.AppendAllText(store.GmFile, "khang\n# a comment\n");
            Assert.IsTrue(store.IsGm("Khang"), "names in gm.txt, whatever the case");
            Assert.IsFalse(store.IsGm("Mai"));
        }

        [Test]
        public void AMachineRemembersTheKeyNotThePassword()
        {
            var salt = LoginCrypto.RandomBytes(16);
            var key = LoginCrypto.DeriveKey("matkhau", salt, 1000);
            LoginInfo.Name = "Khang";
            LoginInfo.Password = "matkhau";
            Assert.IsTrue(LoginInfo.Ready);
            LoginInfo.Accepted("Khang", salt, key);
            Assert.AreEqual("", LoginInfo.Password, "the typed password is forgotten");
            Assert.AreEqual("Khang", LoginInfo.RememberedName);
            StringAssert.DoesNotContain("matkhau", File.ReadAllText(Path.Combine(LoginInfo.FolderOverride, "login.json")));

            LoginInfo.ResetForTests();
            Assert.IsTrue(LoginInfo.UseRemembered(), "next time: no password needed");
            CollectionAssert.AreEqual(key, LoginInfo.KeyFor(salt, 1000));
            Assert.IsNull(LoginInfo.KeyFor(LoginCrypto.RandomBytes(16), 1000), "a remade account (another salt) asks for the password");

            LoginInfo.Forget();
            Assert.IsNull(LoginInfo.RememberedName);
            Assert.IsFalse(LoginInfo.UseRemembered());
        }
    }

    /// <summary>Finding the world without typing an address.</summary>
    public class OnlineDiscoveryTests
    {
        [Test]
        public void AnAnswerIsReadBack()
        {
            string text = ServerDiscovery.ReplyText(NetProtocol.Version, 7770, 3, 20, "Rừng | Thì Thầm", "4242");
            var f = ServerDiscovery.Parse(text, "4242", "26.253.10.125");
            Assert.NotNull(f);
            Assert.AreEqual("26.253.10.125", f.address);
            Assert.AreEqual(7770, f.port);
            Assert.AreEqual(3, f.players);
            Assert.AreEqual(20, f.max);
            Assert.AreEqual("Rừng | Thì Thầm", f.name, "the name may hold the separator");
            Assert.IsTrue(f.Compatible);
            Assert.IsFalse(f.Full);
            Assert.IsNull(ServerDiscovery.Parse(text, "1111", "x"), "an answer to someone else's question");
            Assert.IsNull(ServerDiscovery.Parse("hello", "4242", "x"));
            Assert.IsFalse(ServerDiscovery.Parse(ServerDiscovery.ReplyText(NetProtocol.Version + 1, 7770, 0, 20, "a", "1"), "1", "x").Compatible);
            Assert.IsTrue(ServerDiscovery.Parse(ServerDiscovery.ReplyText(NetProtocol.Version, 7770, 20, 20, "a", "1"), "1", "x").Full);
        }

        [Test]
        public void AddressesMayCarryAPort()
        {
            Assert.IsTrue(ServerDiscovery.TryResolve("26.253.10.125", 7770, out var ip, out ushort port));
            Assert.AreEqual("26.253.10.125", ip.ToString());
            Assert.AreEqual(7770, port);
            Assert.IsTrue(ServerDiscovery.TryResolve(" 192.168.1.9:7780 ", 7770, out ip, out port));
            Assert.AreEqual("192.168.1.9", ip.ToString());
            Assert.AreEqual(7780, port);
            Assert.IsFalse(ServerDiscovery.TryResolve("", 7770, out _, out _));
        }

        [Test]
        public void TheDiscoveryPortIsNextToTheGamePort() => Assert.AreEqual(7771, ServerDiscovery.PortFor(7770));

        [Test]
        public void ChatLinesAreOneLineWithoutMarkup()
        {
            Assert.AreEqual("‹b›hi‹/b› there", ServerPlayers.CleanChat(" <b>hi</b>\nthere "));
            Assert.AreEqual(120, ServerPlayers.CleanChat(new string('a', 300)).Length);
        }
    }
}
