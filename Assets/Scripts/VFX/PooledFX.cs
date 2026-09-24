using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG
{
    /// <summary>
    /// Root component of every VFX prefab. Restarts particles / animations when reused
    /// from the pool and returns itself to the pool after <see cref="lifetime"/>.
    /// </summary>
    public class PooledFX : MonoBehaviour
    {
        [Tooltip("Seconds before auto-release. Ignored when spawned as persistent.")]
        public float lifetime = 1.5f;
        [Tooltip("Seconds to wait for particles to fade after Stop().")]
        public float stopLinger = 1f;

        public bool Persistent { get; set; }

        ParticleSystem[] systems;
        SpriteRenderer[] sprites;
        Light2D[] lights;
        float spawnTime;
        bool stopping;

        void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            lights = GetComponentsInChildren<Light2D>(true);
        }

        void OnEnable()
        {
            spawnTime = Time.time;
            stopping = false;
            if (systems == null) Awake();
            foreach (var ps in systems)
            {
                if (ps == null) continue;
                ps.Clear(true);
                ps.Play(true);
            }
            foreach (var sr in sprites)
                if (sr != null) sr.enabled = true;
            foreach (var l in lights)
                if (l != null) l.enabled = true;
        }

        void Update()
        {
            if (stopping || Persistent) return;
            if (lifetime > 0 && Time.time - spawnTime >= lifetime) Pool.Release(gameObject);
        }

        /// <summary>Stops emitting, hides sprites/lights, then releases once particles died.</summary>
        public void StopAndRelease()
        {
            if (stopping || !gameObject.activeInHierarchy) return;
            stopping = true;
            foreach (var ps in systems)
                if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var sr in sprites)
                if (sr != null && sr.GetComponent<ParticleSystem>() == null) sr.enabled = false;
            foreach (var l in lights)
                if (l != null) l.enabled = false;
            StartCoroutine(ReleaseLater());
        }

        IEnumerator ReleaseLater()
        {
            yield return new WaitForSeconds(stopLinger);
            transform.SetParent(null);
            Pool.Release(gameObject);
        }
    }
}
