using UnityEngine;
using UnityEngine.InputSystem;

namespace RPG
{
    /// <summary>
    /// Handy keys while prototyping:
    /// F5 full heal/energy + potions · F6 skip time (day/night) · F7 teleport to boss arena
    /// F8 teleport to village · F9 kill enemies nearby
    /// </summary>
    public class DevCheats : MonoBehaviour
    {
        public bool enableCheats = true;

        void Update()
        {
            if (!enableCheats) return;
            var gm = GameManager.I;
            if (gm == null || gm.player == null) return;
            var p = gm.player;
            if (InputReader.Pressed(Key.F5))
            {
                p.health.Heal(9999);
                p.energy = p.maxEnergy;
                p.skills.ResetCooldowns();
                Inventory.I.Add(gm.db.Item("potion_red"), 3, false);
                Inventory.I.Add(gm.db.Item("potion_blue"), 3, false);
                GameEvents.RaiseLog("[Cheat] Hồi đầy máu, năng lượng và hồi chiêu.");
            }
            if (InputReader.Pressed(Key.F6) && DayNightCycle.I != null)
            {
                DayNightCycle.I.time = Mathf.Repeat(DayNightCycle.I.time + 0.25f, 1f);
                GameEvents.RaiseLog("[Cheat] Tua thời gian +6 giờ.");
            }
            if (InputReader.Pressed(Key.F7) && gm.bossSpot != null)
            {
                p.motor.Teleport((Vector2)gm.bossSpot.position + Vector2.down * 6.5f);
                if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            }
            if (InputReader.Pressed(Key.F8) && gm.respawnPoint != null)
            {
                p.motor.Teleport(gm.respawnPoint.position);
                if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            }
            if (InputReader.Pressed(Key.F9))
            {
                foreach (var e in EnemyBase.All.ToArray())
                    if (!e.IsDead && Vector2.Distance(e.transform.position, p.transform.position) < 12f) e.health.Kill();
            }
        }
    }
}
