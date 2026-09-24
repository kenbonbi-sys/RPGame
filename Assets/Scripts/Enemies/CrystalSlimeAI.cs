using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Slime Pha Lê (Hang Pha Lê): a slime grown through with crystal. It hops and lunges like the
    /// forest's moss slime (<see cref="SlimeAI"/>), but its skin turns a hero's shots back at them
    /// (<see cref="ShotReflector"/> on the prefab): blades and blasts work, arrows and bolts come
    /// back. When it falls it bursts into splinters flying out all around.
    /// </summary>
    public class CrystalSlimeAI : SlimeAI
    {
        [Header("Crystal")]
        public int burstShards = 6;
        public float shardDamage = 16f;
        public float shardSpeed = 7.5f;

        protected override void OnDied(DamageInfo d)
        {
            base.OnDied(d);
            if (GameSession.IsAuthority && burstShards > 0)
            {
                // it shatters: splinters in a ring, whoever stands next to it catches one
                float turn = Random.Range(0f, 360f / burstShards);
                Vector2 c = Pos + Vector2.up * 0.25f;
                for (int i = 0; i < burstShards; i++)
                {
                    Vector2 dir = Util.FromAngle(turn + i * 360f / burstShards);
                    EnemyShots.Shard(gameObject, c + dir * 0.3f, c + dir * 6f, shardDamage, shardSpeed, "Vỡ Pha Lê", 0.8f);
                }
                NetCues.Vfx("crystal_shatter", c, 0f, 0.9f);
                NetCues.Sound("sfx_crystal_break", 0.8f, 0.1f, transform.position);
            }
        }
    }
}
