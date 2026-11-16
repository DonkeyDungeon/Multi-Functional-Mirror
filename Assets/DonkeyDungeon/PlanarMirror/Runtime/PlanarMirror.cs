using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DonkeyDungeon.PlanarMirrors
{
    /// <summary>A planar reflection with resources owned by each surface and viewing camera.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Renderer))]
    [DefaultExecutionOrder(10000)]
    public sealed class PlanarMirror : MonoBehaviour
    {
        [Header("Reflection quality")]
        [Tooltip("Square reflection texture size. Rounded to a power of two.")]
        [Range(64, 4096)] public int resolution = 1024;
        [Tooltip("Layers included in the reflected view. Mirror surfaces are excluded automatically.")]
        public LayerMask reflectionLayers = -1;
        [Tooltip("1 updates every frame. Larger values trade freshness for CPU/GPU cost.")]
        [Range(1, 30)] public int updateEveryFrames = 1;
        [Min(1)] public float farDistance = 100;
        [Header("Surface")]
        [Tooltip("Local outward normal. The supplied meshes face local +Z.")]
        public Vector3 localNormal = Vector3.forward;
        [Range(0.001f, 0.2f)] public float clipOffset = 0.01f;
        public bool reflectSkybox = true;
        public bool transparentBackground;
        public Color backgroundColor = Color.black;
        [Tooltip("Preview reflections in the Scene view (editor only).")]
        public bool sceneViewPreview = true;

        sealed class View
        {
            public Camera camera;
            public RenderTexture texture;
            public Matrix4x4 projection;
            public bool hasCapture;
            public int frame;
            public float usedAt;
        }
        readonly Dictionary<Camera, View> views = new Dictionary<Camera, View>();
        readonly List<Camera> expired = new List<Camera>();
        static readonly HashSet<PlanarMirror> mirrors = new HashSet<PlanarMirror>();
        static bool rendering;
        public static event System.Action<Camera, Camera> ConfigureReflectionCamera;
        static readonly int ReflectionTex = Shader.PropertyToID("_DDMirrorReflectionTex");
        static readonly int ReflectionVP = Shader.PropertyToID("_DDMirrorReflectionVP");
        static readonly int Transparent = Shader.PropertyToID("_DDMirrorTransparentBackground");
        static readonly int HasReflection = Shader.PropertyToID("_DDMirrorHasReflection");
        Renderer surface;
        MaterialPropertyBlock properties;
        View boundView;
        Camera[] cameras = new Camera[4];
        public int ActiveViewCount => views.Count;

        void OnEnable()
        {
            surface = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            mirrors.Add(this);
            Camera.onPreCull += BindBuiltIn;
            RenderPipelineManager.beginCameraRendering += BindSRP;
        }
        void OnDisable() { ReleaseAll(); }
        void OnDestroy() { ReleaseAll(); }
        void OnValidate()
        {
            resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 64, 4096);
            updateEveryFrames = Mathf.Clamp(updateEveryFrames, 1, 30);
            farDistance = Mathf.Max(1, farDistance);
            if (localNormal.sqrMagnitude < 0.001f) localNormal = Vector3.forward;
        }
        void LateUpdate()
        {
            int count = Camera.allCamerasCount;
            if (cameras.Length < count) cameras = new Camera[count];
            count = Camera.GetAllCameras(cameras);
            for (int i = 0; i < count; ++i) RenderForCamera(cameras[i]);
            expired.Clear();
            foreach (var pair in views)
                if (!pair.Key || Time.realtimeSinceStartup - pair.Value.usedAt > 10) expired.Add(pair.Key);
            foreach (var key in expired) { Dispose(views[key]); views.Remove(key); }
        }
        void BindBuiltIn(Camera camera) { if (!GraphicsSettings.currentRenderPipeline) Bind(camera); }
        void BindSRP(ScriptableRenderContext context, Camera camera) { Bind(camera); }
        void Bind(Camera camera)
        {
            if (rendering || !surface) return;
            if (CanObserve(camera) && views.TryGetValue(camera, out var view) && view.hasCapture &&
                view.texture && view.texture.IsCreated() && IsInFront(camera, WorldNormal()))
                ApplyReflection(view);
            else
                ApplyReflection(null);
        }

        void ApplyReflection(View view)
        {
            if (view == null && boundView == null) return;

            // Only write mirror-owned properties. Other scripts can update the same block.
            surface.GetPropertyBlock(properties);
            properties.SetTexture(ReflectionTex, view == null ? Texture2D.blackTexture : (Texture)view.texture);
            properties.SetMatrix(ReflectionVP, view == null ? Matrix4x4.identity : view.projection);
            properties.SetFloat(Transparent, view != null && transparentBackground ? 1 : 0);
            properties.SetFloat(HasReflection, view == null ? 0 : 1);
            surface.SetPropertyBlock(properties);
            boundView = view;
        }

        bool CanObserve(Camera source) => surface.enabled && source &&
            source.cameraType != CameraType.Reflection && source.cameraType != CameraType.Preview &&
            (source.cameraType != CameraType.SceneView || sceneViewPreview) && !source.stereoEnabled &&
            (source.cullingMask & (1 << gameObject.layer)) != 0;

        Vector3 WorldNormal() => transform.worldToLocalMatrix.transpose.MultiplyVector(localNormal).normalized;

        bool IsInFront(Camera source, Vector3 normal) =>
            Vector3.Dot(source.transform.position - transform.position, normal) > clipOffset;

        /// <summary>Call outside the SRP render loop, after moving the viewing camera.</summary>
        public void RenderForCamera(Camera source)
        {
            if (rendering || !isActiveAndEnabled || !surface || !CanObserve(source)) return;
            Vector3 normal = WorldNormal();
            if (!IsInFront(source, normal)) return;
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(source), surface.bounds)) return;
            if (!views.TryGetValue(source, out var view))
            {
                var go = new GameObject("Planar Mirror Reflection", typeof(Camera), typeof(Skybox));
                go.hideFlags = HideFlags.HideAndDontSave;
                view = new View { camera = go.GetComponent<Camera>() };
                view.camera.enabled = false;
                view.camera.cameraType = CameraType.Reflection;
                views.Add(source, view);
            }
            view.usedAt = Time.realtimeSinceStartup;
            int size = Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), 64, 4096);
            if (!view.texture || !view.texture.IsCreated() || view.texture.width != size)
            {
                ReleaseTexture(view);
                view.texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBHalf)
                { name = "Planar Mirror Reflection RT", hideFlags = HideFlags.HideAndDontSave,
                  useMipMap = false, autoGenerateMips = false, antiAliasing = 1 };
                if (!view.texture.Create()) return;
            }
            if (view.hasCapture && Application.isPlaying && Time.frameCount - view.frame < updateEveryFrames) { Bind(source); return; }
            view.hasCapture = false;
            Camera reflected = view.camera;
            reflected.CopyFrom(source);
            reflected.enabled = false;
            reflected.cameraType = CameraType.Reflection;
            reflected.stereoTargetEye = StereoTargetEyeMask.None;
            reflected.allowMSAA = false;
            reflected.allowDynamicResolution = false;
            reflected.useOcclusionCulling = false;
            reflected.cullingMask = reflectionLayers & source.cullingMask;
            reflected.farClipPlane = Mathf.Max(source.nearClipPlane + 0.01f, Mathf.Min(source.farClipPlane, farDistance));
            reflected.rect = new Rect(0, 0, 1, 1);
            reflected.targetTexture = view.texture;
            reflected.clearFlags = reflectSkybox && !transparentBackground ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            reflected.backgroundColor = transparentBackground ? Color.clear : backgroundColor;
            var sky = reflected.GetComponent<Skybox>();
            var sourceSky = source.GetComponent<Skybox>();
            sky.enabled = reflectSkybox && !transparentBackground && sourceSky && sourceSky.enabled && sourceSky.material;
            sky.material = sky.enabled ? sourceSky.material : null;

            Vector3 position = transform.position;
            Matrix4x4 reflection = ReflectionMatrix(normal, -Vector3.Dot(normal, position));
            reflected.transform.SetPositionAndRotation(reflection.MultiplyPoint(source.transform.position),
                Quaternion.LookRotation(Vector3.Reflect(source.transform.forward, normal), Vector3.Reflect(source.transform.up, normal)));
            reflected.worldToCameraMatrix = source.worldToCameraMatrix * reflection;
            Vector3 clipPosition = reflected.worldToCameraMatrix.MultiplyPoint(position + normal * clipOffset);
            Vector3 clipNormal = reflected.worldToCameraMatrix.MultiplyVector(normal).normalized;
            Matrix4x4 captureProjection = source.projectionMatrix;
            float near = source.nearClipPlane, far = reflected.farClipPlane;
            captureProjection[2, 2] = source.orthographic ? -2 / (far - near) : -(far + near) / (far - near);
            captureProjection[2, 3] = source.orthographic ? -(far + near) / (far - near) : -2 * far * near / (far - near);
            reflected.projectionMatrix = captureProjection;
            reflected.projectionMatrix = reflected.CalculateObliqueMatrix(new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPosition, clipNormal)));
            reflected.cullingMatrix = reflected.projectionMatrix * reflected.worldToCameraMatrix;
            var hidden = new List<KeyValuePair<Renderer, bool>>();
            bool previousCulling = GL.invertCulling;
            rendering = true;
            try
            {
                ConfigureReflectionCamera?.Invoke(source, reflected);
                foreach (var mirror in mirrors)
                    if (mirror && mirror.surface)
                    {
                        hidden.Add(new KeyValuePair<Renderer, bool>(mirror.surface, mirror.surface.forceRenderingOff));
                        mirror.surface.forceRenderingOff = true;
                    }
                GL.invertCulling = !previousCulling;
                if (!GraphicsSettings.currentRenderPipeline) reflected.Render();
                else
                {
                    var request = new RenderPipeline.StandardRequest { destination = view.texture };
                    if (!RenderPipeline.SupportsRenderRequest(reflected, request)) return;
                    RenderPipeline.SubmitRenderRequest(reflected, request);
                }
                view.projection = GL.GetGPUProjectionMatrix(reflected.projectionMatrix, true) * reflected.worldToCameraMatrix;
                view.frame = Time.frameCount;
                view.hasCapture = true;
            }
            finally
            {
                GL.invertCulling = previousCulling;
                foreach (var entry in hidden) if (entry.Key) entry.Key.forceRenderingOff = entry.Value;
                rendering = false;
                if (!view.hasCapture && boundView == view && surface) ApplyReflection(null);
            }
            Bind(source);
        }
        public static Matrix4x4 ReflectionMatrix(Vector3 n, float d)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            for (int row = 0; row < 3; ++row)
            {
                for (int column = 0; column < 3; ++column)
                    matrix[row, column] -= 2 * n[row] * n[column];
                matrix[row, 3] = -2 * d * n[row];
            }
            return matrix;
        }
        void ReleaseAll()
        {
            Camera.onPreCull -= BindBuiltIn;
            RenderPipelineManager.beginCameraRendering -= BindSRP;
            mirrors.Remove(this);
            if (surface) ApplyReflection(null);
            boundView = null;
            foreach (var view in views.Values) Dispose(view);
            views.Clear();
        }
        void Dispose(View view)
        {
            ReleaseTexture(view);
            if (view.camera) DestroyOwned(view.camera.gameObject);
        }
        void ReleaseTexture(View view)
        {
            if (boundView == view && surface) ApplyReflection(null);
            view.hasCapture = false;
            if (view.camera) view.camera.targetTexture = null;
            if (view.texture) { view.texture.Release(); DestroyOwned(view.texture); }
            view.texture = null;
        }
        static void DestroyOwned(Object obj)
        {
            if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, WorldNormal());
        }
    }
}
