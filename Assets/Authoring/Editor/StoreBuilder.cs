using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.HighDefinition;
using DonkeyDungeon.PlanarMirrors;

public static class StoreBuilder
{
    const string Root = "Assets/DonkeyDungeon/PlanarMirror";
    static readonly string[] Pipelines = { "Built-in", "URP", "HDRP" };
    public static void Validate()
    {
        try
        {
            string pipeline = "Built-in";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-mirrorPipeline") pipeline = args[i + 1];
            Directory.CreateDirectory("Assets/Authoring/Settings");
            if (pipeline == "URP")
            {
                const string path = "Assets/Authoring/Settings/URP.asset";
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (!asset)
                {
                    var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, "Assets/Authoring/Settings/URPRenderer.asset");
                    asset = UniversalRenderPipelineAsset.Create(renderer); AssetDatabase.CreateAsset(asset, path);
                }
                GraphicsSettings.defaultRenderPipeline = asset;
            }
            else if (pipeline == "HDRP")
            {
                const string path = "Assets/Authoring/Settings/HDRP.asset";
                var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
                if (!asset) { asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>(); AssetDatabase.CreateAsset(asset, path); }
                GraphicsSettings.defaultRenderPipeline = asset;
                typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.HDRenderPipelineGlobalSettings").GetMethod("Ensure", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.Invoke(null, new object[] { true });
            }
            else GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            EditorSceneManager.OpenScene(Root + "/Samples/" + pipeline.Replace("-", "") + "/MirrorShowcase.unity");
            var runner = new GameObject("Validation Runner").AddComponent<MirrorValidation>();
            runner.pipeline = pipeline;
            runner.reportDirectory = Path.GetFullPath("../Reports/" + pipeline);
            bool build = Array.IndexOf(args, "-mirrorBuild") >= 0;
            if (build) runner.reportDirectory += "-Windows";
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Authoring/Validation.unity");
            AssetDatabase.SaveAssets();
            if (build)
            {
                string output = "../PlayerValidation/" + pipeline + "/MirrorValidation.exe";
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/Authoring/Validation.unity" }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Player build failed: " + result.summary.result);
                EditorApplication.Exit(0); return;
            }
            Debug.Log("MIRROR_VALIDATION_READY " + pipeline);
            EditorApplication.delayCall += () => { Debug.Log("MIRROR_ENTER_PLAY"); EditorApplication.EnterPlaymode(); };
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static void Prepare()
    {
        try
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "DonkeyDungeon";
            PlayerSettings.productName = "Multi-Functional Mirror";
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            ImportTexture("MirrorDistortion_Normal.tif", true);
            ImportTexture("MirrorScratches_Mask.tga", false);
            ImportTexture("MirrorWaterStains_Mask.png", false);
            CreateMesh(false); CreateMesh(true);
            foreach (string pipeline in Pipelines) CreateDemo(pipeline);
            AssetDatabase.SaveAssets();
            File.WriteAllText("../Reports/Prepared.txt", "Prepared three pipeline demos. Visual validation still required.\n");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void ImportTexture(string name, bool normal)
    {
        var path = Root + "/Textures/" + name;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!texture || texture.width < 16 || texture.height < 16) throw new Exception("Texture has no valid pixels: " + path);
    }
    static void CreateMesh(bool circle)
    {
        string path = Root + "/Models/" + (circle ? "RoundMirror" : "SquareMirror") + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) return;
        var mesh = new Mesh { name = circle ? "RoundMirror" : "SquareMirror" };
        if (!circle)
        {
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0,1,2,0,2,3 };
        }
        else
        {
            const int sides = 96;
            var vertices = new Vector3[sides + 1]; var uv = new Vector2[sides + 1]; var triangles = new int[sides * 3];
            uv[0] = Vector2.one * .5f;
            for (int i = 0; i < sides; ++i)
            {
                float a = i * Mathf.PI * 2 / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .5f;
                uv[i + 1] = (Vector2)vertices[i + 1] + Vector2.one * .5f;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % sides + 1;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
        }
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
    }
    static Material Material(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader; return mat;
    }
    static void CreateDemo(string pipeline)
    {
        string suffix = pipeline.Replace("-", "");
        foreach (string folder in new[] { "Materials", "Prefabs", "Samples" }) Directory.CreateDirectory(Root + "/" + folder + "/" + suffix);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 2.5f, 8);
        camera.transform.LookAt(new Vector3(0, 1.4f, 0)); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f,.1f,.16f);
        if (pipeline == "HDRP")
        {
            var data = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            data.backgroundColorHDR = camera.backgroundColor;
            var volume = new GameObject("Fixed Exposure", typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure = profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(0);
            var profilePath = Root + "/Samples/HDRP/Exposure.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (existing) UnityEngine.Object.DestroyImmediate(profile); else
            { AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.AddObjectToAsset(exposure, profile); }
            volume.sharedProfile = existing ? existing : profile;
        }
        else if (pipeline == "URP") camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = pipeline == "HDRP" ? 20000 : 1.2f;
        light.transform.rotation = Quaternion.Euler(45,-30,0);
        Shader propShader = Shader.Find(pipeline == "HDRP" ? "HDRP/Unlit" : pipeline == "URP" ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        Color[] colors = { new Color(.8f,.1f,.07f), new Color(.05f,.7f,.22f), new Color(.04f,.3f,.9f) };
        string[] colorNames = { "Red", "Green", "Blue" };
        for (int i = 0; i < 3; ++i)
        {
            var mat = Material(Root + "/Materials/" + suffix + "/" + colorNames[i] + ".mat", propShader);
            foreach (var name in new[] { "_Color", "_BaseColor", "_UnlitColor" }) if (mat.HasProperty(name)) mat.SetColor(name, colors[i]);
            var obj = GameObject.CreatePrimitive(i == 0 ? PrimitiveType.Cube : i == 1 ? PrimitiveType.Sphere : PrimitiveType.Capsule);
            obj.name = "Reflection Subject " + i; obj.transform.position = new Vector3((i-1)*1.5f,1,2);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
        }
        for (int i = 0; i < 3; ++i)
        {
            string kind = i == 0 ? "SquareMirror" : i == 1 ? "RoundMirror" : "Puddle";
            var mat = Material(Root + "/Materials/" + suffix + "/" + kind + ".mat", Shader.Find("DonkeyDungeon/Planar Mirror/" + pipeline));
            mat.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/MirrorDistortion_Normal.tif"));
            mat.SetTexture("_Roughness", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/MirrorScratches_Mask.tga"));
            mat.SetTexture("_StainMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/MirrorWaterStains_Mask.png"));
            mat.SetFloat("_Distortion", i == 2 ? .012f : 0);
            mat.SetFloat("_RoughnessValue", i == 1 ? .65f : 0);
            mat.SetFloat("_StainStrength", i == 1 ? .2f : 0);
            mat.SetFloat("_PuddleMode", i == 2 ? 1 : 0);
            var obj = new GameObject(kind, typeof(MeshFilter), typeof(MeshRenderer), typeof(PlanarMirror));
            obj.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/Models/" + (i == 1 ? "RoundMirror" : "SquareMirror") + ".asset");
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            obj.GetComponent<PlanarMirror>().reflectSkybox = false;
            obj.transform.position = i == 2 ? new Vector3(0,.02f,2.5f) : new Vector3(i == 0 ? -1.6f : 1.6f,1.8f,0);
            obj.transform.localScale = i == 2 ? new Vector3(5,4,1) : new Vector3(3,3,1);
            if (i == 2) obj.transform.rotation = Quaternion.Euler(-90,0,0);
            PrefabUtility.SaveAsPrefabAsset(obj, Root + "/Prefabs/" + suffix + "/" + kind + ".prefab");
        }
        EditorSceneManager.SaveScene(scene, Root + "/Samples/" + suffix + "/MirrorShowcase.unity");
    }
}
