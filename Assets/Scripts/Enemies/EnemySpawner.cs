using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>When a camp is out (plan §10: the swamp has other creatures by day than by night).</summary>
    public enum DayPart { Always, Day, Night }

    /// <summary>
    /// Keeps a camp of enemies alive; respawns them while no hero is near. A camp that only comes
    /// out by day or by night (<see cref="activeAt"/>) sends its creatures away at the turn of the
    /// day (those in a fight finish it first) and brings them back, one by one, when its time comes.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public GameObject prefab;
        public int count = 3;
        public float radius = 2.5f;
        public float respawnDelay = 25f;
        public float minPlayerDistance = 11f;
        [Tooltip("Out all the time, only by day, or only at night.")]
        public DayPart activeAt = DayPart.Always;

        readonly List<EnemyBase> alive = new List<EnemyBase>();
        readonly List<(EnemyBase e, float at)> dead = new List<(EnemyBase, float)>();
        /// <summary>Sent away until the camp's time comes again, each with when it comes back.</summary>
        readonly List<(EnemyBase e, float at)> resting = new List<(EnemyBase, float)>();
        float nextSeasonCheck;

        /// <summary>Whether this is the camp's time of day (always, for most camps).</summary>
        public bool InSeason => IsSeason(activeAt, DayNightCycle.IsNight);

        public static bool IsSeason(DayPart part, bool night) => part == DayPart.Always || (part == DayPart.Night) == night;

        /// <summary>Its creatures that are out right now.</summary>
        public int Alive => alive.Count;

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
            // out of season from the start: gone before anyone sees them
            if (GameSession.IsAuthority && !InSeason) SendAway(true);
        }

        public void NotifyDead(EnemyBase e)
        {
            alive.Remove(e);
            dead.Add((e, Time.time + respawnDelay));
        }

        void Update()
        {
            if (!GameSession.IsAuthority) return;   // online the server brings camps back
            if (activeAt != DayPart.Always && Time.time >= nextSeasonCheck)
            {
                nextSeasonCheck = Time.time + 1f;
                if (InSeason) CallBack();
                else SendAway(false);
            }
            if (dead.Count == 0) return;
            bool watched = false;   // a hero nearby, even one lying dead, sees the camp
            foreach (var p in Players.All)
                if (p != null && Vector2.Distance(p.transform.position, transform.position) < minPlayerDistance) watched = true;
            bool season = InSeason;
            for (int i = dead.Count - 1; i >= 0; i--)
            {
                if (Time.time < dead[i].at) continue;
                var e = dead[i].e;
                if (!season)
                {
                    // its time is over: it waits with the others for the next one
                    dead.RemoveAt(i);
                    if (e != null) resting.Add((e, 0f));
                    continue;
                }
                if (watched) continue;
                dead.RemoveAt(i);
                if (e == null) continue;
                e.Revive((Vector2)transform.position + Util.RandomInCircle(radius));
                alive.Add(e);
            }
        }

        /// <summary>A hero close enough to see a creature come or go.</summary>
        const float SeenRadius = 13f;

        /// <summary>The camp's time is over: whoever is not fighting leaves (at once and unseen when <paramref name="quietly"/>).</summary>
        void SendAway(bool quietly)
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var e = alive[i];
                if (e == null)
                {
                    alive.RemoveAt(i);
                    continue;
                }
                if (e.IsDead) continue;   // the fallen go through NotifyDead
                if (!quietly)
                {
                    if (e.Busy) continue;   // a fight ends first
                    if (!e.FadesInView && Players.AnyWithin(e.transform.position, SeenRadius)) continue;   // it slips away unseen
                }
                e.Retire(quietly);
                alive.RemoveAt(i);
                resting.Add((e, 0f));
            }
        }

        /// <summary>The camp's time has come: its creatures come back one by one over a few seconds.</summary>
        void CallBack()
        {
            for (int i = resting.Count - 1; i >= 0; i--)
            {
                var (e, at) = resting[i];
                if (e == null)
                {
                    resting.RemoveAt(i);
                    continue;
                }
                if (at <= 0f)
                {
                    resting[i] = (e, Time.time + Random.Range(0.2f, 6f));
                    continue;
                }
                if (Time.time < at) continue;
                Vector2 spot = (Vector2)transform.position + Util.RandomInCircle(radius);
                if (!e.FadesInView && Players.AnyWithin(spot, SeenRadius)) continue;   // it arrives unseen
                resting.RemoveAt(i);
                e.Revive(spot);
                e.Arrive();
                alive.Add(e);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = activeAt == DayPart.Night ? new Color(0.4f, 0.6f, 1f, 0.5f) : activeAt == DayPart.Day ? new Color(1f, 0.9f, 0.3f, 0.5f) : new Color(1, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
