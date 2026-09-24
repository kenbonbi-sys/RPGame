using UnityEngine;

namespace RPG
{
    public static class UIUtil
    {
        /// <summary>Converts a world position to a position inside <paramref name="layer"/> (overlay canvas).</summary>
        public static bool WorldToLayer(Vector3 world, RectTransform layer, out Vector2 local)
        {
            local = Vector2.zero;
            var cam = CameraRig.MainCam;
            if (cam == null || layer == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return false;
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, sp, null, out local);
            return ok && sp.x > -100 && sp.x < Screen.width + 100 && sp.y > -100 && sp.y < Screen.height + 100;
        }

        public static void SetAlpha(CanvasGroup g, float a)
        {
            if (g == null) return;
            g.alpha = a;
            bool on = a > 0.01f;
            g.blocksRaycasts = on && g.interactable;
        }
    }
}
