using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Follows the player with a little mouse look-ahead, keeps an integer pixel zoom
    /// (crisp pixel art at any resolution) and provides trauma-based screen shake.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }
        static Camera _main;

        public static Camera MainCam
        {
            get
            {
                if (_main == null) _main = Camera.main;
                return _main;
            }
        }

        public Transform target;
        public float smoothTime = 0.12f;
        public float lookAhead = 0.12f;
        public float maxLookAhead = 1.6f;
        [Tooltip("Pixels per unit of the art.")]
        public int ppu = 16;
        [Tooltip("Reference vertical resolution in art pixels (270 = 16.9 tiles tall).")]
        public float referenceHeight = 270f;
        public Rect worldBounds = new Rect(0, 0, 100, 64);

        [Header("Shake")]
        public float maxShakeOffset = 0.6f;
        public float maxShakeAngle = 1.5f;
        public float traumaDecay = 1.6f;

        Transform focus;
        float focusWeight, focusWeightNow;
        Camera cam;
        Vector3 vel;
        Vector3 basePos;
        float trauma;
        float zoomMul = 1f, zoomMulTarget = 1f;
        float seed;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
            _main = cam;
            seed = Random.value * 100f;
            basePos = transform.position;
        }

        public static void Shake(float amount)
        {
            if (I == null) return;
            I.trauma = Mathf.Clamp01(I.trauma + amount);
        }

        /// <summary>Pulls the camera toward a second point of interest (e.g. the boss). null clears.</summary>
        public static void SetFocus(Transform t, float weight = 0.35f)
        {
            if (I == null) return;
            I.focus = t;
            I.focusWeight = t != null ? weight : 0f;
        }

        /// <summary>1 = normal, &lt;1 zooms out (shows more of the world).</summary>
        public static void SetZoom(float mul)
        {
            if (I != null) I.zoomMulTarget = mul;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            basePos = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = basePos;
        }

        int PixelScale => Mathf.Max(1, Mathf.RoundToInt(Screen.height / referenceHeight));

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            zoomMul = Mathf.MoveTowards(zoomMul, zoomMulTarget, dt * 0.6f);
            float scale = PixelScale * zoomMul;
            cam.orthographicSize = Screen.height / (2f * ppu * scale);

            if (target != null)
            {
                Vector3 goal = target.position;
                focusWeightNow = Mathf.MoveTowards(focusWeightNow, focus != null ? focusWeight : 0f, dt * 0.8f);
                if (focus != null && focusWeightNow > 0) goal = Vector3.Lerp(goal, focus.position + Vector3.up * 1.5f, focusWeightNow);
                Vector2 mouse = InputReader.MouseWorld;
                Vector2 ahead = Vector2.ClampMagnitude((mouse - (Vector2)target.position) * lookAhead, maxLookAhead);
                goal += (Vector3)ahead;
                goal.z = transform.position.z;
                basePos = Vector3.SmoothDamp(basePos, goal, ref vel, smoothTime, Mathf.Infinity, dt);
            }

            // clamp to world bounds
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            if (worldBounds.width > halfW * 2) basePos.x = Mathf.Clamp(basePos.x, worldBounds.xMin + halfW, worldBounds.xMax - halfW);
            if (worldBounds.height > halfH * 2) basePos.y = Mathf.Clamp(basePos.y, worldBounds.yMin + halfH, worldBounds.yMax - halfH);

            // shake
            trauma = Mathf.Max(0, trauma - traumaDecay * dt);
            float s = trauma * trauma;
            float tt = Time.unscaledTime * 22f;
            Vector3 off = new Vector3((Mathf.PerlinNoise(seed, tt) - 0.5f) * 2f, (Mathf.PerlinNoise(seed + 7f, tt) - 0.5f) * 2f, 0) * (maxShakeOffset * s);
            float ang = (Mathf.PerlinNoise(seed + 13f, tt) - 0.5f) * 2f * maxShakeAngle * s;

            // snap to the screen pixel grid to avoid pixel-art shimmer
            float unitsPerScreenPixel = 1f / (ppu * scale);
            Vector3 p = basePos + off;
            if (s < 0.001f)
            {
                p.x = Mathf.Round(p.x / unitsPerScreenPixel) * unitsPerScreenPixel;
                p.y = Mathf.Round(p.y / unitsPerScreenPixel) * unitsPerScreenPixel;
            }
            transform.position = p;
            transform.rotation = Quaternion.Euler(0, 0, ang);
        }
    }
}
