namespace QuietNoize.SaveKit.Editor
{
    using QuietNoize.SaveKit.Serialization;
    using QuietNoize.SaveKit.Identification;
    using QuietNoize.SaveKit.Editor.Serialization;

    using System;
    using System.Collections.Generic;
    using System.IO;

    using UnityEditor;
    using UnityEngine;

    [CustomEditor(typeof(GameObjectSaveHandler))]
    internal sealed class GameObjectSaveHandlerEditor : Editor
    {
        private const float SectionSpacing = 10f;
        private const float AddButtonHeight = 34f;

        private GUIStyle m_sectionStyle;
        private GUIStyle m_addComponentButtonStyle;

        private SerializedProperty m_identifier;
        private SerializedProperty m_componentDefinitions;
        private SerializedProperty m_prefabPath;
        private SerializedProperty m_order;

        #region Unity Events

        private void OnEnable()
        {
            m_identifier = serializedObject.FindProperty("m_identifier");

            m_componentDefinitions =
                serializedObject.FindProperty("m_componentDefinitions");

            m_prefabPath = serializedObject.FindProperty("m_prefabPath");
            m_order = serializedObject.FindProperty("m_order");
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            InitStyles();

            serializedObject.Update();

            ComponentSerializerDefinitionEditorUtility
                .EnsureUniqueLocalNumbers(m_componentDefinitions);

            DrawGeneralSettings();

            DrawPrefabSettings();

            DrawComponentDefinitions();

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Styles

        private void InitStyles()
        {
            if (m_sectionStyle != null)
                return;

            m_sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };

            m_addComponentButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = AddButtonHeight
            };
        }

        #endregion

        #region General Settings

