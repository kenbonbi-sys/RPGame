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

        public Vector2 Facing { get; set; } = Vector2.down;
        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
        public bool IsDashing => Time.time < dashUntil;
        public float SpeedMultiplier { get; set; } = 1f;

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
            current = Vector2.MoveTowards(current, desired * SpeedMultiplier, acceleration * dt);
            knock = Vector2.MoveTowards(knock, Vector2.zero, knockbackDecay * dt);
            rb.linearVelocity = IsDashing ? dashVel : current + knock;
        }
    }
}
