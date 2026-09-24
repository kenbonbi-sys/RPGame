using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>Keeps a camp of enemies alive; respawns them while the player is away.</summary>
    public class EnemySpawner : MonoBehaviour
    {
        public GameObject prefab;
        public int count = 3;
        public float radius = 2.5f;
        public float respawnDelay = 25f;
        public float minPlayerDistance = 11f;

        readonly List<EnemyBase> alive = new List<EnemyBase>();
        readonly List<(EnemyBase e, float at)> dead = new List<(EnemyBase, float)>();

        void Start()
        {
            // enemies placed as children by the scene builder are adopted first
            foreach (var e in GetComponentsInChildren<EnemyBase>(true))
            {
                e.spawner = this;
                alive.Add(e);
            }
            for (int i = alive.Count; i < count && prefab != null; i++)
            {
                var go = Instantiate(prefab, (Vector2)transform.position + Util.RandomInCircle(radius), Quaternion.identity, transform);
                var e = go.GetComponent<EnemyBase>();
                e.spawner = this;
                alive.Add(e);
            }
        }

        public void NotifyDead(EnemyBase e)
        {
            alive.Remove(e);
            dead.Add((e, Time.time + respawnDelay));
        }

        void Update()
        {
            if (dead.Count == 0) return;
            var p = GameManager.I != null ? GameManager.I.player : null;
            for (int i = dead.Count - 1; i >= 0; i--)
            {
                if (Time.time < dead[i].at) continue;
                if (p != null && Vector2.Distance(p.transform.position, transform.position) < minPlayerDistance) continue;
                var e = dead[i].e;
                dead.RemoveAt(i);
                if (e == null) continue;
                e.Revive((Vector2)transform.position + Util.RandomInCircle(radius));
                alive.Add(e);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
