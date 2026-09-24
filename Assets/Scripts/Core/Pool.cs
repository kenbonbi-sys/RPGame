using System.Collections.Generic;
using UnityEngine;

namespace RPG
{

    /// <summary>Very small prefab pool. Spawned objects get OnEnable every reuse.</summary>
    public static class Pool
    {
        static readonly Dictionary<GameObject, Stack<GameObject>> Free = new Dictionary<GameObject, Stack<GameObject>>();
        static Transform _root;

        static Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("[Pool]");
                    _root = go.transform;
                }
                return _root;
            }
        }

        /// <summary>The root existed but was destroyed: its scene is being unloaded.</summary>
        static bool RootLost => !ReferenceEquals(_root, null) && _root == null;

        public static void ClearAll()
        {
            Free.Clear();
            _root = null;
        }

        public static GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            if (prefab == null) return null;
            GameObject go = null;
            if (Free.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0 && go == null) go = stack.Pop();
            }
            if (go == null)
            {
                go = Object.Instantiate(prefab, pos, rot, parent != null ? parent : Root);
                var tag = go.GetComponent<PoolTag>();
                if (tag == null) tag = go.AddComponent<PoolTag>();
                tag.prefab = prefab;
                tag.inPool = false;
                return go;
            }
            var t = go.transform;
            t.SetParent(parent != null ? parent : Root, false);
            t.SetPositionAndRotation(pos, rot);
            t.localScale = prefab.transform.localScale;
            go.GetComponent<PoolTag>().inPool = false;
            go.SetActive(true);
            return go;
        }

        public static void Release(GameObject go)
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
            go.transform.SetParent(Root, false);
            if (!Free.TryGetValue(tag.prefab, out var stack))
            {
                stack = new Stack<GameObject>();
                Free[tag.prefab] = stack;
            }
            stack.Push(go);
        }
    }
}
