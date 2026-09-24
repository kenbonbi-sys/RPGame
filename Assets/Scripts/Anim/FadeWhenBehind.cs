using UnityEngine;

namespace RPG
{
    /// <summary>Makes a tall prop (tree, house) see-through while the player walks behind it.</summary>
    public class FadeWhenBehind : MonoBehaviour
    {
        public SpriteRenderer sr;
        public Vector2 areaSize = new Vector2(2f, 2.6f);
        public Vector2 areaOffset = new Vector2(0f, 1.6f);
        public float fadedAlpha = 0.45f;
        float alpha = 1f;

        void Awake()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            var p = GameManager.I != null && GameManager.I.player != null ? GameManager.I.player.transform.position : Vector3.one * 9999f;
            Vector2 c = (Vector2)transform.position + areaOffset;
            bool behind = Mathf.Abs(p.x - c.x) < areaSize.x * 0.5f && Mathf.Abs(p.y - c.y) < areaSize.y * 0.5f && p.y > transform.position.y;
            float goal = behind ? fadedAlpha : 1f;
            alpha = Mathf.MoveTowards(alpha, goal, Time.deltaTime * 4f);
            if (sr != null)
            {
                var col = sr.color;
                col.a = alpha;
                sr.color = col;
            }
        }
    }
}
