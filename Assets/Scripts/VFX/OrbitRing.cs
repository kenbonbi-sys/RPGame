using UnityEngine;

namespace RPG
{
    /// <summary>Moves all children around an ellipse (stun stars, orbiting blades).</summary>
    public class OrbitRing : MonoBehaviour
    {
        public float radiusX = 0.4f;
        public float radiusY = 0.16f;
        public float degreesPerSecond = 220f;
        public bool rotateChildren;
        public float childSpin;
        [Tooltip("Children behind the centre get pushed back in sort order.")]
        public bool depthSort = true;

        float angle;

        void OnEnable() => angle = Random.value * 360f;

        void Update()
        {
            angle += degreesPerSecond * Time.deltaTime;
            int n = transform.childCount;
            for (int i = 0; i < n; i++)
            {
                var c = transform.GetChild(i);
                float a = (angle + i * 360f / n) * Mathf.Deg2Rad;
                c.localPosition = new Vector3(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusY, 0);
                if (rotateChildren) c.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90f);
                else if (childSpin != 0) c.Rotate(0, 0, childSpin * Time.deltaTime);
                if (depthSort)
                {
                    var sr = c.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sortingOrder = Mathf.Sin(a) > 0 ? -2 : 2;
                }
            }
        }
    }
}
