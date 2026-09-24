// Reference stubs for URP 17 / SRP Core (Light2D, Volume framework). Signatures only.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine.Rendering
{
    public abstract class VolumeParameter { public bool overrideState { get; set; } public virtual void SetValue(VolumeParameter parameter) { } public virtual object GetValue() => null; }
    public class VolumeParameter<T> : VolumeParameter
    {
        public VolumeParameter() { }
        protected VolumeParameter(T value, bool overrideState) { }
        public virtual T value { get; set; }
        public void Override(T x) { }
        public static implicit operator T(VolumeParameter<T> prop) => default;
    }
    public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool value, bool overrideState = false) { } }
    public class IntParameter : VolumeParameter<int> { public IntParameter(int value, bool overrideState = false) { } }
    public class FloatParameter : VolumeParameter<float> { public FloatParameter(float value, bool overrideState = false) { } }
    public class MinFloatParameter : FloatParameter { public float min; public MinFloatParameter(float value, float min, bool overrideState = false) : base(value, overrideState) { } }
    public class MaxFloatParameter : FloatParameter { public float max; public MaxFloatParameter(float value, float max, bool overrideState = false) : base(value, overrideState) { } }
    public class ClampedFloatParameter : FloatParameter { public float min, max; public ClampedFloatParameter(float value, float min, float max, bool overrideState = false) : base(value, overrideState) { } }
    public class ClampedIntParameter : IntParameter { public int min, max; public ClampedIntParameter(int value, int min, int max, bool overrideState = false) : base(value, overrideState) { } }
    public class ColorParameter : VolumeParameter<Color> { public bool hdr, showAlpha, showEyeDropper; public ColorParameter(Color value, bool overrideState = false) { } public ColorParameter(Color value, bool hdr, bool showAlpha, bool showEyeDropper, bool overrideState = false) { } }
    public class Vector2Parameter : VolumeParameter<Vector2> { public Vector2Parameter(Vector2 value, bool overrideState = false) { } }
    public class TextureParameter : VolumeParameter<Texture> { public TextureParameter(Texture value, bool overrideState = false) { } }
    public abstract class VolumeComponent : ScriptableObject
    {
        public bool active = true;
        public string displayName { get; protected set; }
        public System.Collections.ObjectModel.ReadOnlyCollection<VolumeParameter> parameters => null;
        public void SetAllOverridesTo(bool state) { }
    }
    public sealed class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new List<VolumeComponent>();
        public bool isDirty;
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public VolumeComponent Add(Type type, bool overrides = false) => null;
        public void Remove<T>() where T : VolumeComponent { }
        public bool Has<T>() where T : VolumeComponent => false;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
        public bool TryGet<T>(Type type, out T component) where T : VolumeComponent { component = null; return false; }
        public void Reset() { }
    }
    public class Volume : MonoBehaviour
    {
        public bool isGlobal = true;
        public float priority;
        public float blendDistance;
        public float weight = 1f;
        public VolumeProfile sharedProfile;
        public VolumeProfile profile { get; set; }
        public bool HasInstantiatedProfile() => false;
    }
    public enum RenderPipelineType { }
    public class DebugManager { public static DebugManager instance => null; public bool enableRuntimeUI { get; set; } }
}
namespace UnityEngine.Rendering.Universal
{
    public enum BloomDownscaleMode { Half, Quarter }
    public enum TonemappingMode { None, Neutral, ACES }
    public enum FilmGrainLookup { Thin1, Thin2, Medium1, Medium2, Medium3, Medium4, Medium5, Medium6, Large01, Large02, Custom }
    public enum VignetteMode { Classic, Masked }
    public sealed class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);
        public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);
        public BoolParameter highQualityFiltering = new BoolParameter(false);
        public ClampedIntParameter skipIterations = new ClampedIntParameter(1, 0, 16);
        public TextureParameter dirtTexture = new TextureParameter(null);
        public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);
        public ClampedIntParameter maxIterations = new ClampedIntParameter(6, 2, 8);
        public bool IsActive() => false;
    }
    public sealed class Vignette : VolumeComponent
    {
        public ColorParameter color = new ColorParameter(Color.black, false, false, true);
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.2f, 0.01f, 1f);
        public BoolParameter rounded = new BoolParameter(false);
        public bool IsActive() => false;
    }
    public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure = new FloatParameter(0f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);
        public ColorParameter colorFilter = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter hueShift = new ClampedFloatParameter(0f, -180f, 180f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);
        public bool IsActive() => false;
    }
    public sealed class ChromaticAberration : VolumeComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f); public bool IsActive() => false; }
    public sealed class LensDistortion : VolumeComponent
    {
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter xMultiplier = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter yMultiplier = new ClampedFloatParameter(1f, 0f, 1f);
        public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));
        public ClampedFloatParameter scale = new ClampedFloatParameter(1f, 0.01f, 5f);
        public bool IsActive() => false;
    }
    public sealed class FilmGrain : VolumeComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f); public ClampedFloatParameter response = new ClampedFloatParameter(0.8f, 0f, 1f); }
    public sealed class Tonemapping : VolumeComponent { }
    public sealed class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature = new ClampedFloatParameter(0f, -100f, 100f); public ClampedFloatParameter tint = new ClampedFloatParameter(0f, -100f, 100f); }
    public sealed class SplitToning : VolumeComponent { }
    public sealed class MotionBlur : VolumeComponent { }
    public sealed class PaniniProjection : VolumeComponent { }
    public sealed class DepthOfField : VolumeComponent { }

    public class Light2DBase : MonoBehaviour { }
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class Light2D : Light2DBase
    {
        public enum LightType { Parametric = 0, Freeform = 1, Sprite = 2, Point = 3, Global = 4 }
        public enum NormalMapQuality { Disabled = 2, Fast = 0, Accurate = 1 }
        public enum OverlapOperation { Additive, AlphaBlend }
        public LightType lightType { get; set; }
        public int blendStyleIndex { get; set; }
        public float falloffIntensity { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float volumeIntensity { get; set; }
        public bool volumeIntensityEnabled { get; set; }
        public Sprite lightCookieSprite { get; set; }
        public int lightOrder { get; set; }
        public bool alphaBlendOnOverlap { get; }
        public OverlapOperation overlapOperation { get; set; }
        public int[] targetSortingLayers { get; set; }
        public float pointLightInnerAngle { get; set; }
        public float pointLightOuterAngle { get; set; }
        public float pointLightInnerRadius { get; set; }
        public float pointLightOuterRadius { get; set; }
        public float normalMapDistance { get; }
        public NormalMapQuality normalMapQuality { get; }
        public float shapeLightFalloffSize { get; set; }
        public Vector3[] shapePath { get; }
        public bool shadowsEnabled { get; set; }
        public float shadowIntensity { get; set; }
        public float shadowSoftness { get; set; }
        public float shadowSoftnessFalloffIntensity { get; set; }
        public bool volumetricShadowsEnabled { get; set; }
        public float shadowVolumeIntensity { get; set; }
        public void SetShapePath(Vector3[] path) { }
    }
    public class ShadowCaster2D : MonoBehaviour { public bool castsShadows { get; set; } public bool selfShadows { get; set; } public bool useRendererSilhouette { get; set; } }
    public class PixelPerfectCamera : MonoBehaviour
    {
        public enum CropFrame { None, Pillarbox, Letterbox, Windowbox, StretchFill }
        public enum GridSnapping { None, PixelSnapping, UpscaleRenderTexture }
        public int assetsPPU { get; set; }
        public int refResolutionX { get; set; }
        public int refResolutionY { get; set; }
        public CropFrame cropFrame { get; set; }
        public GridSnapping gridSnapping { get; set; }
        public float orthographicSize => 0;
        public int pixelRatio => 1;
        public Vector3 RoundToPixel(Vector3 position) => position;
    }
    public class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing { get; set; } public bool stopNaN { get; set; } public bool dithering { get; set; } public AntialiasingMode antialiasing { get; set; } public LayerMask volumeLayerMask { get; set; } public Transform volumeTrigger { get; set; } public void SetRenderer(int index) { } }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public ScriptableRendererData[] rendererDataList => null;
        public bool supportsHDR { get; set; }
        public int msaaSampleCount { get; set; }
        public float renderScale { get; set; }
        public override RenderPipeline CreatePipeline() => null;
    }
    public abstract class ScriptableRendererData : ScriptableObject { }
    public class Renderer2DData : ScriptableRendererData { public float hdrEmulationScale { get; set; } }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null; }
}
