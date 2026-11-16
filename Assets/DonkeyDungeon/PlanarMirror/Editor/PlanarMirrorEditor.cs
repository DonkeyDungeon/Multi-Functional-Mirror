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
        static ScenePreview() { SceneView.beforeSceneGui += RenderOnRepaint; }

        static void RenderOnRepaint(SceneView view)
        {
            if (Event.current.type != EventType.Repaint || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            // The Scene view has configured its camera but has not rendered it yet.
            var activeTarget = RenderTexture.active;
            try { PlanarMirror.RenderSceneView(view.camera); }
            finally { RenderTexture.active = activeTarget; }
        }
    }
}
