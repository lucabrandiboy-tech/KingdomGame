// Compile-check stubs: ONLY the URP members the KingdomGame scripts use, with
// signatures copied from com.unity.render-pipelines.universal 17.3.0
// (github.com/Unity-Technologies/Graphics, branch 6000.3/staging).
namespace UnityEngine.Rendering.Universal
{
    public partial class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public int msaaSampleCount { get; set; }
        public override RenderPipeline CreatePipeline() => null; // base signature differs in the 2021.3 reference DLLs
    }

    public abstract partial class ScriptableRendererData : ScriptableObject { }
    public partial class Renderer2DData : ScriptableRendererData { }

    public class PixelPerfectCamera : MonoBehaviour
    {
        public enum CropFrame { None, Pillarbox, Letterbox, Windowbox, StretchFill }
        public enum GridSnapping { None, PixelSnapping, UpscaleRenderTexture }
        public CropFrame cropFrame { get; set; }
        public GridSnapping gridSnapping { get; set; }
        public int assetsPPU { get; set; }
        public int refResolutionX { get; set; }
        public int refResolutionY { get; set; }
    }

    public sealed class Light2D : MonoBehaviour
    {
        public enum LightType { Parametric = 0, Freeform = 1, Sprite = 2, Point = 3, Global = 4 }
        public LightType lightType { get; set; }
        public float intensity { get; set; }
        public int[] targetSortingLayers { get; set; }
    }
}
