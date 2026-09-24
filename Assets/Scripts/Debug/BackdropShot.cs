using System;
using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Takes the picture behind the title screen from the game itself:
    ///   RungThiTham.exe -backdropshot "C:\…\title_backdrop.png" -screen-width 1920 -screen-height 1080
    /// Plays offline, hides the HUD and the hero, sets dusk over Làng Lá Xanh (campfire and lanterns
    /// lit), waits for the scene to settle, saves the picture and quits. Copy it to
    /// Assets/Art/UI/title_backdrop.png and rebuild the title scene (Tools/RPG/Steps/8).
    /// Does nothing in normal play.
    /// </summary>
    public class BackdropShot : MonoBehaviour
    {
        public static bool Active => Array.IndexOf(Environment.GetCommandLineArgs(), "-backdropshot") >= 0;

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-backdropshot");
            string path = i >= 0 && i + 1 < args.Length ? args[i + 1] : "title_backdrop.png";
            while (SceneLoader.I == null || SceneLoader.I.Busy) yield return null;
            yield return null;
            var hud = HUD.I;
            if (hud != null) hud.gameObject.SetActive(false);
            var me = Players.Local;
            var gm = GameManager.I;
            var chief = gm != null ? gm.Spot("chief") : null;
            Vector2 look = chief != null ? (Vector2)chief.position + new Vector2(-1.5f, -3f) : new Vector2(20f, 18f);
            if (me != null)
            {
                me.motor.Teleport(look);
                foreach (var r in me.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            if (DayNightCycle.I != null)
            {
                DayNightCycle.I.dayLength = 0f;
                DayNightCycle.I.time = 0.745f;   // dusk: warm sky, lights coming on
            }
            if (CameraRig.I != null) CameraRig.I.SnapToTarget();
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[BackdropShot] " + path);
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit(0);
        }
    }
}
