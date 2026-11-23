namespace QuietNoize.SaveKit.Editor.Identification
{
#if UNITY_EDITOR
    using QuietNoize.SaveKit.Identification;
    using UnityEditor;

    [CustomEditor(typeof(GuidIdentifier))]
    internal sealed class GuidIdentifierEditor : IdentifierBaseEditor
    {
        protected override void DrawAdditionalInspectorGUI()
        {
            if (!IsPrefabAssetOrStage())
                return;

            DrawUniqueInstanceInfo();
        }

        private bool IsPrefabAssetOrStage()
        {
            var component = (GuidIdentifier)target;

            if (PrefabUtility.IsPartOfPrefabAsset(component.gameObject))
                return true;

            return UnityEditor.SceneManagement.PrefabStageUtility
                .GetPrefabStage(component.gameObject) != null;
        }

        private void DrawUniqueInstanceInfo()
        {
            SerializedProperty property =
                serializedObject.FindProperty("m_isUniqueInstance");

            if (property == null)
                return;

            EditorGUILayout.Space(8);

            if (property.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Unique Instance is enabled.\n\n" +
                    "This prefab must have only one instance in the game. " +
                    "Creating another instance will result in duplicated GUIDs " +
                    "and an identifier collision warning in the console.\n\n" +
                    "Use this option only for unique objects, such as Player.",
                    MessageType.Warning
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Enable Unique Instance only for prefabs that represent " +
                    "a single unique object in the game. Ordinary prefab " +
                    "instances should receive their own identifiers.",
                    MessageType.Info
                );
            }
        }
    }
#endif
}