        private void DrawGeneralSettings()
        {
            DrawSectionHeader("General Settings");

            EditorGUILayout.BeginVertical(m_sectionStyle);

            DrawIdentifierField();

            if (m_order != null)
            {
                EditorGUILayout.PropertyField(
                    m_order,
                    new GUIContent(
                        "Load Order",
                        "Objects with higher load order values are processed earlier."
                    )
                );
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawIdentifierField()
        {
            if (m_identifier == null)
            {
                EditorGUILayout.HelpBox(
                    "Identifier property was not found.",
                    MessageType.Error
                );

                return;
            }

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(
                m_identifier,
                new GUIContent(
                    "Identifier",
                    "Unique identifier used to track this GameObject."
                )
            );

            if (m_identifier.objectReferenceValue == null)
            {
                if (GUILayout.Button(
                    "+",
                    EditorStyles.miniButton,
                    GUILayout.Width(28f),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                {
                    ShowAddIdentifierMenu(
                        EditorGUILayout.GetControlRect(false, 0f),
                        target as GameObjectSaveHandler
                    );
                }
            }

            EditorGUILayout.EndHorizontal();

            if (m_identifier.objectReferenceValue == null)
            {
                EditorGUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    "This GameObject does not have an Identifier assigned. " +
                    "Without an Identifier, SaveKit cannot save or track this object. " +
                    "Add an Identifier component to enable serialization.",
                    MessageType.Warning
                );
            }
        }

        #endregion

        #region Prefab Settings

        private void DrawPrefabSettings()
        {
            DrawSectionHeader("Prefab Settings");

            EditorGUILayout.BeginVertical(m_sectionStyle);

            if (m_prefabPath == null)
            {
                EditorGUILayout.HelpBox(
                    "Prefab path property was not found.",
                    MessageType.Error
                );

                EditorGUILayout.EndVertical();
                return;
            }

            GameObject prefab = string.IsNullOrEmpty(m_prefabPath.stringValue)
                ? null
                : Resources.Load<GameObject>(m_prefabPath.stringValue);

            EditorGUI.BeginChangeCheck();

            GameObject selectedPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Prefab",
                    "Prefab used to recreate this GameObject. It must be located inside a Resources folder."
                ),
                prefab,
                typeof(GameObject),
                false
            );

            if (EditorGUI.EndChangeCheck())
            {
                m_prefabPath.stringValue =
                    GetPrefabResourcePath(selectedPrefab);
            }

            if (prefab == null &&
                !string.IsNullOrEmpty(m_prefabPath.stringValue))
            {
                EditorGUILayout.Space(4);

                EditorGUILayout.HelpBox(
                    "The specified prefab could not be found in Resources. " +
                    "Check its path or select another prefab.",
                    MessageType.Warning
                );
            }

            EditorGUILayout.EndVertical();
        }

        private static string GetPrefabResourcePath(GameObject prefab)
        {
            if (prefab == null)
                return string.Empty;

            string assetPath = AssetDatabase.GetAssetPath(prefab);

            const string resourcesPath = "Assets/Resources/";

            if (!assetPath.StartsWith(
                resourcesPath,
                StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    "Prefab must be located inside an Assets/Resources folder."
                );

                return string.Empty;
            }

            string path = assetPath.Substring(resourcesPath.Length);

            return Path.ChangeExtension(path, null)
                .Replace('\\', '/');
        }

        #endregion

        #region Component Definitions

        private void DrawComponentDefinitions()
        {
            DrawSectionHeader("Components to Save");

            if (m_componentDefinitions == null)
            {
                EditorGUILayout.HelpBox(
                    "Cannot find 'm_componentDefinitions' property.",
                    MessageType.Error
                );

                return;
            }

            if (m_componentDefinitions.arraySize == 0)
            {
                EditorGUILayout.BeginVertical(m_sectionStyle);

                EditorGUILayout.HelpBox(
                    "No components are currently registered for serialization.\n\n" +
                    "Add a component below to start configuring its members.",
                    MessageType.Info
                );

                EditorGUILayout.EndVertical();
            }

            for (int i = 0; i < m_componentDefinitions.arraySize; i++)
            {
                SerializedProperty element =
                    m_componentDefinitions.GetArrayElementAtIndex(i);

                EditorGUILayout.PropertyField(
                    element,
                    GUIContent.none,
                    true
                );

                EditorGUILayout.Space(SectionSpacing);
            }

            DrawAddComponentControl();
        }

        #endregion

        #region Add Component

        private void DrawAddComponentControl()
        {
            GameObjectSaveHandler handler =
                target as GameObjectSaveHandler;

            if (handler == null)
                return;

            EditorGUILayout.BeginVertical(m_sectionStyle);

            Rect buttonRect = EditorGUILayout.GetControlRect(
                false,
                AddButtonHeight,
                GUILayout.ExpandWidth(true)
            );

            if (GUI.Button(
                buttonRect,
                new GUIContent(
                    "+  Add Component",
                    "Select a component to add to the serialization definitions."
                ),
                m_addComponentButtonStyle))
            {
                ShowAddComponentMenu(buttonRect, handler);
            }

            EditorGUILayout.EndVertical();
        }

        private void ShowAddComponentMenu(
            Rect position,
            GameObjectSaveHandler handler)
        {
            GenericMenu menu = new GenericMenu();

            if (handler == null)
                return;

            List<Component> availableComponents =
                ComponentSerializerDefinitionEditorUtility.GetAvailableComponents(
                    handler.gameObject,
                    m_componentDefinitions
                );

            if (availableComponents.Count == 0)
            {
                menu.AddDisabledItem(
                    new GUIContent(
                        "No additional components available"
                    )
                );

                menu.DropDown(position);
                return;
            }

            for (int i = 0; i < availableComponents.Count; i++)
            {
                Component component = availableComponents[i];

                string displayName =
                    ComponentSerializerDefinitionEditorUtility
                        .GetComponentDisplayName(
                            component,
                            availableComponents
                        );

                menu.AddItem(
                    new GUIContent(displayName),
                    false,
                    () => AddComponentDefinition(handler, component)
                );
            }

            menu.DropDown(position);
        }

        private void AddComponentDefinition(
            GameObjectSaveHandler handler,
            Component component)
        {
            if (handler == null || component == null)
                return;

            Type componentType = component.GetType();

            if (!ComponentSerializerDefinitionEditorUtility
                .CanCreateDefinition(componentType))
            {
                return;
            }

            // Use a fresh SerializedObject because the menu callback
            // is executed after the original inspector GUI pass.
            SerializedObject handlerObject = new SerializedObject(handler);

            handlerObject.Update();

            SerializedProperty componentDefinitions =
                handlerObject.FindProperty("m_componentDefinitions");

            if (componentDefinitions == null)
            {
                Debug.LogError(
                    "Cannot find 'm_componentDefinitions' property.",
                    handler
                );

                return;
            }

            ComponentSerializerDefinitionEditorUtility
                .EnsureUniqueLocalNumbers(componentDefinitions);

            uint localNumber =
                ComponentSerializerDefinitionEditorUtility.GetNextLocalNumber(
                    componentDefinitions,
                    componentType
                );

            ComponentSerializerDefinition definition =
                ComponentSerializerDefinitionEditorUtility.CreateDefinition(
                    component,
                    localNumber
                );

            if (definition == null)
            {
                Debug.LogWarning(
                    $"Unable to create a serializer definition for " +
                    $"'{componentType.FullName}'.",
                    component
                );

                return;
            }

            int index = componentDefinitions.arraySize;

            componentDefinitions.InsertArrayElementAtIndex(index);

            SerializedProperty element =
                componentDefinitions.GetArrayElementAtIndex(index);

            element.managedReferenceValue = definition;

            handlerObject.ApplyModifiedProperties();

            serializedObject.Update();

            Repaint();
        }

        #endregion

        #region Layout

        private static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(SectionSpacing);

            EditorGUILayout.LabelField(
                title,
                EditorStyles.boldLabel
            );

            EditorGUILayout.Space(4);
        }

