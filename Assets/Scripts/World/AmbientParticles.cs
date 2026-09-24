using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Keeps ambient particle systems (fireflies, falling leaves, pollen, low mist) around the
    /// camera. In a misty region (<see cref="DayNightCycle.Mist"/>) the leaves stop and the mist
    /// rolls in; the mist is made here when the scene has none (<see cref="GameDatabase.mistMaterial"/>).
    /// Under the rock (<see cref="DayNightCycle.Underground"/>) only a little dust drifts.
    /// </summary>
    public class AmbientParticles : MonoBehaviour
    {
        public ParticleSystem fireflies;
        public ParticleSystem leaves;
        public ParticleSystem motes;
        public ParticleSystem mist;
        public float fireflyDay = 3f, fireflyNight = 22f;
        public float leavesRate = 2.5f;
        public float motesDay = 6f, motesNight = 2f;
        public float mistRate = 5f;

        void Start()
        {
            if (mist == null && GameSession.HasScreen) mist = MakeMist();
        }

        void LateUpdate()
        {
            var cam = CameraRig.MainCam;
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            p.z = 0;
            transform.position = p;
            float n = DayNightCycle.NightFactor;
            float m = DayNightCycle.Mist;
            float u = DayNightCycle.Underground;   // under the rock: no fireflies, no leaves, a little dust
            SetRate(fireflies, Mathf.Lerp(fireflyDay, fireflyNight, n) * (1f + m) * (1f - u));
            SetRate(leaves, leavesRate * (1f - m) * (1f - u));
            SetRate(motes, Mathf.Lerp(Mathf.Lerp(motesDay, motesNight, n), motesNight, u));
            SetRate(mist, mistRate * m);
        }

        /// <summary>Large soft puffs drifting slowly across the view.</summary>
        ParticleSystem MakeMist()
        {
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (db == null || db.mistMaterial == null) return null;
            var go = new GameObject("Mist");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.86f, 0.8f, 0.16f), new Color(0.68f, 0.78f, 0.74f, 0.26f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 26f, 1f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.08f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = db.mistMaterial;
            r.sortingLayerName = SortingLayerNames.Top;
            r.sortingOrder = -5;
            ps.Play();
            return ps;
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var e = ps.emission;
            e.rateOverTime = rate;
        }
    }
}
