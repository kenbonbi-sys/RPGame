using FishNet.Object;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// A hero in an online session (online phase 1, Docs/KeHoach-Online.md). The player who owns
    /// it plays it exactly like offline, and its NetworkTransform sends where it walks. Every other
    /// copy — on the server and on the other players' screens — is a puppet: it follows those
    /// updates, animates from its motion and carries a name tag.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class NetworkHero : NetworkBehaviour
    {
        PlayerController hero;
        NameplateUI plate;

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
                if (HUD.I != null) plate = HUD.I.CreateNameplate(transform, $"Người chơi {OwnerId}", false, new Color(0.55f, 0.85f, 1f), null, 1.62f);
                return;
            }
            hero.SetPuppet(false);
            Players.SetLocal(hero);
            if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            GameEvents.RaiseLog($"Đã vào thế giới online (người chơi {OwnerId}).", Palette.LogQuest);
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
