using FishNet.Object;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A hero in an online session (Docs/KeHoach-Online.md). The player who owns it moves it like
    /// offline (its NetworkTransform sends where it walks) and asks the server for everything else;
    /// the server's copy follows those positions and runs the rules for it (damage, skills, loot,
    /// quests); every other screen shows a puppet with the character's name over it.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class NetworkHero : NetworkBehaviour
    {
        PlayerController hero;
        NameplateUI plate;
        string displayName;

        public PlayerController Hero => hero;

        void Awake() => hero = GetComponent<PlayerController>();

        /// <summary>The server shows heroes; their owners move them.</summary>
        public override void OnStartServer()
        {
            base.OnStartServer();
            name = $"Hero {OwnerId}";
            hero.SetPuppet(true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            name = $"Hero {OwnerId}";
            if (!IsOwner)
            {
                hero.SetPuppet(true);
                SetDisplayName(NetWorld.HeroName(ObjectId + 1) ?? ServerPlayers.NameOf(hero) ?? "…");
                string look = NetWorld.HeroLookOf(ObjectId + 1);
                if (!string.IsNullOrEmpty(look) && hero.stats != null) hero.stats.LoadLook(HeroLook.FromJson(look));
                return;
            }
            hero.SetPuppet(false);
            Players.SetLocal(hero);
            if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            if (OnlineSession.I != null) OnlineSession.I.HeroArrived(hero);
        }

        /// <summary>The character's name over another player's hero.</summary>
        public void SetDisplayName(string newName)
        {
            displayName = newName;
            if (IsOwner || HUD.I == null) return;
            if (plate == null)
                plate = HUD.I.CreateNameplate(transform, displayName, false, new Color(0.55f, 0.85f, 1f), hero.health, 1.62f);
            else plate.SetLabel(displayName);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            ReleasePlate();
            if (Players.Local == hero) Players.SetLocal(null);
        }

        void OnDestroy() => ReleasePlate();

        void ReleasePlate()
        {
            if (plate == null) return;
            plate.Release();
            plate = null;
        }
    }
}
