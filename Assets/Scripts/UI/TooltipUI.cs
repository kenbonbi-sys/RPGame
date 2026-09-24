using TMPro;
using UnityEngine;

namespace RPG
{
    public class TooltipUI : MonoBehaviour
    {
        public RectTransform rect;
        public CanvasGroup group;
        public TextMeshProUGUI title;
        public TextMeshProUGUI body;
        public Vector2 offset = new Vector2(24, 24);

        bool showing;
        RectTransform canvasRect;

        void Awake()
        {
            if (rect == null) rect = (RectTransform)transform;
            UIUtil.SetAlpha(group, 0);
            var canvas = GetComponentInParent<Canvas>();
            canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
        }

        public void Show(string t, string b, Color titleColor)
        {
            if (title != null)
            {
                title.text = t;
                title.color = titleColor;
            }
            if (body != null) body.text = b;
            showing = true;
            Follow();
        }

        public void Hide() => showing = false;

        void Update()
        {
            float a = Mathf.MoveTowards(group != null ? group.alpha : 0, showing ? 1 : 0, Time.unscaledDeltaTime * 10f);
            UIUtil.SetAlpha(group, a);
            if (showing) Follow();
        }

        void Follow()
        {
            if (canvasRect == null || rect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, InputReader.MouseScreen, null, out Vector2 local);
            // flip to stay on screen
            var size = rect.rect.size;
            var cr = canvasRect.rect;
            Vector2 p = local + new Vector2(offset.x, offset.y);
            float pivotX = p.x + size.x > cr.xMax ? 1 : 0;
            float pivotY = p.y + size.y > cr.yMax ? 1 : 0;
            rect.pivot = new Vector2(pivotX, pivotY);
            if (pivotX > 0) p.x = local.x - offset.x;
            if (pivotY > 0) p.y = local.y - offset.y;
            rect.anchoredPosition = p;
        }
    }
}
