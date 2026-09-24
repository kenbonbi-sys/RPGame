using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>Leaves fading ghost copies of a sprite behind (dash, boss leap).</summary>
    public class AfterImageSpawner : MonoBehaviour
    {
        public SpriteRenderer source;
        public Color color = new Color(0.55f, 0.85f, 1f, 0.7f);
        public float interval = 0.035f;
        public float fadeTime = 0.3f;

        float until;
        float next;

        public void Emit(float seconds, Color? c = null)
        {
            until = Time.time + seconds;
            next = 0;
            if (c.HasValue) color = c.Value;
        }

        void Update()
        {
            if (Time.time > until || source == null) return;
            if (Time.time < next) return;
            next = Time.time + interval;
            SpawnGhost();
        }

        void SpawnGhost()
        {
            var go = new GameObject("Ghost");
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = source.sprite;
            sr.flipX = source.flipX;
            sr.sortingLayerName = SortingLayerNames.Default;
            sr.sortingOrder = -1;
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db != null && db.silhouette != null) sr.sharedMaterial = db.silhouette;
            sr.color = color;
            StartCoroutine(Fade(sr, go));
        }

        IEnumerator Fade(SpriteRenderer sr, GameObject go)
        {
            float t = 0;
            Color c0 = sr.color;
            while (t < fadeTime && sr != null)
            {
                t += Time.deltaTime;
                sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1 - t / fadeTime));
                yield return null;
            }
            if (go != null) Destroy(go);
        }
    }
}
