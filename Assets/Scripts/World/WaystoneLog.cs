using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// The Đá Truyền Tống one hero has woken, and the last one they touched (where they get up
    /// after a fall). Saved with the character; online the server keeps it and sends it to its
    /// player (the "waystones" section), whose screen lights the stones and fills the map.
    /// </summary>
    public class WaystoneLog : MonoBehaviour, ICharacterSaveable
    {
        readonly List<string> known = new List<string>();

        /// <summary>The stone touched last: the hero gets up there after a fall (empty: the zone's spawn).</summary>
        public string Last { get; private set; } = "";

        public IReadOnlyList<string> Known => known;
        public event Action Changed;

        public PlayerController Owner { get; private set; }

        void Awake()
        {
            Owner = GetComponent<PlayerController>();
            SaveRegistry.Register(this);
        }

        void OnDestroy() => SaveRegistry.Unregister(this);

        public bool Knows(string id) => !string.IsNullOrEmpty(id) && known.Contains(id);

        /// <summary>
        /// The hero stands at <paramref name="stone"/>: it wakes (the first time) and becomes where
        /// they get up after a fall. Where the world's rules run.
        /// </summary>
        public void Touch(Waystone stone)
        {
            if (stone == null || Owner == null || Owner.IsDead) return;
            bool woke = !known.Contains(stone.stoneId);
            bool moved = Last != stone.stoneId;
            if (!woke && !moved) return;
            if (woke) known.Add(stone.stoneId);
            Last = stone.stoneId;
            if (woke)
            {
                NetCues.Vfx("waystone_wake", stone.transform.position + Vector3.up * 1.1f);
                NetCues.Sound("sfx_waystone", 0.9f, 0.03f, stone.transform.position);
                Notify.Log(Owner, $"Đã đánh thức Đá Truyền Tống: {stone.displayName}. Đứng cạnh đá bấm F (hoặc mở bản đồ, M) để dịch chuyển.", Palette.LogQuest);
                Notify.Banner(Owner, BannerKind.Quest, "Đá Truyền Tống", stone.displayName);
            }
            else Notify.Log(Owner, $"Điểm hồi sinh: Đá Truyền Tống {stone.displayName}.", Palette.LogInfo);
            Changed?.Invoke();
        }

        /// <summary>Why the hero cannot travel to <paramref name="toId"/> now, or null when they can.</summary>
        public string TravelRefusal(string toId, out Waystone from, out Waystone to)
        {
            from = Owner != null ? Waystone.At(Owner.transform.position) : null;
            to = Waystone.Find(toId);
            if (Owner == null || Owner.IsDead) return "Không thể dịch chuyển lúc này.";
            if (from == null) return "Hãy đứng cạnh một Đá Truyền Tống.";
            if (to == null || !Knows(toId)) return "Bạn chưa đánh thức viên đá đó.";
            if (to == from) return "Bạn đang ở đây rồi.";
            if (!Knows(from.stoneId)) return "Viên đá này chưa được đánh thức.";
            return null;
        }

        /// <summary>The player on this screen asks to travel: offline (or a host's own hero) at once, online through the server.</summary>
        public void RequestTravel(string toId)
        {
            if (GameSession.IsAuthority)
            {
                Travel(toId);
                return;
            }
            string why = TravelRefusal(toId, out _, out _);
            if (why != null)
            {
                GameEvents.RaiseLog(why, new Color(1f, 0.6f, 0.5f));
                return;
            }
            OnlineSession.Ask(new ActRequest { kind = ActKind.Travel, text = toId });
        }

        /// <summary>
        /// Travels to a woken stone (where the rules run): offline the hero moves at once; a server
        /// moves its player's hero (<see cref="ServerPlayers.Teleport"/>). False with the reason told.
        /// </summary>
        public bool Travel(string toId)
        {
            string why = TravelRefusal(toId, out var from, out var to);
            if (why != null)
            {
                Notify.Log(Owner, why, new Color(1f, 0.6f, 0.5f));
                return false;
            }
            NetCues.Vfx("respawn", Owner.transform.position);
            NetCues.Sound("sfx_waystone", 0.8f, 0.03f, Owner.transform.position);
            Vector2 at = to.Arrival;
            if (GameSession.Serving && !Owner.IsLocal) ServerPlayers.Teleport(Owner, at);
            else
            {
                Owner.motor.Teleport(at);
                if (Owner.IsLocal && CameraRig.I != null) CameraRig.I.SnapToTarget();
            }
            Last = to.stoneId;
            NetCues.Vfx("respawn", at);
            NetCues.Sound("sfx_waystone", 0.8f, 0.03f, at);
            Notify.Log(Owner, $"Đã dịch chuyển tới {to.displayName}.", Palette.LogInfo);
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ save
        [Serializable]
        class SaveState
        {
            public List<string> known = new List<string>();
            public string last;
        }

        public string SaveKey => "waystones";

        public string CaptureState() => JsonUtility.ToJson(new SaveState { known = new List<string>(known), last = Last });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<SaveState>(json);
            known.Clear();
            if (s != null && s.known != null)
                foreach (var id in s.known)
                    if (!string.IsNullOrEmpty(id) && !known.Contains(id)) known.Add(id);
            Last = s != null && s.last != null ? s.last : "";
            Changed?.Invoke();
        }
    }
}
