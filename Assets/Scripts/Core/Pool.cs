using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPG
{
    /// <summary>
    /// The one object pool of the game (plan T12): VFX, projectiles, damage numbers, nameplates,
    /// skill banners and log lines all come from here, so busy fights do not allocate.
    /// Spawned objects get OnEnable on every reuse. Keyed by the prefab (or UI template) they came from.
    /// </summary>
    public static class Pool
    {
        static readonly Dictionary<GameObject, Stack<GameObject>> Free = new Dictionary<GameObject, Stack<GameObject>>();
        static Transform _root;
        static Scene home;

        /// <summary>The scene that keeps the pool root (Core), so zone changes do not destroy pooled objects.</summary>
        public static void SetHome(Scene scene) => home = scene;

        static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("[Pool]");
                    if (home.IsValid() && home.isLoaded) SceneManager.MoveGameObjectToScene(go, home);
                    _root = go.transform;
                }
                return _root;
            }
        }

        /// <summary>The root existed but was destroyed: its scene is being unloaded.</summary>
        static bool RootLost => !ReferenceEquals(_root, null) && _root == null;

        /// <summary>Objects waiting for reuse (debug console).</summary>
        public static int FreeCount
        {
            get
            {
                int n = 0;
                foreach (var s in Free.Values) n += s.Count;
                return n;
            }
        }

        public static void ClearAll()
        {
            Free.Clear();
            _root = null;
        }

        static GameObject Pop(GameObject prefab)
        {
            if (!Free.TryGetValue(prefab, out var stack)) return null;
            GameObject go = null;
            while (stack.Count > 0 && go == null) go = stack.Pop();
            return go;
        }

        static GameObject Create(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
        {
            var go = Object.Instantiate(prefab, pos, rot, parent);
            var tag = go.GetComponent<PoolTag>();
            if (tag == null) tag = go.AddComponent<PoolTag>();
            tag.prefab = prefab;
            tag.inPool = false;
            return go;
        }

        /// <summary>World objects: placed at <paramref name="pos"/>, under <paramref name="parent"/> or the pool root.</summary>
        public static GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            if (prefab == null) return null;
            var go = Pop(prefab);
            if (go == null) return Create(prefab, pos, rot, parent != null ? parent : Root);
            var t = go.transform;
            t.SetParent(parent != null ? parent : Root, false);
            t.SetPositionAndRotation(pos, rot);
            t.localScale = prefab.transform.localScale;
            go.GetComponent<PoolTag>().inPool = false;
            go.SetActive(true);
            return go;
        }

        /// <summary>
        /// UI and other self-positioning objects: taken under <paramref name="parent"/> with the
        /// template's local layout and switched on (UI templates are usually inactive).
        /// </summary>
        public static T Get<T>(T prefab, Transform parent) where T : Component
        {
            if (prefab == null) return null;
            var src = prefab.gameObject;
            var go = Pop(src);
            if (go == null) go = Create(src, src.transform.position, src.transform.rotation, parent);
            else
            {
                go.transform.SetParent(parent, false);
                go.GetComponent<PoolTag>().inPool = false;
            }
            var t = go.transform;
            t.localRotation = src.transform.localRotation;
            t.localScale = src.transform.localScale;
            if (t is RectTransform rt && src.transform is RectTransform srt) rt.anchoredPosition = srt.anchoredPosition;
            else t.localPosition = src.transform.localPosition;
            t.SetAsLastSibling();
            go.SetActive(true);
            return go.GetComponent<T>();
        }

        /// <summary>
        /// Returns an object to its pool. <paramref name="keepParent"/> leaves it (inactive) where it
        /// is, which avoids moving UI in and out of its canvas.
        /// </summary>
        public static void Release(GameObject go, bool keepParent = false)
        {
            if (go == null) return;
            var tag = go.GetComponent<PoolTag>();
            // releasing while the scene unloads (OnDisable of a dying object) must not create a new root
            if (tag == null || tag.prefab == null || RootLost)
            {
                Object.Destroy(go);
                return;
            }
            if (tag.inPool) return;
            tag.inPool = true;
            go.SetActive(false);
            // a parent switching off (a burning slime falling) cannot give up its children yet:
            // the object waits under it, switched off, and moves on its next use
            var parent = go.transform.parent;
            bool parentLeaving = parent != null && !parent.gameObject.activeInHierarchy;
            if (!keepParent && !parentLeaving) go.transform.SetParent(Root, false);
            if (!Free.TryGetValue(tag.prefab, out var stack))
            {
                stack = new Stack<GameObject>();
                Free[tag.prefab] = stack;
            }
            stack.Push(go);
        }

        /// <summary>Creates <paramref name="count"/> inactive copies up front so the first uses do not allocate.</summary>
        public static void Prewarm(GameObject prefab, int count, Transform parent = null)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = Create(prefab, prefab.transform.position, prefab.transform.rotation, parent != null ? parent : Root);
                Release(go, parent != null);
            }
        }
    }
}
