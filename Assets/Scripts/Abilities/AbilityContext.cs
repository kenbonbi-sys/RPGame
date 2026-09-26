using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Anything that can use abilities: the hero now, enemies with EnemyDef later.</summary>
    public interface IAbilityCaster
    {
        /// <summary>Runs the ability's coroutines.</summary>
        MonoBehaviour Runner { get; }
        Team Team { get; }
        Health Health { get; }
        CharacterMotor Motor { get; }
        StatusEffects Status { get; }
        AfterImageSpawner AfterImages { get; }
        int Level { get; }
        bool IsDead { get; }
        /// <summary>Attack used by Damage blocks: damage = power × Attack (plan §04).</summary>
        float Attack(DamageType type);
        /// <summary>Outgoing damage multiplier for this ability, 1 = none: the "(1 + Tăng%)" of plan §04.</summary>
        float DamageDealt(AbilityDef ability);
        void BeginAction(string animBase, Vector2 dir, float lockTime, float moveMul);
        void AddBuff(BuffSpec buff);
        void RemoveBuff(string id);
    }

    /// <summary>How much of a cast happens on this machine (online phase 3).</summary>
    public enum CastMode
    {
        /// <summary>The rules and the show: offline, a host (every hero), a hero whose rules run here and are seen here.</summary>
        Live,
        /// <summary>The rules only: a server without a screen.</summary>
        Rules,
        /// <summary>The show now, the rules on the server: a player's machine casting for its own hero.</summary>
        Predicted,
        /// <summary>The show only: another player's hero, because the server said so.</summary>
        Shown
    }

    /// <summary>What one cast knows: who, where, which level, and where the current block happens.</summary>
    public class AbilityContext
    {
        public AbilityDef ability;
        public IAbilityCaster caster;
        public int level = 1;
        /// <summary>Hits, heals, buffs and dodges really happen (where the world's rules run).</summary>
        public bool live = true;
        /// <summary>Effects, sounds and poses show (this machine has a screen).</summary>
        public bool visual = true;
        /// <summary>The player's own machine shows the cast before the server answers.</summary>
        public bool predicted;
        /// <summary>Random numbers of this cast; seeded the same on every machine so they show the same spikes and bolts.</summary>
        public System.Random rng = new System.Random();
        /// <summary>Running combo count of a Combo block (0, 1, 2…).</summary>
        public int combo;
        /// <summary>Caster's feet when cast.</summary>
        public Vector2 origin;
        /// <summary>Target point (clamped to the ability's range).</summary>
        public Vector2 aim;
        /// <summary>Normalized origin → aim.</summary>
        public Vector2 dir;
        /// <summary>Where the current block happens (a spike, a bolt, an explosion).</summary>
        public Vector2 point;
        /// <summary>The target that was hit, for blocks that follow a hit.</summary>
        public Health target;
        /// <summary>Size multiplier handed down by Line blocks (growing spikes).</summary>
        public float scale = 1f;
        /// <summary>Damage multiplier of this one cast (the skill after Lướt Hoàn Hảo: ×1.3).</summary>
        public float damageMultiplier = 1f;

        public Team Team => caster.Team;
        public Transform CasterTransform => caster.Runner.transform;
        public Vector2 CasterPosition => caster.Runner.transform.position;
        public bool Alive => caster != null && caster.Runner != null && !caster.IsDead;
        public float PowerScale => 1f + ability.powerPerLevel * (Mathf.Max(1, level) - 1);

        /// <summary>0..1 from this cast's random numbers.</summary>
        public float Rand01() => (float)rng.NextDouble();

        public float RandRange(float min, float max) => min + (max - min) * Rand01();

        public int RandInt(int maxExclusive) => maxExclusive > 0 ? rng.Next(maxExclusive) : 0;

        /// <summary>A point in a circle of radius <paramref name="r"/>, from this cast's random numbers.</summary>
        public Vector2 RandInCircle(float r)
        {
            if (r <= 0f) return Vector2.zero;
            float a = Rand01() * Mathf.PI * 2f;
            float d = Mathf.Sqrt(Rand01()) * r;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
        }

        public AbilityContext Copy() => (AbilityContext)MemberwiseClone();

        public AbilityContext At(Vector2 p, Health t = null)
        {
            var c = Copy();
            c.point = p;
            c.target = t;
            return c;
        }

        /// <summary>What power 1 is worth for an element: the ability's level scaling × the caster's Attack × damage bonuses × this cast's multiplier.</summary>
        public float HitScale(DamageType type) => PowerScale * caster.Attack(type) * caster.DamageDealt(ability) * damageMultiplier;

        /// <summary>A hit worth <paramref name="power"/> × the caster's Attack for its element.</summary>
        public DamageInfo MakeDamage(float power, DamageType type, Vector2 at, Vector2 direction, float knockback = 0f)
        {
            var d = DamageInfo.Make(power * HitScale(type), Team, caster.Runner.gameObject, at, direction, type, knockback);
            d.attackScaled = true;
            d.sourceLevel = caster.Level;
            d.skillName = ability.displayName;
            return d;
        }

        /// <summary>
        /// A hero's basic attack (slot Q) while their weapon is charged (Lôi Ấn, <see cref="BuffSpec.imbuePower"/>):
        /// more power and stacks of Tích Điện. True when it was charged.
        /// </summary>
        public bool Imbue(ref float power, ref StatusHit status)
        {
            if (!(caster is PlayerController pc) || pc.ImbuePower <= 0f || pc.skills == null || pc.skills.slots[0] != ability) return false;
            power *= 1f + pc.ImbuePower;
            status.charge += pc.ImbueCharge;
            return true;
        }

        public Coroutine Start(IEnumerator routine) => caster.Runner.StartCoroutine(routine);

        /// <summary>Runs blocks in order; blocks with a delay start on their own timer.</summary>
        public void Run(List<AbilityEffect> effects)
        {
            if (effects == null) return;
            foreach (var e in effects)
            {
                if (e == null) continue;
                if (e.delay > 0f) Start(Delayed(e, e.delay));
                else e.Run(this);
            }
        }

        IEnumerator Delayed(AbilityEffect e, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (Alive) e.Run(this);
        }
    }

    /// <summary>One block of an ability's timeline. Subclasses are picked in the Inspector.</summary>
    [System.Serializable]
    public abstract class AbilityEffect
    {
        [Tooltip("Seconds after the cast (or after the block that triggered it).")]
        public float delay;

        public abstract void Run(AbilityContext ctx);
    }

    /// <summary>Where a block happens, relative to the caster or to the current point.</summary>
    [System.Serializable]
    public struct Anchor
    {
        public enum From { Caster, Point, Aim, Origin }

        public From from;
        [Tooltip("World units up (e.g. 0.45 = chest height).")]
        public float up;
        [Tooltip("World units along the cast direction.")]
        public float forward;

        public Anchor(From from, float up = 0f, float forward = 0f)
        {
            this.from = from;
            this.up = up;
            this.forward = forward;
        }

        public Vector2 Resolve(AbilityContext ctx)
        {
            Vector2 p = from == From.Caster ? ctx.CasterPosition : from == From.Aim ? ctx.aim : from == From.Origin ? ctx.origin : ctx.point;
            return p + Vector2.up * up + ctx.dir * forward;
        }
    }
}
