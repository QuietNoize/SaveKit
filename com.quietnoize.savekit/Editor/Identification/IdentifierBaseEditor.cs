namespace QuietNoize.SaveKit.Editor.Identification
{
#if UNITY_EDITOR
    using QuietNoize.SaveKit.Identification;
    using System;
    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(IdentifierBase), true)]
    internal class IdentifierBaseEditor : Editor
    {
        private GUIStyle m_idStyle;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIdHeader();

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                DrawPropertiesExcluding(
                    serializedObject,
                    "m_Script",
                    "m_id"
                );

                DrawAdditionalInspectorGUI();
            }

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.Space(6);

                EditorGUILayout.HelpBox(
                    "Identifier properties cannot be edited in Play Mode.",
                    MessageType.Info
                );
            }

            serializedObject.ApplyModifiedProperties();
        }

        #region Header

        protected virtual void DrawIdHeader()
        {
            InitStyles();

            var rect = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight
            );

            SerializedProperty idProperty = serializedObject.FindProperty("m_id");

            if (idProperty != null && idProperty.propertyType == SerializedPropertyType.String)
            {
                EditorGUI.BeginProperty(rect, GUIContent.none, idProperty);

                EditorGUI.BeginChangeCheck();

                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    EditorGUI.BeginProperty(rect, GUIContent.none, idProperty);

                    EditorGUI.BeginChangeCheck();

                    string value = EditorGUI.TextField(
                        rect,
                        idProperty.stringValue,
                        m_idStyle
                    );

                    if (EditorGUI.EndChangeCheck() && !EditorApplication.isPlaying)
                        idProperty.stringValue = value;

                    EditorGUI.EndProperty();
                }

                EditorGUI.EndProperty();
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.LabelField(rect, GetIdentifierString(), m_idStyle);
                }
            }

            EditorGUILayout.Space(4);

            var separator = EditorGUILayout.GetControlRect(false, 1f);

            EditorGUI.DrawRect(
                separator,
                EditorGUIUtility.isProSkin
                    ? new Color(0f, 0f, 0f, 0.35f)
                    : new Color(0f, 0f, 0f, 0.2f)
            );
        }

        private void InitStyles()
        {
            if (m_idStyle != null)
                return;

            m_idStyle = new GUIStyle(EditorStyles.textField)
            {
                alignment = TextAnchor.MiddleCenter,
                font = EditorStyles.label.font,
                fontSize = EditorStyles.label.fontSize,
                fontStyle = EditorStyles.label.fontStyle,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };

            m_idStyle.normal.background = null;
            m_idStyle.hover.background = null;
            m_idStyle.active.background = null;
            m_idStyle.focused.background = null;

            m_idStyle.onNormal.background = null;
            m_idStyle.onHover.background = null;
            m_idStyle.onActive.background = null;
            m_idStyle.onFocused.background = null;
        }

        protected virtual string GetIdentifierString()
        {
            var field = target.GetType().GetField(
                "m_id",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic
            );

            if (field == null)
                throw new InvalidOperationException(
                    $"Identifier '{target.GetType().Name}' must contain an m_id field."
                );

            object value = field.GetValue(target);

            if (value is byte[] bytes && bytes.Length == 16)
                return new Guid(bytes).ToString("D");

            if (value is string text)
                return string.IsNullOrEmpty(text) ? "Unassigned" : text;

            return "Unassigned";
        }

        #endregion

        #region Extensions

        protected virtual void DrawAdditionalInspectorGUI()
        {
        }

        #endregion
    }
#endif
}