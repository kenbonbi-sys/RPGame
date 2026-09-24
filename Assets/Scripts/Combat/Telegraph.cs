using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Ground warning for enemy attacks: an outline showing the full area plus an
    /// inner fill that grows until the attack lands (circle or cone).
    /// </summary>
    public class Telegraph : MonoBehaviour
    {
        public SpriteRenderer ring;
        public SpriteRenderer fill;
        public SpriteRenderer coneOutline;
        public SpriteRenderer coneFill;
        public Sprite ringSprite, fillSprite, coneSprite;

        float duration, t;
        float radius;
        bool cone;
        Color color;
        bool active;
        float fadeOut;

        /// <summary>Circle warning of the given world radius that lasts duration seconds.</summary>
        public static Telegraph Circle(Vector2 pos, float radius, float duration, Color? color = null)
        {
            var tg = Spawn(pos);
            if (tg == null) return null;
            tg.Setup(false, radius, duration, color ?? Palette.Telegraph, 0);
            return tg;
        }

        /// <summary>Cone warning (90°) pointing along dir.</summary>
        public static Telegraph Cone(Vector2 pos, Vector2 dir, float radius, float duration, Color? color = null)
        {
            var tg = Spawn(pos);
            if (tg == null) return null;
            tg.Setup(true, radius, duration, color ?? Palette.Telegraph, Util.Angle(dir) - 90f);
            return tg;
        }

        static Telegraph Spawn(Vector2 pos)
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.telegraphPrefab == null) return null;
            var go = Pool.Get(db.telegraphPrefab, pos, Quaternion.identity);
            return go.GetComponent<Telegraph>();
        }

        void Setup(bool isCone, float r, float dur, Color c, float angle)
        {
            cone = isCone;
            radius = r;
            duration = Mathf.Max(0.05f, dur);
            color = c;
            t = 0;
            fadeOut = 0;
            active = true;
            ring.gameObject.SetActive(!cone);
            fill.gameObject.SetActive(!cone);
            coneOutline.gameObject.SetActive(cone);
            coneFill.gameObject.SetActive(cone);
            transform.rotation = Quaternion.Euler(0, 0, cone ? angle : 0);
            // sprites are 2 units across (128px @ 64ppu) for circles, cone is radius 1 unit
            float s = cone ? r : r;
            ring.transform.localScale = Vector3.one * s;
            coneOutline.transform.localScale = Vector3.one * s;
            Apply();
        }

        /// <summary>Ends the warning early (e.g. the boss got stunned).</summary>
        public void Cancel()
        {
            if (!active) return;
            active = false;
            fadeOut = 0.2f;
        }

        void Update()
        {
            if (active)
            {
                t += Time.deltaTime;
                if (t >= duration)
                {
                    active = false;
                    fadeOut = 0.18f;
                }
                Apply();
            }
            else
            {
                fadeOut -= Time.deltaTime;
                float a = Mathf.Clamp01(fadeOut / 0.18f);
                SetAlpha(a * 1.2f, a);
                if (fadeOut <= 0) Pool.Release(gameObject);
            }
        }

        void Apply()
        {
            float k = Mathf.Clamp01(t / duration);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 18f);
            float s = Mathf.Lerp(0.05f, 1f, Util.EaseInOut(k)) * radius;
            fill.transform.localScale = Vector3.one * s;
            coneFill.transform.localScale = Vector3.one * s;
            SetAlpha(0.9f * pulse, 0.55f + 0.35f * k);
        }

        void SetAlpha(float outlineA, float fillA)
        {
            ring.color = color.WithAlpha(outlineA);
            coneOutline.color = color.WithAlpha(outlineA * 0.5f);
            fill.color = color.WithAlpha(fillA * 0.55f);
            coneFill.color = color.WithAlpha(fillA * 0.6f);
        }
    }
}
