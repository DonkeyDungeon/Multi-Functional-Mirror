using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StoreExport
{
    const string Root = "Assets/DonkeyDungeon/PlanarMirror";
    public static void Export()
    {
        try
        {
            Directory.CreateDirectory("../Releases");
            foreach (var pipeline in new[] { "URP", "HDRP" })
            {
                var paths = new List<string> {
                    Root + "/Runtime/PlanarMirror.cs", Root + "/Runtime/DonkeyDungeon.PlanarMirrors.asmdef",
                    Root + "/Editor", Root + "/Textures", Root + "/Models", Root + "/Documentation",
                    Root + "/Shaders/PlanarMirror" + pipeline + ".shader",
                    Root + "/Materials/" + pipeline, Root + "/Prefabs/" + pipeline, Root + "/Samples/" + pipeline
                };
                if (pipeline != "BuiltIn") paths.Add(Root + "/Runtime/" + pipeline);
                foreach (var path in paths) if (!File.Exists(path) && !Directory.Exists(path)) throw new Exception("Missing export asset " + path);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/PlanarMirror" + pipeline + ".shader");
                if (ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader compile error: " + shader.name);
                AssetDatabase.ExportPackage(paths.ToArray(), "../Releases/Multi-Functional-Mirror_0.1.0_" + pipeline + ".unitypackage", ExportPackageOptions.Recurse);
            }
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
