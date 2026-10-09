using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.EditorTools
{
    /// <summary>Gives a camera the project's pixel-perfect settings (see docs/DECISIONS.md, section 3).</summary>
    public static class PixelCameraSetup
    {
        public static void Configure(Camera camera, int referenceWidth = PixelArt.ReferenceWidth, int referenceHeight = PixelArt.ReferenceHeight)
        {
            Undo.RecordObject(camera, "Set up pixel camera");
            camera.orthographic = true;

            var pixel = camera.GetComponent<PixelPerfectCamera>();
            if (pixel == null)
                pixel = Undo.AddComponent<PixelPerfectCamera>(camera.gameObject);
            else
                Undo.RecordObject(pixel, "Set up pixel camera");

            pixel.assetsPPU = PixelArt.PixelsPerUnit;
            pixel.refResolutionX = referenceWidth;
            pixel.refResolutionY = referenceHeight;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixel.cropFrame = PixelPerfectCamera.CropFrame.None;
            EditorUtility.SetDirty(pixel);
        }
    }
}
