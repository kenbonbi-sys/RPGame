using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Enemies waiting out of sight until something calls them up: the Bùn Con a Người Bùn splits
    /// into, the toads Cóc Tía croaks up from its pond, the leeches of the snake mother's lake.
    /// They are placed with the zone, switched off, so that every machine numbers them the same
    /// (online), and they go back to waiting when they fall (no camp brings them back).
    /// </summary>
    public class Brood : MonoBehaviour
    {
        public List<EnemyBase> members = new List<EnemyBase>();

        /// <summary>How many are still waiting to be called.</summary>
        public int Waiting
        {
            get
            {
                int n = 0;
                foreach (var m in members)
                    if (m != null && !m.gameObject.activeSelf) n++;
                return n;
            }
        }

        /// <summary>How many are out and alive.</summary>
        public int Out
        {
            get
            {
                int n = 0;
                foreach (var m in members)
                    if (m != null && m.gameObject.activeSelf && !m.IsDead) n++;
                return n;
            }
        }

        /// <summary>
        /// Calls up to <paramref name="count"/> waiting members around <paramref name="at"/>, going for
        /// <paramref name="target"/> when there is one. Returns how many came (0 where the rules do not run).
        /// </summary>
        public int Release(Vector2 at, int count, PlayerController target = null, float spread = 0.9f)
        {
            if (!GameSession.IsAuthority) return 0;
            int n = 0;
            foreach (var m in members)
            {
                if (n >= count) break;
                if (m == null || m.gameObject.activeSelf) continue;
                Vector2 spot = at + Util.RandomInCircle(spread);
                m.Revive(spot);
                NetCues.Vfx("water_splash", spot);
                m.Alert(target);
                n++;
            }
            return n;
        }

        /// <summary>Sends every member back to waiting (the fight that called them was reset).</summary>
        public void Dismiss()
        {
            if (!GameSession.IsAuthority) return;
            foreach (var m in members)
            {
                if (m == null || !m.gameObject.activeSelf) continue;
                NetCues.Vfx("water_splash", m.transform.position);
                m.gameObject.SetActive(false);
            }
        }
    }
}
