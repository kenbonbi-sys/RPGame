using UnityEngine;

namespace RPG
{
    /// <summary>Static entry point for spawning effects by id (see VFXLibrary).</summary>
    public static class VFX
    {
        static VFXLibrary Lib => GameManager.I != null ? GameManager.I.vfx : null;

        /// <param name="persistent">If true the effect stays until <see cref="Release"/> is called.</param>
        public static GameObject Spawn(string id, Vector3 pos, Quaternion rot = default, float scale = 1f, Transform parent = null, bool persistent = false)
        {
            var lib = Lib;
            if (lib == null || !GameSession.HasScreen) return null;   // a zone server shows nothing
            var prefab = lib.Get(id);
            if (prefab == null)
            {
                Debug.LogWarning($"[VFX] Unknown effect id '{id}'");
                return null;
            }
            if (rot.x == 0 && rot.y == 0 && rot.z == 0 && rot.w == 0) rot = Quaternion.identity;
            var go = Pool.Get(prefab, pos, rot, parent);
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = prefab.transform.localScale * scale;
            var fx = go.GetComponent<PooledFX>();
            if (fx != null) fx.Persistent = persistent;
            return go;
        }

        /// <summary>Gracefully stops an effect (particles finish) and returns it to the pool.</summary>
        public static void Release(GameObject go)
        {
            if (go == null) return;
            var fx = go.GetComponent<PooledFX>();
            if (fx != null && go.activeInHierarchy) fx.StopAndRelease();
            else Pool.Release(go);
        }

        /// <summary>For projectiles: stop collisions/sprites, let trails fade, then release.</summary>
        public static void ReleaseAfterTrails(GameObject go) => Release(go);

        /// <summary>Spawns a ground decal / effect and a point light flash in one call.</summary>
        public static void Burst(string id, Vector3 pos, float shake = 0f, string sfx = null, float sfxVol = 1f)
        {
            Spawn(id, pos, Quaternion.identity);
            if (shake > 0) CameraRig.Shake(shake);
            if (!string.IsNullOrEmpty(sfx)) AudioManager.Play(sfx, sfxVol, 0.06f, pos);
        }
    }
}
