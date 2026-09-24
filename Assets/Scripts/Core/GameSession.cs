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
    /// The session mode and the rules that change with it. Code that must behave differently in
    /// a shared world asks here instead of assuming it is alone.
    /// </summary>
    public static class GameSession
    {
        public static SessionMode Mode = SessionMode.Offline;

        /// <summary>Playing in a shared world (host, client or server).</summary>
        public static bool Online => Mode != SessionMode.Offline;

        /// <summary>
        /// The world's rules run on this machine: enemies think, hits deal damage, loot drops,
        /// XP and quests move on. Offline, on a host and on a server. A client only shows what
        /// the server decided and asks it for what its player wants to do.
        /// </summary>
        public static bool IsAuthority => Mode != SessionMode.Client;

        /// <summary>This machine shows the game: a zone server runs without a screen, sound or HUD.</summary>
        public static bool HasScreen => Mode != SessionMode.Server;

        /// <summary>This machine runs a world other players join (host or server).</summary>
        public static bool Serving => Mode == SessionMode.Host || Mode == SessionMode.Server;

        /// <summary>
        /// One player owns the clock, so pausing, hit-stop and slow motion may change
        /// Time.timeScale. In a shared world time runs on for everyone and those become
        /// effects on this screen only.
        /// </summary>
        public static bool OwnsTime => Mode == SessionMode.Offline;
    }
}
