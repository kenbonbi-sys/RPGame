using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Finding the world without typing an address (Docs/KeHoach-Online.md). A server answers on
    /// UDP port game port + 1 (7771); a player's game asks every network it is on (LAN, Radmin VPN,
    /// Hamachi… a broadcast reaches the whole network) and the addresses written down in
    /// <see cref="ServerList"/>, and joins the best answer. The answer carries the server's name,
    /// its channel, its players and its version, so an outdated game can say so. Every channel of
    /// a world answers on its own port (<see cref="ChannelPort"/>), and a search asks them all.
    /// </summary>
    public sealed class ServerDiscovery : IDisposable
    {
        const string AskWord = "RTT?";
        const string ReplyWord = "RTT!";

        /// <summary>Channels a search asks for on every address (online phase 4).</summary>
        public const int MaxChannels = 4;

        public static ushort PortFor(ushort gamePort) => (ushort)(gamePort + 1);

        /// <summary>The game port of <paramref name="channel"/> (1, 2…) of a world whose channel 1 is on <paramref name="basePort"/>: every channel takes two ports (game, discovery).</summary>
        public static ushort ChannelPort(ushort basePort, int channel) => (ushort)(basePort + 2 * (Math.Max(1, channel) - 1));

        /// <summary>Players in the world right now, for the answer (the server keeps it up to date).</summary>
        public static volatile int PlayerCount;

        UdpClient udp;
        Thread thread;
        volatile bool running;
        string serverName;
        int maxPlayers;
        int channel;
        ushort gamePort;

        // ================================================================== server
        /// <summary>Starts answering; null when the port is taken (the game still runs, it just cannot be found).</summary>
        public static ServerDiscovery StartResponder(ushort gamePort, int channel, Func<string> name, Func<int> players, int max)
        {
            try
            {
                var d = new ServerDiscovery { serverName = name(), maxPlayers = max, gamePort = gamePort, channel = channel };
                PlayerCount = players();
                d.udp = new UdpClient(AddressFamily.InterNetwork);
                d.udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                d.udp.Client.Bind(new IPEndPoint(IPAddress.Any, PortFor(gamePort)));
                d.running = true;
                d.thread = new Thread(d.Answer) { IsBackground = true, Name = "RTT discovery" };
                d.thread.Start();
                Debug.Log($"[Online] answering players looking for servers on UDP {PortFor(gamePort)}");
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Online] cannot answer on UDP {PortFor(gamePort)} ({e.Message}); players must type this server's address");
                return null;
            }
        }

        void Answer()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = udp.Receive(ref from);
                    string msg = Encoding.UTF8.GetString(data);
                    if (!msg.StartsWith(AskWord)) continue;
                    string token = msg.Length > AskWord.Length + 1 ? msg.Substring(AskWord.Length + 1) : "";
                    byte[] reply = Encoding.UTF8.GetBytes(ReplyText(NetProtocol.Version, gamePort, PlayerCount, maxPlayers, channel, serverName, token));
                    udp.Send(reply, reply.Length, from);
                }
                catch (SocketException)
                {
                    if (!running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    if (!running) break;
                    Debug.LogWarning("[Online] discovery: " + e.Message);
                }
            }
        }

        /// <summary>The server's answer: RTT!|version|port|players|max|token|channel|name (a game older than protocol 3 reads "channel|name" as the name, and still sees the version).</summary>
        public static string ReplyText(int version, ushort port, int players, int max, int channel, string name, string token) =>
            $"{ReplyWord}|{version}|{port}|{players}|{max}|{token}|{channel}|{name}";

        public void Dispose()
        {
            running = false;
            try { udp?.Close(); }
            catch { /* already closed */ }
            udp = null;
        }

        // ================================================================== player
        /// <summary>A server that answered.</summary>
        public class Found
        {
            public string address;
            public ushort port;
            public string name;
            public int players, max, version;
            /// <summary>1, 2… (0: a server too old to say).</summary>
            public int channel;
            /// <summary>Seconds from asking to the answer.</summary>
            public float ping;

            public bool Compatible => version == NetProtocol.Version;
            public bool Full => max > 0 && players >= max;
        }

        /// <summary>Reads an answer; null when it is not one (or not for <paramref name="token"/>).</summary>
        public static Found Parse(string text, string token, string address)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith(ReplyWord + "|")) return null;
            var parts = text.Split(new[] { '|' }, 8);
            if (parts.Length < 7 || parts[5] != token) return null;
            if (!int.TryParse(parts[1], out int version) || !ushort.TryParse(parts[2], out ushort port)) return null;
            int.TryParse(parts[3], out int players);
            int.TryParse(parts[4], out int max);
            var f = new Found { address = address, port = port, players = players, max = max, version = version, name = parts[6] };
            // protocol 3 on: the channel before the name
            if (parts.Length == 8 && int.TryParse(parts[6], out int channel))
            {
                f.channel = channel;
                f.name = parts[7];
            }
            return f;
        }

        /// <summary>
        /// Asks every network this machine is on (unless <paramref name="broadcast"/> is false) and
        /// every address in <paramref name="addresses"/> ("host" or "host:port"), on each channel's
        /// port, for <paramref name="seconds"/>; <paramref name="results"/> gets one entry per
        /// server, fastest first.
        /// </summary>
        public static IEnumerator Search(IList<string> addresses, ushort gamePort, float seconds, List<Found> results, bool broadcast = true)
        {
            results.Clear();
            UdpClient udp;
            try
            {
                udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                udp.Client.Blocking = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] cannot look for servers: " + e.Message);
                yield break;
            }
            string token = UnityEngine.Random.Range(100000, 999999).ToString();
            byte[] ask = Encoding.UTF8.GetBytes(AskWord + "|" + token);
            float sentAt = Time.realtimeSinceStartup;
            var asked = new HashSet<string>();
            void Send(IPAddress ip, ushort basePort)
            {
                for (int c = 1; c <= MaxChannels; c++)
                {
                    ushort port = ChannelPort(basePort, c);
                    if (!asked.Add(ip + ":" + port)) continue;
                    try { udp.Send(ask, ask.Length, new IPEndPoint(ip, PortFor(port))); }
                    catch (Exception) { /* an interface that cannot send: skip it */ }
                }
            }
            if (broadcast)
                foreach (var bc in BroadcastAddresses()) Send(bc, gamePort);
            if (addresses != null)
                foreach (var a in addresses)
                    if (TryResolve(a, gamePort, out var ip, out ushort port)) Send(ip, port);
            float end = Time.realtimeSinceStartup + seconds;
            var seen = new HashSet<string>();
            while (Time.realtimeSinceStartup < end)
            {
                while (udp.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data;
                    try { data = udp.Receive(ref from); }
                    catch (Exception) { break; }
                    var f = Parse(Encoding.UTF8.GetString(data), token, from.Address.ToString());
                    if (f == null || !seen.Add(f.address + ":" + f.port)) continue;
                    f.ping = Time.realtimeSinceStartup - sentAt;
                    results.Add(f);
                }
                yield return null;
            }
            udp.Close();
            results.Sort((x, y) => x.ping.CompareTo(y.ping));
        }

        /// <summary>"host" or "host:port" to an IPv4 address; names are looked up (DNS).</summary>
        public static bool TryResolve(string text, ushort defaultPort, out IPAddress ip, out ushort port)
        {
            ip = null;
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string host = text.Trim();
            int colon = host.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(host.Substring(colon + 1), out ushort p))
            {
                port = p;
                host = host.Substring(0, colon);
            }
            if (IPAddress.TryParse(host, out ip)) return ip.AddressFamily == AddressFamily.InterNetwork;
            try
            {
                foreach (var a in Dns.GetHostAddresses(host))
                    if (a.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ip = a;
                        return true;
                    }
            }
            catch (Exception)
            {
                // an unknown name: not a server
            }
            return false;
        }

        /// <summary>255.255.255.255 and the broadcast address of every IPv4 network this machine is on (LAN, VPN).</summary>
        static IEnumerable<IPAddress> BroadcastAddresses()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        byte[] ip = ua.Address.GetAddressBytes(), mask = ua.IPv4Mask.GetAddressBytes();
                        if (mask.Length != 4) continue;
                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                        list.Add(new IPAddress(b));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] cannot list networks: " + e.Message);
            }
            return list;
        }
    }

    /// <summary>
    /// The addresses a player's game asks for the world besides its own networks: this machine,
    /// the lines of StreamingAssets/servers.txt (shipped with the game; edit it next to the .exe
    /// in RungThiTham_Data/StreamingAssets to point everyone at a new server) and the one the
    /// player typed on the title screen.
    /// </summary>
    public static class ServerList
    {
        const string CustomKey = "rtt.server.custom";

        public static string FilePath => Path.Combine(Application.streamingAssetsPath, "servers.txt");

        /// <summary>The address the player typed (empty: none).</summary>
        public static string Custom
        {
            get => PlayerPrefs.GetString(CustomKey, "");
            set
            {
                PlayerPrefs.SetString(CustomKey, (value ?? "").Trim());
                PlayerPrefs.Save();
            }
        }

        public static List<string> Addresses()
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(Custom)) list.Add(Custom);
            try
            {
                if (File.Exists(FilePath))
                    foreach (var raw in File.ReadAllLines(FilePath))
                    {
                        string line = raw;
                        int hash = line.IndexOf('#');
                        if (hash >= 0) line = line.Substring(0, hash);
                        line = line.Trim();
                        if (line.Length > 0 && !list.Contains(line)) list.Add(line);
                    }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] cannot read " + FilePath + ": " + e.Message);
            }
            if (!list.Contains("127.0.0.1")) list.Add("127.0.0.1");
            return list;
        }
    }
}
