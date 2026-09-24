using UnityEngine;

namespace RPG
{
    /// <summary>Marks an instance with the prefab it came from so it can be recycled.</summary>
    public class PoolTag : MonoBehaviour
    {
        public GameObject prefab;
        public bool inPool;
    }
}
