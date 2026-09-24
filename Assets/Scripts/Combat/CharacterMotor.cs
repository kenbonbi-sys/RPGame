using System;
using UnityEngine;

namespace RPG
{
    /// <summary>Velocity-based top-down mover with knockback and dash support.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterMotor : MonoBehaviour
    {
        public float moveSpeed = 4f;
        public float acceleration = 40f;
        public float knockbackDecay = 18f;
        [Tooltip("Heavy characters (bosses) take less knockback.")]
        public float knockbackResist = 1f;
        [Tooltip("Wades slower through swamp water (ZoneRoot.WaterSpeed). Off for swimmers: toads, leeches, snakes.")]
        public bool slowedByWater = true;

        public Vector2 Facing { get; set; } = Vector2.down;
        /// <summary>Standing in swamp water (splashes, a swimmer's hiding).</summary>
        public bool InWater { get; private set; }
        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
        public bool IsDashing => Time.time < dashUntil;
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>Trói or Đóng Băng (set by StatusEffects): no walking and no dashing; knockback still moves.</summary>
        public bool Rooted { get; set; }

        /// <summary>A knockback drove this character into a wall (plan §04: Đẩy Lùi); the contact point.</summary>
        public event Action<Vector2> WallSlammed;

        Rigidbody2D rb;
        Vector2 desired;
        Vector2 current;
        Vector2 knock;
        Vector2 dashVel;
        float dashUntil;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        /// <summary>dir is a normalized direction (or zero). speedScale multiplies moveSpeed.</summary>
        public void Move(Vector2 dir, float speedScale = 1f)
        {
            desired = dir * (moveSpeed * speedScale);
            if (dir.sqrMagnitude > 0.01f) Facing = dir.normalized;
        }

        public void Stop()
        {
            desired = Vector2.zero;
        }

        public void HardStop()
        {
            desired = current = knock = Vector2.zero;
            dashUntil = 0;
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }

        public void AddKnockback(Vector2 impulse)
        {
            knock += impulse * knockbackResist;
        }

        public void Dash(Vector2 velocity, float duration)
        {
            dashVel = velocity;
            dashUntil = Time.time + duration;
            if (velocity.sqrMagnitude > 0.01f) Facing = velocity.normalized;
        }

        public void Teleport(Vector2 pos)
        {
            HardStop();
            rb.position = pos;
            transform.position = pos;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            var zone = ZoneRoot.Current;
            InWater = zone != null && zone.IsWater(rb.position);
            float ground = InWater && slowedByWater ? ZoneRoot.WaterSpeed : 1f;
            current = Vector2.MoveTowards(current, Rooted ? Vector2.zero : desired * SpeedMultiplier * ground, acceleration * dt);
            knock = Vector2.MoveTowards(knock, Vector2.zero, knockbackDecay * dt);
            if (Rooted) dashUntil = 0f;
            rb.linearVelocity = IsDashing ? dashVel : current + knock;
        }

        void OnCollisionEnter2D(Collision2D c) => CheckWallSlam(c);

        void OnCollisionStay2D(Collision2D c) => CheckWallSlam(c);

        /// <summary>Knocked back fast enough, head-on into an obstacle: the knockback ends in a slam.</summary>
        void CheckWallSlam(Collision2D c)
        {
            float min = CombatConfig.Current.wallSlamSpeed;
            if (IsDashing || knock.sqrMagnitude < min * min || c.contactCount == 0) return;
            if (((1 << c.collider.gameObject.layer) & Layers.ObstacleMask) == 0) return;
            var contact = c.GetContact(0);
            if (Vector2.Dot(knock.normalized, contact.normal) > -0.5f) return;   // a glancing touch
            knock = Vector2.zero;
            WallSlammed?.Invoke(contact.point);
        }
    }
}
