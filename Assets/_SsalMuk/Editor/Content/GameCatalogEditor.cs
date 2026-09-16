using SsalMuk.Unity;
using UnityEditor;

namespace SsalMuk.Editor
{
    [CustomEditor(typeof(GameCatalog))]
    internal sealed class GameCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            string error = ((GameCatalog)target).ValidationError;
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
