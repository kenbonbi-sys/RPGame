using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPG
{
    /// <summary>
    /// Keeps the Core scene (managers, hero, camera, UI) loaded and swaps zone scenes next to it
    /// (plan §16): fade out, unload the old zone, load the new one additively, hand its spots,
    /// bounds and minimap layers to the Core systems, place the hero, fade in.
    /// At start-up it loads the zone of the save being loaded, else the start zone — or adopts
    /// a zone that is already open (pressing Play on a zone scene in the editor).
    /// </summary>
    [DefaultExecutionOrder(-85)]
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader I { get; private set; }

        [Tooltip("Leave empty to use GameDatabase.startZone.")]
        public ZoneDef startZone;
        [Header("Transition")]
        public CanvasGroup fade;
        public TextMeshProUGUI fadeTitle;
        public float fadeTime = 0.35f;

        /// <summary>True while a zone is being loaded (saves wait for it).</summary>
        public bool Busy { get; private set; } = true;
        public ZoneRoot Zone { get; private set; }

        /// <summary>Raised once a zone is loaded and wired up.</summary>
        public event Action<ZoneRoot> ZoneReady;

        void Awake()
        {
            I = this;
            if (fade != null) UIUtil.SetAlpha(fade, 1f);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        IEnumerator Start()
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            // a zone opened in the editor is already here
            var open = ZoneRoot.Current;
            if (open != null)
            {
                yield return null;
                Adopt(open, null, !SaveManager.HasPendingLoad);
                yield return Fade(0f);
                yield break;
            }
            ZoneDef zone = null;
            if (SaveManager.HasPendingLoad && db != null) zone = db.Zone(SaveManager.PendingZoneId);
            if (zone == null) zone = startZone != null ? startZone : db != null ? db.startZone : null;
            if (zone == null)
            {
                Debug.LogError("[SceneLoader] No start zone.");
                Busy = false;
                yield break;
            }
            yield return Load(zone, null, !SaveManager.HasPendingLoad);
        }

        /// <summary>Goes to another zone (a road, a door, a Đá Truyền Tống).</summary>
        public void Travel(ZoneDef zone, string entry = null)
        {
            if (zone == null || Busy) return;
            StartCoroutine(Load(zone, entry, true));
        }

        IEnumerator Load(ZoneDef zone, string entry, bool placeHero)
        {
            Busy = true;
            if (fadeTitle != null) fadeTitle.text = zone.displayName;
            yield return Fade(1f);
            if (Zone != null && Zone.Scene.isLoaded)
            {
                var old = Zone.Scene;
                Zone = null;
                yield return SceneManager.UnloadSceneAsync(old);
            }
            var op = SceneManager.LoadSceneAsync(zone.sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] Scene '{zone.sceneName}' is not in the build settings.");
                Busy = false;
                yield break;
            }
            yield return op;
            var scene = SceneManager.GetSceneByName(zone.sceneName);
            ZoneRoot root = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                root = go.GetComponentInChildren<ZoneRoot>(true);
                if (root != null) break;
            }
            if (root == null)
            {
                Debug.LogError($"[SceneLoader] Scene '{zone.sceneName}' has no ZoneRoot.");
                Busy = false;
                yield break;
            }
            Adopt(root, entry, placeHero);
            yield return Fade(0f);
        }

        void Adopt(ZoneRoot root, string entry, bool placeHero)
        {
            Zone = root;
            // things spawned while playing (loot, boulders) belong to the zone and leave with it
            SceneManager.SetActiveScene(root.Scene);
            var def = root.def;
            if (CameraRig.I != null) CameraRig.I.worldBounds = root.bounds;
            if (HUD.I != null && HUD.I.minimap != null) HUD.I.minimap.SetWorld(root);
            if (def != null)
            {
                if (!string.IsNullOrEmpty(def.music)) AudioManager.PlayMusic(def.music, 2f);
                if (!string.IsNullOrEmpty(def.ambience)) AudioManager.PlayAmbience(def.ambience);
            }
            var hero = Players.Local;
            if (placeHero && hero != null)
            {
                var at = root.SpotOf(!string.IsNullOrEmpty(entry) ? entry : def != null ? def.defaultEntry : "spawn") ?? root.SpotOf("spawn");
                if (at != null) hero.motor.Teleport(at.position);
            }
            if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            Busy = false;
            ZoneReady?.Invoke(root);
        }

        IEnumerator Fade(float to)
        {
            if (fade == null) yield break;
            fade.blocksRaycasts = to > 0.5f;
            float from = fade.alpha;
            for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                UIUtil.SetAlpha(fade, Mathf.Lerp(from, to, t / fadeTime));
                yield return null;
            }
            UIUtil.SetAlpha(fade, to);
        }
    }
}
