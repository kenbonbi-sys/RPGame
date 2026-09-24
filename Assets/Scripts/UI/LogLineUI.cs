using TMPro;
using UnityEngine;

namespace RPG
{
    public class LogLineUI : MonoBehaviour
    {
        public TextMeshProUGUI text;
        public CanvasGroup group;
        public float lifetime = 9f;
        [HideInInspector] public string kind;
        [HideInInspector] public int amount;

        float born;

        public bool Expired => Time.unscaledTime - born > lifetime;

        public void Set(string msg, Color c)
        {
            born = Time.unscaledTime;
            if (text != null)
            {
                text.text = "• " + msg;
                text.color = c;
            }
        }

        void Update()
        {
            float age = Time.unscaledTime - born;
            float a = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((lifetime - age) / 1.2f);
            if (group != null) group.alpha = a;
        }
    }
}
