using System;
using System.Diagnostics;
using System.Threading;

namespace RPG
{
    /// <summary>
    /// How Tools/Server/stop-server.ps1 asks a running server to stop cleanly (Docs/MayChu.md):
    /// the server tells its players, saves every character and quits, instead of being killed
    /// with up to 30 seconds of play unsaved. A named event of this Windows session,
    /// <see cref="NameFor"/> the server's process id, set from outside.
    /// </summary>
    public sealed class ServerStopSignal : IDisposable
    {
        EventWaitHandle handle;

        public static string NameFor(int processId) => "Local\\RungThiTham-Stop-" + processId;

        /// <summary>Starts listening for this process; null when named events are not available (the server can still be killed).</summary>
        public static ServerStopSignal Listen() => Listen(Process.GetCurrentProcess().Id);

        public static ServerStopSignal Listen(int processId)
        {
            try
            {
                return new ServerStopSignal { handle = new EventWaitHandle(false, EventResetMode.ManualReset, NameFor(processId)) };
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Server] cannot listen for stop requests: " + e.Message);
                return null;
            }
        }

        /// <summary>Someone asked this server to stop.</summary>
        public bool Requested
        {
            get
            {
                try { return handle != null && handle.WaitOne(0); }
                catch (ObjectDisposedException) { return false; }
            }
        }

        /// <summary>What stop-server.ps1 does, for tests: asks the server of <paramref name="processId"/> to stop.</summary>
        public static bool Ask(int processId)
        {
            try
            {
                using (var e = EventWaitHandle.OpenExisting(NameFor(processId))) return e.Set();
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Dispose()
        {
            handle?.Close();
            handle = null;
        }
    }
}
