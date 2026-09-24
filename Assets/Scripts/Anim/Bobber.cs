using UnityEngine;

namespace RPG
{
    /// <summary>Gentle vertical bob + optional pulse (quest markers, pickups).</summary>
    public class Bobber : MonoBehaviour
    {
        public float amplitude = 0.08f;
        public float speed = 3f;
        public float pulse = 0f;
        public bool pixelSnap = true;
        Vector3 basePos;
        Vector3 baseScale;
        float phase;

        void Awake()
        {
            basePos = transform.localPosition;
            baseScale = transform.localScale;
            phase = Random.value * 6f;
        }

        void Update()
        {
            float s = Mathf.Sin(Time.time * speed + phase);
            float y = s * amplitude;
            if (pixelSnap) y = Mathf.Round(y * 16f) / 16f;
            transform.localPosition = basePos + new Vector3(0, y, 0);
            if (pulse > 0) transform.localScale = baseScale * (1f + s * pulse);
        }
    }
}
