using UnityEditor;
using UnityEngine;

namespace DonkeyDungeon.PlanarMirrors.Editor
{
    [CustomEditor(typeof(PlanarMirror)), CanEditMultipleObjects]
    public sealed class PlanarMirrorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("The cyan arrow is the reflective front. Material settings control scratches, stains and puddles. Reflections exclude other mirror surfaces. XR is not supported.", MessageType.Info);
            var mirror = (PlanarMirror)target;
            EditorGUILayout.LabelField("Allocated viewing cameras", mirror.ActiveViewCount.ToString());
        }
    }

    [InitializeOnLoad]
    internal static class ScenePreview
    {
        static double nextUpdate;
        static ScenePreview() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.timeSinceStartup < nextUpdate) return;
            nextUpdate = EditorApplication.timeSinceStartup + 0.1;
            var view = SceneView.lastActiveSceneView;
            if (!view || !view.camera) return;
            foreach (var mirror in Object.FindObjectsByType<PlanarMirror>(FindObjectsSortMode.None))
                if (mirror.sceneViewPreview) mirror.RenderForCamera(view.camera);
        }
    }
}
