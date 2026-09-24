using UnityEngine;

namespace RPG
{
    /// <summary>Keeps ambient particle systems (fireflies, falling leaves, pollen) around the camera.</summary>
    public class AmbientParticles : MonoBehaviour
    {
        public ParticleSystem fireflies;
        public ParticleSystem leaves;
        public ParticleSystem motes;
        public float fireflyDay = 3f, fireflyNight = 22f;
        public float leavesRate = 2.5f;
        public float motesDay = 6f, motesNight = 2f;

        void LateUpdate()
        {
            var cam = CameraRig.MainCam;
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            p.z = 0;
            transform.position = p;
            float n = DayNightCycle.NightFactor;
            SetRate(fireflies, Mathf.Lerp(fireflyDay, fireflyNight, n));
            SetRate(leaves, leavesRate);
            SetRate(motes, Mathf.Lerp(motesDay, motesNight, n));
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var e = ps.emission;
            e.rateOverTime = rate;
        }
    }
}
