using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Generic "sprite effect over lifetime": scale, alpha, rotation and colour curves.
    /// Used for shockwave rings, flashes, magic circles, glows...
    /// </summary>
    public class SpriteFX : MonoBehaviour
    {
        public SpriteRenderer sr;
        public float lifetime = 0.5f;
        public AnimationCurve scale = AnimationCurve.EaseInOut(0, 0.2f, 1, 1f);
        public AnimationCurve alpha = AnimationCurve.EaseInOut(0, 1, 1, 0);
        public float scaleMultiplier = 1f;
        public float spinSpeed = 0f;
        public Gradient color;
        public bool useUnscaled;

        float t;
        Vector3 baseScale;
        Color baseColor;

        void Awake()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            if (sr != null) baseColor = sr.color;
        }

        void OnEnable()
        {
            t = 0;
            Apply();
        }

        void Update()
        {
            t += useUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            Apply();
        }

        void Apply()
        {
            float k = lifetime > 0 ? Mathf.Clamp01(t / lifetime) : 1f;
            transform.localScale = baseScale * (scale.Evaluate(k) * scaleMultiplier);
            if (spinSpeed != 0) transform.Rotate(0, 0, spinSpeed * (useUnscaled ? Time.unscaledDeltaTime : Time.deltaTime));
            if (sr != null)
            {
                Color c = color != null && color.colorKeys.Length > 0 ? color.Evaluate(k) * baseColor : baseColor;
                c.a = baseColor.a * alpha.Evaluate(k);
                sr.color = c;
            }
        }
    }
}
