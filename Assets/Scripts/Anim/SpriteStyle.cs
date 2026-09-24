using System.Collections;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// Dissolve and 1 px outline on a character sprite that uses RPG/Sprite Lit FX (plan T22).
    /// Values go through a MaterialPropertyBlock, so every character keeps sharing one material.
    /// </summary>
    public class SpriteStyle : MonoBehaviour
    {
        public SpriteRenderer target;
        [ColorUsage(true, true)] public Color dissolveEdge = new Color(2.2f, 1.1f, 0.35f, 1f);

        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int EdgeColorId = Shader.PropertyToID("_DissolveEdgeColor");
        static readonly int OutlineId = Shader.PropertyToID("_Outline");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");

        MaterialPropertyBlock block;
        float dissolve;
        float outline, outlineGoal;
        Color outlineColor = Color.white;
        bool dirty = true;

        /// <summary>False when the sprite does not use the FX shader (effects then do nothing).</summary>
        public bool Supported => target != null && target.sharedMaterial != null && target.sharedMaterial.HasProperty(DissolveId);

        public float DissolveAmount => dissolve;
        public float OutlineAmount => outline;

        void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        }

        void OnEnable() => ResetStyle();

        /// <summary>Back to a plain sprite (reused / respawned characters).</summary>
        public void ResetStyle()
        {
            dissolve = 0f;
            outline = outlineGoal = 0f;
            dirty = true;
            Apply();
        }

        /// <summary>Fades the outline in or out (hover, selection, elites).</summary>
        public void SetOutline(bool on, Color color)
        {
            outlineGoal = on ? 1f : 0f;
            if (on) outlineColor = color;
        }

        /// <summary>Burns the sprite away over <paramref name="seconds"/>.</summary>
        public IEnumerator Dissolve(float seconds)
        {
            outlineGoal = 0f;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                dissolve = Mathf.Clamp01(t / seconds);
                dirty = true;
                yield return null;
            }
            dissolve = 1f;
            dirty = true;
            Apply();
        }

        void LateUpdate()
        {
            if (outline != outlineGoal)
            {
                outline = Mathf.MoveTowards(outline, outlineGoal, Time.unscaledDeltaTime * 8f);
                dirty = true;
            }
            if (dirty) Apply();
        }

        void Apply()
        {
            if (!Supported) return;
            dirty = false;
            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetFloat(DissolveId, dissolve);
            block.SetColor(EdgeColorId, dissolveEdge);
            block.SetFloat(OutlineId, outline);
            block.SetColor(OutlineColorId, outlineColor);
            target.SetPropertyBlock(block);
        }
    }
}
