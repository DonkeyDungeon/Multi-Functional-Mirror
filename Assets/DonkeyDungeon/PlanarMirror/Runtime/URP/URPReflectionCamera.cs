using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DonkeyDungeon.PlanarMirrors
{
    internal static class URPReflectionCamera
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
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;
            var data = target.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
        }
    }
}
