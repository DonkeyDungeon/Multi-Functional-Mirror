using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class StoreExport
{
    const string Root = "Assets/DonkeyDungeon/PlanarMirror";

    public static void Export()
    {
        try
        {
            string version = RequiredArgument("-mirrorVersion");
            if (!Regex.IsMatch(version, @"^[0-9][0-9A-Za-z.-]*$"))
                throw new ArgumentException("Invalid release version: " + version);

            string pipeline = RequiredArgument("-mirrorPipeline");
            if (pipeline != "BuiltIn" && pipeline != "URP" && pipeline != "HDRP")
                throw new ArgumentException("Unknown pipeline: " + pipeline);

            string output = Path.GetFullPath(RequiredArgument("-mirrorOutput"));
            Directory.CreateDirectory(output);
            ExportPipeline(pipeline, version, output);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void ExportPipeline(string pipeline, string version, string output)
    {
        string folder = pipeline == "BuiltIn" ? "Builtin" : pipeline;
        string shaderPath = Root + "/Shaders/PlanarMirror" + pipeline + ".shader";
        var paths = new List<string> {
            Root + "/Runtime/PlanarMirror.cs",
            Root + "/Runtime/DonkeyDungeon.PlanarMirrors.asmdef",
            Root + "/Editor", Root + "/Textures", Root + "/Models", Root + "/Documentation",
            shaderPath,
            Root + "/Materials/" + folder,
            Root + "/Prefabs/" + folder,
            Root + "/Samples/" + folder
        };
        if (pipeline != "BuiltIn") paths.Add(Root + "/Runtime/" + pipeline);

        foreach (string path in paths)
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException("Missing export asset", path);

        string license = Root + "/Documentation/LICENSE.txt";
        if (!File.Exists(license) || File.ReadAllText(license) != File.ReadAllText("LICENSE"))
            throw new InvalidOperationException("The package license must match the repository LICENSE.");

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (!shader || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Shader import failed: " + shaderPath);

        string package = Path.Combine(output, $"Multi-Functional-Mirror_{version}_{pipeline}.unitypackage");
        AssetDatabase.ExportPackage(paths.ToArray(), package, ExportPackageOptions.Recurse);
        if (!File.Exists(package) || new FileInfo(package).Length == 0)
            throw new IOException("Unity did not write the package: " + package);
        Debug.Log("Exported " + package);
    }

    static string RequiredArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        if (index < 0 || index + 1 == arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
            throw new ArgumentException("Specify " + name);
        return arguments[index + 1];
    }
}
