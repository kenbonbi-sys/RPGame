namespace RPG
{
    /// <summary>How this copy of the game runs (Docs/KeHoach-Online.md).</summary>
    public enum SessionMode
    {
        /// <summary>One player on this machine: the classic game with local save slots.</summary>
        Offline,
        /// <summary>Runs the world and plays in it.</summary>
        Host,
        /// <summary>Plays in a world that runs on a server.</summary>
        Client,
        /// <summary>Runs the world with no screen and no hero of its own (a zone server).</summary>
        Server
    }

    /// <summary>
    /// The session mode and the rules that change with it. Offline is the only mode until the
    /// network layer arrives (online phase 1); code that must behave differently in a shared
    /// world asks here instead of assuming it is alone.
    /// </summary>
    public static class GameSession
    {
        public static SessionMode Mode = SessionMode.Offline;

        /// <summary>
        /// One player owns the clock, so pausing, hit-stop and slow motion may change
        /// Time.timeScale. In a shared world time runs on for everyone and those become
        /// effects on this screen only.
        /// </summary>
        public static bool OwnsTime => Mode == SessionMode.Offline;
    }
}
