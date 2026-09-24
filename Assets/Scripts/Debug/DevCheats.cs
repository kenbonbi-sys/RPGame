using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Handy keys while prototyping:
    /// F5 full heal/energy + potions · F6 skip time (day/night) · F7 teleport to boss arena
    /// F8 teleport to village · F9 kill enemies nearby · ` opens the DebugConsole.
    /// Turning enableCheats off also disables the console. Online the keys become console
    /// commands the server runs for game masters only.
    /// </summary>
    public class DevCheats : MonoBehaviour
    {
        public bool enableCheats = true;

        void Update()
        {
            if (!enableCheats) return;
            var gm = GameManager.I;
            var p = Players.Local;
            if (gm == null || p == null) return;
            if (GameSession.Online)
            {
                ServerKeys();
                return;
            }
            if (InputReader.Cheat(0))
            {
                p.health.Heal(9999);
                p.energy = p.maxEnergy;
                p.skills.ResetCooldowns();
                p.inventory.Add(gm.db.Item("potion_red"), 3, false);
                p.inventory.Add(gm.db.Item("potion_blue"), 3, false);
                GameEvents.RaiseLog("[Cheat] Hồi đầy máu, năng lượng và hồi chiêu.");
            }
            if (InputReader.Cheat(1) && DayNightCycle.I != null)
            {
                DayNightCycle.I.time = Mathf.Repeat(DayNightCycle.I.time + 0.25f, 1f);
                GameEvents.RaiseLog("[Cheat] Tua thời gian +6 giờ.");
            }
            if (InputReader.Cheat(2) && gm.bossSpot != null)
            {
                p.motor.Teleport((Vector2)gm.bossSpot.position + Vector2.down * 6.5f);
                if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            }
            if (InputReader.Cheat(3) && gm.respawnPoint != null)
            {
                p.motor.Teleport(gm.respawnPoint.position);
                if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            }
            if (InputReader.Cheat(4))
            {
                foreach (var e in EnemyBase.All.ToArray())
                    if (!e.IsDead && Vector2.Distance(e.transform.position, p.transform.position) < 12f) e.health.Kill();
            }
        }

        /// <summary>Online the same keys ask the server (game masters only).</summary>
        static void ServerKeys()
        {
            var console = DebugConsole.I;
            if (console == null) return;
            if (InputReader.Cheat(0)) console.Execute("heal");
            if (InputReader.Cheat(1))
            {
                float t = Mathf.Repeat((DayNightCycle.I != null ? DayNightCycle.I.time : 0f) + 0.25f, 1f);
                console.Execute("time " + t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
            if (InputReader.Cheat(2)) console.Execute("tp boss");
            if (InputReader.Cheat(3)) console.Execute("tp spawn");
            if (InputReader.Cheat(4)) console.Execute("kill");
        }
    }
}
