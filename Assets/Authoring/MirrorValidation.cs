using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using DonkeyDungeon.PlanarMirrors;

public sealed class MirrorValidation : MonoBehaviour
{
    public string pipeline;
    public string reportDirectory;
    int errors;
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    IEnumerator Start()
    {
        Application.runInBackground = true;
        Directory.CreateDirectory(reportDirectory);
        var camera = Camera.main;
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        var mirrors = FindObjectsByType<PlanarMirror>(FindObjectsSortMode.None);
        yield return Frames(30);
        string report = "Pipeline: " + pipeline + "\nUnity: " + Application.unityVersion + "\nGPU: " + SystemInfo.graphicsDeviceName + "\n";
        try
        {
            var activePipeline = UnityEngine.Rendering.RenderPipelineManager.currentPipeline;
            string activeName = activePipeline == null ? "Built-in" : activePipeline.GetType().Name;
            Check(pipeline == "Built-in" ? activePipeline == null : activeName.Contains(pipeline == "URP" ? "Universal" : "HDRenderPipeline"), "Actual active pipeline matches test: " + activeName);
            report += "Active renderer: " + activeName + "\n";
            Check(mirrors.Length == 3, "Three sample surfaces exist");
            foreach (var mirror in mirrors)
            {
                Check(mirror.ActiveViewCount > 0, mirror.name + " allocated its own reflection");
                var block = new MaterialPropertyBlock(); mirror.GetComponent<Renderer>().GetPropertyBlock(block);
                var texture = block.GetTexture("_MainTex") as RenderTexture;
                Check(texture && texture.IsCreated(), mirror.name + " reflection created");
                var pixels = Read(texture);
                File.WriteAllBytes(reportDirectory + "/" + mirror.name + "-reflection.png", pixels.EncodeToPNG());
                int colored = pixels.GetPixels().Count(c => Mathf.Max(c.r,c.g,c.b) - Mathf.Min(c.r,c.g,c.b) > .15f);
                Destroy(pixels);
                Check(colored > 50, mirror.name + " reflects colored geometry; count=" + colored);
                report += mirror.name + ": " + texture.width + "px; colored pixels=" + colored + "\n";
            }
            Save(target, "Showcase.png");
            Check(!GL.invertCulling, "Culling restored");
        }
        catch (Exception e) { Finish(false, report + e); yield break; }

        var square = mirrors.First(m => m.name == "SquareMirror");
        var round = mirrors.First(m => m.name == "RoundMirror");
        var originalRoundMaterial = round.GetComponent<Renderer>().sharedMaterial;
        var material = square.GetComponent<Renderer>().sharedMaterial;
        round.GetComponent<Renderer>().sharedMaterial = material;
        yield return Frames(5);
        try
        {
            var a = new MaterialPropertyBlock(); var b = new MaterialPropertyBlock();
            square.GetComponent<Renderer>().GetPropertyBlock(a); round.GetComponent<Renderer>().GetPropertyBlock(b);
            Check(a.GetTexture("_MainTex") != b.GetTexture("_MainTex"), "Shared material keeps independent reflection textures");
            Check(!(material.GetTexture("_MainTex") is RenderTexture), "Shared material not overwritten");
            report += "Shared-material isolation: PASS\n";
        }
        catch (Exception e) { Finish(false, report + e); yield break; }
        round.GetComponent<Renderer>().sharedMaterial = originalRoundMaterial;
        var instance = new Material(material); square.GetComponent<Renderer>().sharedMaterial = instance;
        instance.SetFloat("_RoughnessValue", 0); yield return Frames(5);
        var baseline = Read(target);
        instance.SetFloat("_RoughnessValue", 1); yield return Frames(5);
        var scratched = Read(target);
        float delta = Difference(baseline, scratched);
        File.WriteAllBytes(reportDirectory + "/Scratch-On.png", scratched.EncodeToPNG());
        Destroy(baseline); Destroy(scratched);
        try { Check(delta > .0001f, "Scratch strength changes final pixels: " + delta); report += "Scratch image difference: " + delta + "\n"; }
        catch (Exception e) { Finish(false, report + e); yield break; }
        instance.SetFloat("_RoughnessValue", 0); instance.SetFloat("_Distortion", 0);
        yield return Frames(5); baseline = Read(target);
        instance.SetFloat("_Distortion", .2f); yield return Frames(5); var distorted = Read(target);
        delta = Difference(baseline, distorted); Destroy(baseline); Destroy(distorted);
        try { Check(delta > .0001f, "Distortion changes final pixels: " + delta); report += "Distortion image difference: " + delta + "\n"; }
        catch (Exception e) { Finish(false, report + e); yield break; }
        square.GetComponent<Renderer>().sharedMaterial = material; Destroy(instance);
        var second = new GameObject("Second viewing camera", typeof(Camera)).GetComponent<Camera>();
        second.CopyFrom(camera); second.enabled = false; second.targetTexture = null;
        second.transform.SetPositionAndRotation(camera.transform.position + Vector3.right * .2f, camera.transform.rotation);
        try
        {
            foreach (var mirror in mirrors) { mirror.RenderForCamera(second); Check(mirror.ActiveViewCount == 2, "Independent second camera view"); }
            report += "Second-camera allocation: PASS\n";
        }
        catch (Exception e) { Finish(false, report + e); yield break; }
        Destroy(second.gameObject);
        yield return new WaitForSecondsRealtime(10.5f);
        try
        {
            Check(mirrors.All(m => m.ActiveViewCount == 1), "Destroyed source camera pruned");
            report += "Destroyed viewing-camera cleanup: PASS\n";
        }
        catch (Exception e) { Finish(false, report + e); yield break; }
        foreach (var mirror in mirrors) mirror.enabled = false;
        yield return Frames(3);
        try
        {
            Check(mirrors.All(m => m.ActiveViewCount == 0), "Disable releases all per-view records");
            Check(Resources.FindObjectsOfTypeAll<Camera>().All(c => c.name != "Planar Mirror Reflection"), "No orphan reflection cameras");
            Check(Resources.FindObjectsOfTypeAll<RenderTexture>().All(t => t.name != "Planar Mirror Reflection RT"), "No orphan reflection textures");
            report += "Disable resource cleanup: PASS\n";
        }
        catch (Exception e) { Finish(false, report + e); yield break; }
        foreach (var mirror in mirrors) mirror.enabled = true;
        yield return Frames(5);
        foreach (var mirror in mirrors) Destroy(mirror.gameObject);
        yield return Frames(3);
        try
        {
            Check(Resources.FindObjectsOfTypeAll<Camera>().All(c => c.name != "Planar Mirror Reflection"), "Delete cleans cameras");
            Check(Resources.FindObjectsOfTypeAll<RenderTexture>().All(t => t.name != "Planar Mirror Reflection RT"), "Delete cleans textures");
            Check(errors == 0, "No logged runtime errors: " + errors);
            report += "Re-enable/delete cleanup: PASS\nRuntime errors: 0\n";
            Finish(true, report);
        }
        catch (Exception e) { Finish(false, report + e); }
    }
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Texture2D Read(RenderTexture target)
    {
        var camera = Camera.main;
        if (camera && camera.targetTexture == target)
        {
            if (!UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline) camera.Render();
            else UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
        }
        var old = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            var texture = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0); texture.Apply(); return texture;
        }
        finally { RenderTexture.active = old; }
    }
    void Save(RenderTexture texture, string name) { var read = Read(texture); File.WriteAllBytes(reportDirectory + "/" + name, read.EncodeToPNG()); Destroy(read); }
    static float Difference(Texture2D a, Texture2D b)
    {
        var x = a.GetPixels(); var y = b.GetPixels(); double sum = 0;
        for (int i = 0; i < x.Length; i++) sum += Math.Abs(x[i].r-y[i].r)+Math.Abs(x[i].g-y[i].g)+Math.Abs(x[i].b-y[i].b);
        return (float)(sum / (x.Length * 3));
    }
    void Finish(bool pass, string report)
    {
        File.WriteAllText(reportDirectory + "/Result.txt", (pass ? "PASS\n" : "FAIL\n") + report);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(pass ? 0 : 1);
#else
        Application.Quit(pass ? 0 : 1);
#endif
    }
}
