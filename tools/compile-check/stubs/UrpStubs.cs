// Compile-check stubs: ONLY the URP and SRP Core members the KingdomGame scripts
// use, with signatures copied from com.unity.render-pipelines.universal and
// com.unity.render-pipelines.core 17.3 (github.com/Unity-Technologies/Graphics,
// branch 6000.3/staging).
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    public class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public float priority = 0f;
        public float weight = 1f;
        public VolumeProfile sharedProfile = null;
        public VolumeProfile profile { get; set; }
    }

    public sealed class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new List<VolumeComponent>();
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
    }

    public partial class VolumeComponent : ScriptableObject
    {
        public bool active = true;
    }

    public abstract class VolumeParameter { }

    public class VolumeParameter<T> : VolumeParameter
    {
        public virtual T value { get; set; }
        public void Override(T x) { }
    }

    public class BoolParameter : VolumeParameter<bool>
    {
        public BoolParameter(bool value, bool overrideState = false) { }
    }

    public class FloatParameter : VolumeParameter<float>
    {
        public FloatParameter(float value, bool overrideState = false) { }
    }

    public class MinFloatParameter : FloatParameter
    {
        public MinFloatParameter(float value, float min, bool overrideState = false) : base(value, overrideState) { }
    }

    public class ClampedFloatParameter : FloatParameter
    {
        public ClampedFloatParameter(float value, float min, float max, bool overrideState = false) : base(value, overrideState) { }
    }

    public class ColorParameter : VolumeParameter<Color>
    {
        public ColorParameter(Color value, bool overrideState = false) { }
        public ColorParameter(Color value, bool hdr, bool showAlpha, bool showEyeDropper, bool overrideState = false) { }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public partial class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public int msaaSampleCount { get; set; }
        public override RenderPipeline CreatePipeline() => null; // base signature differs in the 2021.3 reference DLLs
    }

    public abstract partial class ScriptableRendererData : ScriptableObject { }
    public partial class Renderer2DData : ScriptableRendererData { }

    public sealed class Light2D : MonoBehaviour
    {
        public enum LightType { Parametric = 0, Freeform = 1, Sprite = 2, Point = 3, Global = 4 }
        public LightType lightType { get; set; }
        public float intensity { get; set; }
        public Color color { get; set; }
        public int[] targetSortingLayers { get; set; }
        public float pointLightInnerRadius { get; set; }
        public float pointLightOuterRadius { get; set; }
    }

    public partial class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing { get; set; }
    }

    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null;
    }

    public sealed partial class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);
    }

    public sealed class Vignette : VolumeComponent
    {
        public ColorParameter color = new ColorParameter(Color.black, false, false, true);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.2f, 0.01f, 1f);
    }

    public enum FilmGrainLookup { Thin1, Thin2, Medium1, Medium2, Medium3, Medium4, Medium5, Medium6, Large01, Large02, Custom }

    public sealed class FilmGrainLookupParameter : VolumeParameter<FilmGrainLookup>
    {
        public FilmGrainLookupParameter(FilmGrainLookup value, bool overrideState = false) { }
    }

    public sealed class FilmGrain : VolumeComponent
    {
        public FilmGrainLookupParameter type = new FilmGrainLookupParameter(FilmGrainLookup.Thin1);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter response = new ClampedFloatParameter(0.8f, 0f, 1f);
    }

    public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure = new FloatParameter(0f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);
    }
}
