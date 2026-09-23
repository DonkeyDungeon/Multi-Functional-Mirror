using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace DonkeyDungeon.PlanarMirrors
{
    internal static class HDRPReflectionCamera
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void Register()
        {
            PlanarMirror.ConfigureReflectionCamera -= Configure;
            PlanarMirror.ConfigureReflectionCamera += Configure;
        }
        static void Configure(Camera source, Camera target)
        {
            if (!(GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset)) return;
            var data = target.GetComponent<HDAdditionalCameraData>();
            if (!data) data = target.gameObject.AddComponent<HDAdditionalCameraData>();
            data.invertFaceCulling = true;
            data.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
            data.clearColorMode = target.clearFlags == CameraClearFlags.Skybox ? HDAdditionalCameraData.ClearColorMode.Sky : HDAdditionalCameraData.ClearColorMode.Color;
            data.backgroundColorHDR = target.backgroundColor;
            var sourceData = source.GetComponent<HDAdditionalCameraData>();
            if (sourceData) data.volumeLayerMask = sourceData.volumeLayerMask;
        }
    }
}