        #endregion

        #region Add Identifier

        private void ShowAddIdentifierMenu(
            Rect position,
            GameObjectSaveHandler handler)
        {
            if (handler == null)
                return;

            GenericMenu menu = new GenericMenu();

            List<Type> identifierTypes = new List<Type>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IdentifierBase>())
            {
                if (type.IsAbstract || type.ContainsGenericParameters)
                    continue;

                if (!typeof(IdentifierBase).IsAssignableFrom(type))
                    continue;

                identifierTypes.Add(type);
            }

            identifierTypes.Sort(
                (a, b) => string.Compare(
                    a.Name,
                    b.Name,
                    StringComparison.Ordinal
                )
            );

            if (identifierTypes.Count == 0)
            {
                menu.AddDisabledItem(
                    new GUIContent("No Identifier implementations available")
                );

                menu.DropDown(position);
                return;
            }

            foreach (Type identifierType in identifierTypes)
            {
                menu.AddItem(
                    new GUIContent(ObjectNames.NicifyVariableName(identifierType.Name)),
                    false,
                    () => AddIdentifier(handler, identifierType)
                );
            }

            menu.DropDown(position);
        }

        private void AddIdentifier(
            GameObjectSaveHandler handler,
            Type identifierType)
        {
            if (handler == null || identifierType == null)
                return;

            IdentifierBase identifier =
                handler.GetComponent(identifierType) as IdentifierBase;

            if (identifier == null)
            {
                identifier = Undo.AddComponent(
                    handler.gameObject,
                    identifierType
                ) as IdentifierBase;
            }

            if (identifier == null)
                return;

            SerializedObject handlerObject = new SerializedObject(handler);

            handlerObject.Update();

            SerializedProperty identifierProperty =
                handlerObject.FindProperty("m_identifier");

            if (identifierProperty == null)
            {
                Debug.LogError(
                    "Cannot find 'm_identifier' property.",
                    handler
                );

                return;
            }

            identifierProperty.objectReferenceValue = identifier;

            handlerObject.ApplyModifiedProperties();

            serializedObject.Update();

            Repaint();
        }

        #endregion
    }
}