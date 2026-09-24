using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Automated check of online play (online phase 1): start a host and a client of the built
    /// game on one machine,
    ///   RungThiTham.exe -host -netsmoke [-netsmokeDir "C:\shots"]
    ///   RungThiTham.exe -client 127.0.0.1 -netsmoke -batchmode -nographics
    /// (the client runs without a window: with two game windows on one PC, Unity can crash
    /// while closing the second one; see Docs/KeHoach-Online.md, known issues).
    /// Each waits until both heroes are in the world, walks its own hero right and left while it
    /// watches the other one, takes a screenshot and quits. Exit code: 0 = it saw the other hero
    /// walk, 1 = it did not or errors were logged, 2 = nobody showed up in time.
    /// Does nothing in normal play.
    /// </summary>
    public class NetSmoke : MonoBehaviour
    {
        public static bool Active => Array.IndexOf(Environment.GetCommandLineArgs(), "-netsmoke") >= 0;

        /// <summary>How far the other hero must be seen walking for the run to pass.</summary>
        const float MinSeenWalk = 2f;

        int errors;
        string firstError;

        void Start()
        {
            Application.logMessageReceived += CountErrors;
            StartCoroutine(Run());
        }

        void OnDestroy() => Application.logMessageReceived -= CountErrors;

        void CountErrors(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            if (firstError == null) firstError = message;
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static PlayerController Other(PlayerController me)
        {
            foreach (var p in Players.All)
                if (p != null && p != me) return p;
            return null;
        }

        IEnumerator Run()
        {
            string role = GameSession.Mode.ToString().ToLowerInvariant();
            float deadline = Time.realtimeSinceStartup + 90f;
            while (Players.Local == null || Other(Players.Local) == null)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogError($"[NetSmoke] {role}: the other hero never showed up ({Players.All.Count} heroes)");
                    Application.Quit(2);
                    yield break;
                }
                yield return null;
            }
            var me = Players.Local;
            var other = Other(me);
            Debug.Log($"[NetSmoke] {role}: {Players.All.Count} heroes, walking");
            yield return Wait(1f);

            me.Autopilot = true;
            float seen = 0f;
            bool sawWalkClip = false;
            Vector2 last = other.transform.position;
            Vector2 start = me.transform.position;
            float[] legs = { 1f, -1f, 1f, -1f };
            foreach (float dir in legs)
            {
                me.SetIntent(new PlayerIntent { move = new Vector2(dir, 0f) });
                float end = Time.realtimeSinceStartup + 1.1f;
                while (Time.realtimeSinceStartup < end)
                {
                    if (other == null) break;
                    Vector2 now = other.transform.position;
                    seen += Vector2.Distance(now, last);
                    last = now;
                    if (other.anim != null && other.anim.Current != null && other.anim.Current.StartsWith("walk")) sawWalkClip = true;
                    yield return null;
                }
            }
            me.SetIntent(new PlayerIntent());
            float walked = Vector2.Distance(start, me.transform.position);
            yield return Shot(role);

            bool ok = other != null && seen >= MinSeenWalk && errors == 0;
            Debug.Log($"[NetSmoke] {role}: saw the other hero walk {seen:0.0} units (walk clip: {sawWalkClip}), " +
                      $"mine ended {walked:0.0} from its start, {errors} errors{(firstError != null ? "; first: " + firstError : "")} → {(ok ? "ok" : "FAILED")}");
            // the host stays a little longer so the client finishes watching before it goes
            yield return Wait(GameSession.Mode == SessionMode.Client ? 0.5f : 4f);
            Application.Quit(ok ? 0 : 1);
        }

        static IEnumerator Shot(string role)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-netsmokeDir");
            string dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "netsmoke");
            Directory.CreateDirectory(dir);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(dir, $"netsmoke_{role}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[NetSmoke] " + path);
            yield return null;
        }
    }
}
