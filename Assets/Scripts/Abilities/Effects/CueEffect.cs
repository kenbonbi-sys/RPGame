using UnityEngine;

namespace RPG
{
    /// <summary>Presentation only: a VFX, a sound, camera shake, a screen flash, an impact pulse.</summary>
    [System.Serializable]
    public class CueEffect : AbilityEffect
    {
        public Anchor at = new Anchor(Anchor.From.Point);
        [Tooltip("VFX id from the VFX library (empty = none).")]
        public string vfx;
        public float vfxScale = 1f;
        [Tooltip("Rotate the VFX to the cast direction.")]
        public bool rotateToDirection;
        [Tooltip("Mirror the VFX on every other hit of a combo (sword swings).")]
        public bool flipOnOddCombo;
        [Tooltip("The VFX follows the caster.")]
        public bool attachToCaster;
        public string sfx;
        [Range(0, 1)] public float sfxVolume = 0.8f;
        public float sfxPitchVariance = 0.06f;
        [Tooltip("Plays the sound from this position (3D) instead of at the listener.")]
        public bool sfxAtPoint;
        public float sfxMinInterval = 0.03f;
        public float shake;
        public Color flashColor = Color.white;
        [Tooltip("0 = no screen flash.")]
        public float flashStrength;
        public float flashDuration = 0.2f;
        [Tooltip("0 = no impact pulse (post-processing punch).")]
        public float impact;
        public float impactDuration = 0.3f;

        public override void Run(AbilityContext ctx)
        {
            Vector2 p = at.Resolve(ctx);
            if (!string.IsNullOrEmpty(vfx))
            {
                var rot = rotateToDirection ? Quaternion.Euler(0, 0, Util.Angle(ctx.dir)) : Quaternion.identity;
                var fx = VFX.Spawn(vfx, p, rot, vfxScale * ctx.scale, attachToCaster ? ctx.CasterTransform : null);
                if (fx != null && flipOnOddCombo && ctx.combo % 2 == 1)
                {
                    var s = fx.transform.localScale;
                    fx.transform.localScale = new Vector3(s.x, -s.y, s.z);
                }
            }
            if (!string.IsNullOrEmpty(sfx)) AudioManager.Play(sfx, sfxVolume, sfxPitchVariance, sfxAtPoint ? p : (Vector3?)null, sfxMinInterval);
            if (shake > 0) CameraRig.Shake(shake);
            if (flashStrength > 0) ScreenFX.Flash(flashColor, flashStrength, flashDuration);
            if (impact > 0) ScreenFX.Impact(impact, impactDuration);
        }
    }
}
