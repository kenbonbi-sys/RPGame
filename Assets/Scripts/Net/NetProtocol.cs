using FishNet.Broadcast;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Every message of an online session besides FishNet's own (hero spawns and positions).
    /// Online phases 2–3, Docs/KeHoach-Online.md. The server decides and tells; a client asks.
    /// Bump <see cref="Version"/> when a message changes, and when the world scene gains or loses
    /// enemies, bosses, rocks or crystal pillars (they are numbered in scene order on every machine): an older game
    /// cannot join a newer server.
    /// </summary>
    public static class NetProtocol
    {
        public const int Version = 10;

        /// <summary>First id of the replicated objects of a zone (enemies, boss, rocks); heroes use their NetworkObject id, below it.</summary>
        public const int SceneIdBase = 1000000;
        /// <summary>First id of objects the server makes while playing (thrown rocks, loot).</summary>
        public const int DynamicIdBase = 2000000;
    }

    // ================================================================== login (AccountAuthenticator)
    public enum LoginMode : byte
    {
        /// <summary>An existing character.</summary>
        Login = 0,
        /// <summary>A new character; refused when the name is taken.</summary>
        Register = 1,
        /// <summary>The character, made on the spot if it does not exist yet (automated runs).</summary>
        LoginOrCreate = 2
    }

    public enum LoginCode : byte
    {
        Ok = 0,
        WrongPassword = 1,
        NoSuchCharacter = 2,
        NameTaken = 3,
        Refused = 4
    }

    /// <summary>Client → server: who is logging in, or asking for a new character.</summary>
    public struct LoginHello : IBroadcast
    {
        public string name;
        public LoginMode mode;
        public int version;
    }

    /// <summary>Server → client: the account's salt and a one-time nonce to sign.</summary>
    public struct LoginChallenge : IBroadcast
    {
        public byte[] salt;
        public byte[] nonce;
        public int iterations;
    }

    /// <summary>Client → server: HMAC of the nonce with the account key; a new account also sends its key.</summary>
    public struct LoginProof : IBroadcast
    {
        public byte[] proof;
        public byte[] key;
    }

    /// <summary>Server → client: in or not, and why.</summary>
    public struct LoginResult : IBroadcast
    {
        public LoginCode code;
        public string message;
        public string name;
        public bool created;
        public bool gm;
    }

    // ================================================================== world state (NetWorld)
    /// <summary>A character's statuses as screens show them and as movement feels them.</summary>
    public struct StatusView
    {
        /// <summary>Tenths of a second left (0 = none).</summary>
        public byte stun, freeze, root, slowLeft;
        /// <summary>Share of move speed lost, in hundredths.</summary>
        public byte slow;
        public byte chill, charge, burn, poison;
        /// <summary>1 cursed, 2 judged, 4 immune to crowd control.</summary>
        public byte flags;
    }

    /// <summary>One replicated object of the zone: an enemy, the boss, a Tảng Đá Lớn.</summary>
    public struct EntityState
    {
        public int id;
        public Vector2 pos;
        /// <summary>How high the body is lifted (the boss in the air during Chụp Quăng).</summary>
        public float lift;
        /// <summary>Index of the playing clip in the animator's set; -1 none.</summary>
        public short clip;
        /// <summary>Counts clip (re)starts, so a restarted attack plays again.</summary>
        public byte serial;
        /// <summary>Animator speed × 20.</summary>
        public byte speed;
        /// <summary><see cref="EntityFlags"/>.</summary>
        public byte flags;
        public float hp, maxHp;
        /// <summary>Thanh Trấn Áp, 0..255 of the bar.</summary>
        public byte poise;
        public StatusView status;
    }

    public static class EntityFlags
    {
        public const byte FlipX = 1, Dead = 2, Visible = 4, Engaged = 8, Enraged = 16, PoiseBroken = 32;
        /// <summary>Its body lets heroes through (a leap in the air, a dive under water, a leech clinging on).</summary>
        public const byte Intangible = 64;
    }

    /// <summary>One hero's numbers (its position travels with FishNet's NetworkTransform).</summary>
    public struct HeroState
    {
        public int id;
        public float hp, maxHp, energy, maxEnergy;
        public short level;
        /// <summary>1 dead, 2 invulnerable.</summary>
        public byte flags;
        public StatusView status;
    }

    /// <summary>Server → clients, unreliable, about 15 times a second: what changed since the last one.</summary>
    public struct WorldSnapshot : IBroadcast
    {
        public double time;
        public float dayTime;
        public EntityState[] entities;
        public HeroState[] heroes;
    }

    public enum SpawnKind : byte
    {
        Boulder = 1,
        Loot = 2,
        /// <summary>A fallen boss's treasure chest, for the hero it holds loot for.</summary>
        Chest = 3
    }

    /// <summary>Server → clients: an object made while playing (a thrown rock that stays, a personal loot drop).</summary>
    public struct EntitySpawned : IBroadcast
    {
        public int id;
        public SpawnKind kind;
        /// <summary>Loot: the item id.</summary>
        public string what;
        public int count;
        public Vector2 pos;
        /// <summary>Loot: where it lands after popping out.</summary>
        public Vector2 land;
    }

    public enum GoneHow : byte
    {
        Removed = 0,
        /// <summary>Loot flew into a hero's bag: <see cref="EntityGone.by"/>.</summary>
        Collected = 1,
        /// <summary>A Tảng Đá Lớn broke.</summary>
        Shattered = 2
    }

    public struct EntityGone : IBroadcast
    {
        public int id;
        public GoneHow how;
        public int by;
    }

    /// <summary>Server → clients: a hit landed (numbers, flash, knockback, sparks).</summary>
    public struct HitMsg : IBroadcast
    {
        public int target;
        public int source;
        public float amount;
        public byte type;
        /// <summary><see cref="HitFlags"/>.</summary>
        public byte flags;
        public Vector2 point, dir;
        public float knockback, hitStop;
        public string skill;
    }

    public static class HitFlags
    {
        public const byte Crit = 1, Dot = 2, Contact = 4, Feedback = 8, Pure = 16;
    }

    public struct HealMsg : IBroadcast
    {
        public int target;
        public float amount;
        public bool show;
    }

    /// <summary>Server → clients: a hero used a skill; the others show it on their copy of that hero.</summary>
    public struct CastMsg : IBroadcast
    {
        public int hero;
        public byte slot;
        public Vector2 aim, origin;
        public byte level;
        public int seed;
    }

    /// <summary>Server → clients: a buff started or ended on a hero.</summary>
    public struct BuffMsg : IBroadcast
    {
        public int hero;
        public string buff;
        public float remaining;
        public bool on;
    }

    /// <summary>Server → clients: something to see or hear (<see cref="NetCues"/>).</summary>
    public struct CueMsg : IBroadcast
    {
        public byte kind;
        public string id;
        public Vector2 pos, pos2;
        public float a, b, c, d;
        public int target;
        public Color color;
        public string text;
        public byte flag;
    }

    /// <summary>Server → clients: a hero's name (on arrival, and for everyone already there when someone joins).</summary>
    public struct HeroInfoMsg : IBroadcast
    {
        public int hero;
        public string name;
        /// <summary>Their people, class, weapon and looks (<see cref="HeroLook"/> as JSON): every screen draws them.</summary>
        public string look;
    }

    // ================================================================== one player's own things
    /// <summary>Server → owner: one saved section of their character (stats, bag, quests, Bách Khoa Trùm…).</summary>
    public struct SectionMsg : IBroadcast
    {
        public string key;
        public string json;
    }

    public enum NoticeKind : byte
    {
        Log = 0,
        Banner = 1,
        Sound = 2,
        WorldText = 3,
        ItemPicked = 4,
        LevelUp = 5,
        QuestCompleted = 6,
        QuestChanged = 7,
        ScreenFlash = 8,
        Shake = 9,
        SlowMo = 10
    }

    /// <summary>Server → owner: a message for that player only ("Nhận được…", "Lên cấp!", quest banners).</summary>
    public struct NoticeMsg : IBroadcast
    {
        public NoticeKind kind;
        public string text, text2;
        public Color color;
        public Vector3 pos;
        public float a, b;
        public byte style;
    }

    public enum ControlKind : byte
    {
        /// <summary>The hero fell: death screen, respawn in <see cref="ControlMsg.value"/> seconds.</summary>
        Downed = 1,
        /// <summary>The hero gets up at <see cref="ControlMsg.pos"/>.</summary>
        Respawn = 2,
        /// <summary>Move the hero (the server corrected an impossible position, or a GM teleport).</summary>
        Teleport = 3,
        /// <summary>A skill the owner already showed was refused (reason in text).</summary>
        CastRefused = 4,
        /// <summary>A request finished (<see cref="ControlMsg.id"/>): the conversation may go on.</summary>
        Ack = 5,
        /// <summary>A line for the console (GM commands, server messages).</summary>
        Console = 6,
        /// <summary>Everything about the character has arrived: play.</summary>
        Ready = 7,
        /// <summary><see cref="ControlMsg.text"/> asks this player into their party (the answer: <see cref="ActKind.PartyAnswer"/>).</summary>
        PartyInvite = 8,
        /// <summary>A line for this player's log only (answers to social commands: friends, parties).</summary>
        Info = 9
    }

    /// <summary>Server → owner: orders and answers for that player's own hero.</summary>
    public struct ControlMsg : IBroadcast
    {
        public ControlKind kind;
        public Vector2 pos;
        public float value;
        public int id;
        public bool ok;
        public string text;
    }

    // ================================================================== requests (client → server)
    /// <summary>Client → server: the player used a skill (already shown on their screen).</summary>
    public struct CastRequest : IBroadcast
    {
        public byte slot;
        public Vector2 aim, origin;
    }

    public enum ActKind : byte
    {
        Potion = 1,
        TalkStart = 2,
        TalkEnd = 3,
        QuestStart = 4,
        QuestComplete = 5,
        SetFlag = 6,
        GiveItem = 7,
        SpendStat = 8,
        Console = 9,
        DialogueVars = 11,
        /// <summary>The player's ping in ms (<see cref="ActRequest.value"/>), every few seconds: the server holds enemies' hits that long (<see cref="LagCompensation"/>).</summary>
        Ping = 12,
        /// <summary>Asks <see cref="ActRequest.text"/> into this player's party.</summary>
        PartyInvite = 13,
        /// <summary>The answer to <see cref="ActRequest.text"/>'s invitation: <see cref="ActRequest.value"/> 1 yes, 0 no.</summary>
        PartyAnswer = 14,
        PartyLeave = 15,
        /// <summary>The leader sends <see cref="ActRequest.text"/> out of the party.</summary>
        PartyKick = 16,
        /// <summary>Adds <see cref="ActRequest.text"/> to this player's friends (<see cref="ActRequest.value"/> 0: removes them).</summary>
        Friend = 17,
        /// <summary>This player's friends and who of them is playing, as an <see cref="ControlKind.Info"/> line.</summary>
        FriendList = 18,
        /// <summary>Travel from the Đá Truyền Tống the hero stands at to the woken one <see cref="ActRequest.text"/>.</summary>
        Travel = 19,
        /// <summary>
        /// The character creator's choice (<see cref="ActRequest.text"/>: a <see cref="HeroLook"/> as JSON): people and
        /// class once, looks and the weapon (among the class's) any time.
        /// </summary>
        ChooseLook = 20,
        /// <summary>The smith's forge: <see cref="ActRequest.id"/> a <see cref="Forge.Action"/>, its metal in value or its weapon in text.</summary>
        Forge = 21,
        /// <summary>Puts on the gear <see cref="ActRequest.text"/> from the bag, or (empty text) takes off what is worn in the slot <see cref="ActRequest.value"/>.</summary>
        Equip = 22,
        /// <summary>Eats or drinks the item <see cref="ActRequest.text"/> from the bag (food, potions: the bag's right click).</summary>
        UseItem = 23
    }

    /// <summary>Client → server: everything else a player wants (potions, talking, dialogue commands, stat points, chat).</summary>
    public struct ActRequest : IBroadcast
    {
        public ActKind kind;
        public int id;
        public int value;
        public string text;
    }

    public enum ChatChannel : byte
    {
        /// <summary>Everyone in the world.</summary>
        World = 0,
        /// <summary>The speaker's party.</summary>
        Party = 1,
        /// <summary>One player (<see cref="ChatMsg.to"/>).</summary>
        Whisper = 2
    }

    /// <summary>Client → server: something to say. A whisper's text starts with the name it goes to (names may have spaces; the server finds it).</summary>
    public struct ChatRequest : IBroadcast
    {
        public ChatChannel channel;
        public string text;
    }

    /// <summary>Server → clients: a chat line.</summary>
    public struct ChatMsg : IBroadcast
    {
        public ChatChannel channel;
        public string from;
        /// <summary>A whisper: who it went to (the sender sees their own line too).</summary>
        public string to;
        public string text;
        public bool system;
    }

    /// <summary>Server → each member: their party now (no names: not in one). Heroes are network ids, 0 when not in the world yet.</summary>
    public struct PartyMsg : IBroadcast
    {
        public string leader;
        public string[] names;
        public int[] heroes;
    }
}
