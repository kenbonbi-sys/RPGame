using UnityEngine;

namespace RPG
{
    /// <summary>Constant rotation (magic circles, blade storm, loot glows).</summary>
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 90f;
        public bool unscaled;

        void Update()
        {
            transform.Rotate(0, 0, degreesPerSecond * (unscaled ? Time.unscaledDeltaTime : Time.deltaTime));
        }
    }
}
