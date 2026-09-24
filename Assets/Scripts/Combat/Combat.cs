using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Shared combat helpers: area damage, knockback and hit feedback.</summary>
    public static class Combat
    {
        static readonly List<Health> Buffer = new List<Health>(32);

        /// <summary>Damages everything in a circle. Returns the number of targets hit.</summary>
        public static int DamageCircle(Vector2 center, float radius, DamageInfo template, float critChance = 0f, bool feedback = true)
        {
            DebugConsole.RecordArea(center, radius);
            Util.HealthsInCircle(center, radius, template.sourceTeam, Buffer);
            int n = 0;
            foreach (var h in Buffer)
            {
                var d = template;
                d.point = h.transform.position;
                Vector2 away = (Vector2)h.transform.position - center;
                d.direction = away.sqrMagnitude > 0.001f ? away.normalized : template.direction;
                d = d.RollCrit(critChance);
                if (h.TakeDamage(d) > 0)
                {
                    n++;
                    if (feedback) OnHitFeedback(h, d);
                }
            }
            return n;
        }

        public static int DamageCone(Vector2 origin, Vector2 dir, float radius, float angle, DamageInfo template, float critChance = 0f)
        {
            DebugConsole.RecordArea(origin, radius, dir, angle);
            Util.HealthsInCone(origin, dir, radius, angle, template.sourceTeam, Buffer);
            int n = 0;
            foreach (var h in Buffer)
            {
                var d = template;
                d.point = h.transform.position;
                d.direction = ((Vector2)h.transform.position - origin).normalized;
                d = d.RollCrit(critChance);
                if (h.TakeDamage(d) > 0)
                {
                    n++;
                    OnHitFeedback(h, d);
                }
            }
            return n;
        }

        /// <summary>Knockback, flash, sparks — applied to whoever got hit.</summary>
        public static void OnHitFeedback(Health h, DamageInfo d)
        {
            if (h == null) return;
            var motor = h.GetComponent<CharacterMotor>();
            if (motor != null && d.knockback > 0) motor.AddKnockback(d.direction * d.knockback);
            var flash = h.GetComponentInChildren<HitFlash>();
            if (flash != null) flash.Flash(d.crit ? new Color(1f, 0.95f, 0.6f) : Color.white, 1f, 0.12f);
            Vector3 p = (Vector3)d.point + Vector3.up * 0.5f;
            string fx = d.type == DamageType.Fire ? "hit_fire" : d.type == DamageType.Ice ? "hit_ice" : d.type == DamageType.Lightning ? "hit_lightning" : "hit_spark";
            VFX.Spawn(fx, p, Quaternion.Euler(0, 0, Random.Range(0, 360f)));
            float stop = HitStopFor(h, d);
            if (stop > 0) TimeFX.HitStop(stop);
            if (d.crit)
            {
                CameraRig.Shake(0.12f);
                AudioManager.Play("sfx_crit", 0.6f, 0.05f, p);
            }
        }

        /// <summary>
        /// Hit-stop of plan §04 in three tiers (<see cref="CombatConfig"/>): a hit keeps its own, the
        /// light 35 ms of a swing or the heavy 70 ms of a combo finisher. The hero's crits on hits
        /// that have a hit-stop are at least heavy. A kill is at least light, and the killing blow on
        /// a boss or elite (whoever has a Thanh Trấn Áp) gets the 120 ms of a break. Hits without
        /// their own hit-stop (pulses, storms) stay without one unless they kill, so they never
        /// stutter. Enemies' hits keep their own.
        /// </summary>
        public static float HitStopFor(Health target, DamageInfo d)
        {
            float s = d.hitStop;
            if (d.sourceTeam != Team.Player || target == null) return s;
            var c = CombatConfig.Current;
            if (d.crit && s > 0f) s = Mathf.Max(s, c.hitStopHeavy);
            if (target.IsDead) s = Mathf.Max(s, target.GetComponent<Poise>() != null ? c.hitStopBreak : c.hitStopLight);
            return s;
        }
    }
}
