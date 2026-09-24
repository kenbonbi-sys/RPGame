using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Giám sát (online phase 5, Docs/MayChu.md): a dedicated server writes how it is doing every
    /// <see cref="Every"/> seconds to data/status-k&lt;channel&gt;.json — who plays, since when it
    /// runs, frames per second, memory, errors and warnings logged so far — for
    /// Tools/Server/status-server.ps1 (or anyone) to read, and warns in its log when it runs slow.
    /// </summary>
    public class ServerStatus : IDisposable
    {
        public const float Every = 10f;
        /// <summary>Below this many frames per second the log gets a warning (the world runs at 60).</summary>
        public const float SlowFps = 25f;

        [Serializable]
        public class Snapshot
        {
            public int version;
            public int channel;
            public int port;
            public string name;
            public int players;
            public int maxPlayers;
            public List<string> names = new List<string>();
            public string started;
            public string updated;
            public int uptimeSeconds;
            public float fps;
            public int memoryMB;
            public int accounts;
            public int errors;
            public int warnings;
            public string lastError = "";
        }

        readonly string path;
        readonly DateTime started = DateTime.Now;
        float nextWrite, windowStart;
        int windowFrame;
        bool warnedSlow;
        int errors, warnings;
        string lastError = "";

        public ServerStatus(string dataFolder, int channel)
        {
            path = Path.Combine(dataFolder, $"status-k{channel}.json");
            windowStart = Time.realtimeSinceStartup;
            windowFrame = Time.frameCount;
            nextWrite = Time.realtimeSinceStartup + 2f;
            Application.logMessageReceived += Count;
        }

        public void Dispose() => Application.logMessageReceived -= Count;

        void Count(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) warnings++;
            else if (type != LogType.Log)
            {
                errors++;
                lastError = message.Length > 200 ? message.Substring(0, 200) : message;
            }
        }

        /// <summary>Every <see cref="Every"/> seconds writes the file (the server calls it every frame).</summary>
        public void Tick(ServerPlayers players)
        {
            float now = Time.realtimeSinceStartup;
            if (now < nextWrite) return;
            float fps = (Time.frameCount - windowFrame) / Mathf.Max(0.001f, now - windowStart);
            windowFrame = Time.frameCount;
            windowStart = now;
            nextWrite = now + Every;
            if (fps < SlowFps && !warnedSlow) Debug.LogWarning($"[Server] running slow: {fps:0} frames per second with {players.Count} players");
            warnedSlow = fps < SlowFps;
            Write(Capture(players, fps));
        }

        public Snapshot Capture(ServerPlayers players, float fps)
        {
            var s = new Snapshot
            {
                version = NetProtocol.Version,
                channel = OnlineSession.Channel,
                port = OnlineSession.Port,
                name = OnlineSession.ServerName,
                players = players.Count,
                maxPlayers = ServerPlayers.MaxPlayers,
                started = started.ToString("o"),
                updated = DateTime.Now.ToString("o"),
                uptimeSeconds = (int)(DateTime.Now - started).TotalSeconds,
                fps = Mathf.Round(fps * 10f) / 10f,
                accounts = players.Store != null ? players.Store.CountAccounts() : 0,
                errors = errors,
                warnings = warnings,
                lastError = lastError
            };
            foreach (var session in players.Sessions) s.names.Add(session.Name);
            long bytes = 0;
            try
            {
                bytes = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            }
            catch (Exception)
            {
                // not every runtime can tell
            }
            // Mono says 0 for its own process: what Unity's allocators hold, and the managed heap
            if (bytes <= 0) bytes = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() + GC.GetTotalMemory(false);
            s.memoryMB = (int)(bytes / (1024 * 1024));
            return s;
        }

        void Write(Snapshot s)
        {
            try
            {
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(s, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Server] cannot write the status file: " + e.Message);
            }
        }
    }
}
