using SsalMuk.Unity;
using UnityEditor;

namespace SsalMuk.Editor
{
    [CustomEditor(typeof(DevelopmentDefaults))]
    internal sealed class DevelopmentDefaultsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            string error = ((DevelopmentDefaults)target).ValidationError;
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
