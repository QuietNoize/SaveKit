namespace QuietNoize.SaveKit.Editor.Serialization
{
    using QuietNoize.SaveKit.Serialization;

    using System;
    using System.Collections.Generic;
    using System.Linq;

    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(ComponentSerializerDefinition), true)]
    internal sealed class ComponentSerializerDefinitionDrawer : PropertyDrawer
    {
        private const float Padding = 10f;
        private const float Spacing = 5f;

        private const float RemoveButtonWidth = 68f;
        private const float HelpBoxHeight = 40f;
        private const float TypeLabelWidth = 120f;
        private const float MemberSpacing = 2f;

        private static readonly Dictionary<EntityId, Dictionary<string, string>>
            s_filters = new();

        #region Property Height

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;

            float height = Padding;

            // Component selector.
            height += lineHeight + Spacing;

            if (GetComponent(property) == null)
            {
                height += HelpBoxHeight + Padding;
                return height;
            }

            // Local Number.
            height += lineHeight + Spacing;

            // Custom Definition Settings.
            List<SerializedProperty> customProperties =
                GetCustomProperties(property);

            if (customProperties.Count > 0)
            {
                height += lineHeight + Spacing;

                foreach (SerializedProperty customProperty in customProperties)
                {
                    height += EditorGUI.GetPropertyHeight(
                        customProperty,
                        true
                    ) + Spacing;
                }
            }

            // Separation before Members.
            height += Spacing;

            SerializedProperty membersProperty =
                property.FindPropertyRelative("m_members");

            if (membersProperty == null || !membersProperty.isArray)
            {
                height += HelpBoxHeight + Padding;
                return height;
            }

            // Members header, filter and selection buttons.
            height += (lineHeight + Spacing) * 3f;

            int visibleCount = GetVisibleMemberCount(
                membersProperty,
                GetFilter(property)
            );

            if (visibleCount == 0)
            {
                height += HelpBoxHeight;
            }
            else
            {
                height += visibleCount *
                          (lineHeight + MemberSpacing);
            }

            height += Padding;

            return height;
        }

        #endregion

        #region GUI

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            DrawCardBackground(position);

            Rect content = new Rect(
                position.x + Padding,
                position.y + Padding,
                position.width - Padding * 2f,
                position.height - Padding * 2f
            );

            float lineHeight = EditorGUIUtility.singleLineHeight;

            Component component = GetComponent(property);

            SerializedProperty componentProperty =
                property.FindPropertyRelative("m_component");

            bool removeRequested = DrawComponentSelector(
                new Rect(
                    content.x,
                    content.y,
                    content.width,
                    lineHeight
                ),
                property,
                componentProperty,
                component
            );

            content.y += lineHeight + Spacing;

            component = GetComponent(property);

            if (component == null)
            {
                EditorGUI.HelpBox(
                    new Rect(
                        content.x,
                        content.y,
                        content.width,
                        HelpBoxHeight
                    ),
                    "Select a component to configure its serializer definition.",
                    MessageType.Info
                );

                EditorGUI.EndProperty();

                if (removeRequested)
                    RemoveDefinition(property);

                return;
            }

            DrawLocalNumber(
                new Rect(
                    content.x,
                    content.y,
                    content.width,
                    lineHeight
                ),
                property.FindPropertyRelative("m_localNumber")
            );

            content.y += lineHeight + Spacing;

            content.y = DrawCustomProperties(
                content,
                property
            );

            content.y += Spacing;

            DrawMembers(
                content,
                property.FindPropertyRelative("m_members"),
                property
            );

            EditorGUI.EndProperty();

            if (removeRequested)
                RemoveDefinition(property);
        }

        private static void DrawCardBackground(Rect position)
        {
            GUI.Box(
                position,
                GUIContent.none,
                EditorStyles.helpBox
            );
        }

        #endregion

        #region Component Selector

        private bool DrawComponentSelector(
            Rect position,
            SerializedProperty definitionProperty,
            SerializedProperty componentProperty,
            Component currentComponent)
        {
            Rect popupRect = new Rect(
                position.x,
                position.y,
                position.width - RemoveButtonWidth - Spacing,
                position.height
            );

            Rect removeRect = new Rect(
                popupRect.xMax + Spacing,
                position.y,
                RemoveButtonWidth,
                position.height
            );

            if (componentProperty == null)
            {
                EditorGUI.LabelField(
                    popupRect,
                    "Component",
                    "Missing component reference."
                );
            }
            else
            {
                SerializedProperty definitions =
                    GetDefinitionsArray(
                        definitionProperty,
                        out int currentIndex
                    );

                GameObject gameObject =
                    definitionProperty.serializedObject.targetObject
                        is GameObjectSaveHandler handler
                        ? handler.gameObject
                        : currentComponent != null
                            ? currentComponent.gameObject
                            : null;

                List<Component> availableComponents =
                    ComponentSerializerDefinitionEditorUtility
                        .GetAvailableComponents(
                            gameObject,
                            definitions,
                            currentIndex,
                            currentComponent
                        );

                var options = new List<string>
                {
                    "— Select Component —"
                };

                foreach (Component component in availableComponents)
                {
                    options.Add(
                        ComponentSerializerDefinitionEditorUtility
                            .GetComponentDisplayName(
                                component,
                                availableComponents
                            )
                    );
                }

                int selectedIndex = currentComponent == null
                    ? 0
                    : availableComponents.IndexOf(currentComponent) + 1;

                selectedIndex = Mathf.Clamp(
                    selectedIndex,
                    0,
                    options.Count - 1
                );

                EditorGUI.BeginChangeCheck();

                int newIndex = EditorGUI.Popup(
                    popupRect,
                    "Component",
                    selectedIndex,
                    options.ToArray()
                );

                if (EditorGUI.EndChangeCheck() &&
                    newIndex > 0 &&
                    newIndex != selectedIndex)
                {
                    ChangeComponent(
                        definitionProperty,
                        componentProperty,
                        currentComponent,
                        availableComponents[newIndex - 1],
                        definitions,
                        currentIndex
                    );
                }
            }

            Color previousColor = GUI.backgroundColor;

            GUI.backgroundColor = new Color(
                0.85f,
                0.35f,
                0.35f
            );

            bool removeRequested = GUI.Button(
                removeRect,
                "Remove",
                EditorStyles.miniButton
            );

            GUI.backgroundColor = previousColor;

            return removeRequested;
        }

        private void ChangeComponent(
            SerializedProperty definitionProperty,
            SerializedProperty componentProperty,
            Component currentComponent,
            Component selectedComponent,
            SerializedProperty definitions,
            int currentIndex)
        {
            if (selectedComponent == null ||
                selectedComponent == currentComponent)
            {
                return;
            }

            Type selectedType = selectedComponent.GetType();

            // Switching between instances of the same type
            // does not require recreating the definition.
            if (currentComponent != null &&
                currentComponent.GetType() == selectedType)
            {
                componentProperty.objectReferenceValue =
                    selectedComponent;

                return;
            }

            if (!ComponentSerializerDefinitionEditorUtility
                .CanCreateDefinition(selectedType))
            {
                return;
            }

            uint localNumber =
                ComponentSerializerDefinitionEditorUtility.GetNextLocalNumber(
                    definitions,
                    selectedType,
                    currentIndex
                );

            ComponentSerializerDefinition definition =
                ComponentSerializerDefinitionEditorUtility.CreateDefinition(
                    selectedComponent,
                    localNumber
                );

            if (definition == null)
            {
                Debug.LogWarning(
                    $"Unable to create a serializer definition for " +
                    $"'{selectedType.FullName}'.",
                    selectedComponent
                );

                return;
            }

            definitionProperty.managedReferenceValue = definition;
        }

        private static void DrawLocalNumber(
            Rect position,
            SerializedProperty property)
        {
            if (property == null)
                return;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(
                    position,
                    property,
                    new GUIContent(
                        "Local Number",
                        "Unique number among serialized components of the same type."
                    )
                );
            }
        }

        #endregion

        #region Custom Definition Properties

        private float DrawCustomProperties(
            Rect position,
            SerializedProperty definitionProperty)
        {
            List<SerializedProperty> properties =
                GetCustomProperties(definitionProperty);

            if (properties.Count == 0)
                return position.y;

            EditorGUI.LabelField(
                new Rect(
                    position.x,
                    position.y,
                    position.width,
                    EditorGUIUtility.singleLineHeight
                ),
                "Definition Settings",
                EditorStyles.boldLabel
            );

            position.y += EditorGUIUtility.singleLineHeight + Spacing;

            foreach (SerializedProperty property in properties)
            {
                float propertyHeight = EditorGUI.GetPropertyHeight(
                    property,
                    true
                );

                EditorGUI.PropertyField(
                    new Rect(
                        position.x,
                        position.y,
                        position.width,
                        propertyHeight
                    ),
                    property,
                    true
                );

                position.y += propertyHeight + Spacing;
            }

            return position.y;
        }

        private static List<SerializedProperty> GetCustomProperties(
            SerializedProperty definitionProperty)
        {
            var result = new List<SerializedProperty>();

            SerializedProperty iterator = definitionProperty.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.depth != definitionProperty.depth + 1)
                    continue;

                if (iterator.name == "m_component" ||
                    iterator.name == "m_members" ||
                    iterator.name == "m_localNumber")
                {
                    continue;
                }

                result.Add(iterator.Copy());
            }

            return result;
        }

        #endregion

        #region Members

        private void DrawMembers(
            Rect position,
            SerializedProperty membersProperty,
            SerializedProperty definitionProperty)
        {
            if (membersProperty == null || !membersProperty.isArray)
            {
                EditorGUI.HelpBox(
                    new Rect(
                        position.x,
                        position.y,
                        position.width,
                        HelpBoxHeight
                    ),
                    "Member definitions were not found.",
                    MessageType.Error
                );

                return;
            }

            float lineHeight = EditorGUIUtility.singleLineHeight;

            EditorGUI.LabelField(
                new Rect(
                    position.x,
                    position.y,
                    position.width,
                    lineHeight
                ),
                "Members to Save",
                EditorStyles.boldLabel
            );

            position.y += lineHeight + Spacing;

            string filter = GetFilter(definitionProperty);

            filter = EditorGUI.TextField(
                new Rect(
                    position.x,
                    position.y,
                    position.width,
                    lineHeight
                ),
                "Filter",
                filter
            );

            SetFilter(definitionProperty, filter);

            position.y += lineHeight + Spacing;

            float buttonWidth = (position.width - Spacing) / 2f;

            if (GUI.Button(
                new Rect(
                    position.x,
                    position.y,
                    buttonWidth,
                    lineHeight
                ),
                "Select All",
                EditorStyles.miniButtonLeft))
            {
                SetAllMembersSelected(membersProperty, true);
            }

            if (GUI.Button(
                new Rect(
                    position.x + buttonWidth + Spacing,
                    position.y,
                    buttonWidth,
                    lineHeight
                ),
                "Deselect All",
                EditorStyles.miniButtonRight))
            {
                SetAllMembersSelected(membersProperty, false);
            }

            position.y += lineHeight + Spacing;

            bool anyShown = false;

            for (int i = 0; i < membersProperty.arraySize; i++)
            {
                SerializedProperty member =
                    membersProperty.GetArrayElementAtIndex(i);

                SerializedProperty nameProperty =
                    member.FindPropertyRelative("m_name");

                SerializedProperty typeProperty =
                    member.FindPropertyRelative("m_memberType");

                SerializedProperty selectedProperty =
                    member.FindPropertyRelative("m_isSelected");

                if (nameProperty == null ||
                    typeProperty == null ||
                    selectedProperty == null)
                {
                    continue;
                }

                string memberName = nameProperty.stringValue;

                Type memberType = Type.GetType(
                    typeProperty.stringValue
                );

                if (!MatchesFilter(memberName, memberType, filter))
                    continue;

                anyShown = true;

                DrawMemberToggle(
                    new Rect(
                        position.x,
                        position.y,
                        position.width,
                        lineHeight
                    ),
                    nameProperty,
                    typeProperty,
                    selectedProperty,
                    memberType
                );

                position.y += lineHeight + MemberSpacing;
            }

            if (!anyShown)
            {
                EditorGUI.HelpBox(
                    new Rect(
                        position.x,
                        position.y,
                        position.width,
                        HelpBoxHeight
                    ),
                    string.IsNullOrEmpty(filter)
                        ? "This component has no serializable members."
                        : "No members match your search.",
                    MessageType.Info
                );
            }
        }

        private static void DrawMemberToggle(
            Rect position,
            SerializedProperty nameProperty,
            SerializedProperty typeProperty,
            SerializedProperty selectedProperty,
            Type memberType)
        {
            string memberName = nameProperty.stringValue;

            float typeWidth = Mathf.Min(
                TypeLabelWidth,
                position.width * 0.4f
            );

            Rect typeRect = new Rect(
                position.x,
                position.y,
                typeWidth,
                position.height
            );

            Rect toggleRect = new Rect(
                typeRect.xMax + Spacing,
                position.y,
                position.width - typeWidth - Spacing,
                position.height
            );

            EditorGUI.LabelField(
                typeRect,
                memberType != null
                    ? GetFriendlyTypeName(memberType)
                    : "Unknown",
                EditorStyles.miniLabel
            );

            Color previousColor = GUI.backgroundColor;

            if (selectedProperty.boolValue)
            {
                GUI.backgroundColor = EditorGUIUtility.isProSkin
                    ? new Color(0.65f, 0.9f, 0.65f)
                    : new Color(0.8f, 1f, 0.8f);
            }

            selectedProperty.boolValue = EditorGUI.ToggleLeft(
                toggleRect,
                new GUIContent(
                    ObjectNames.NicifyVariableName(memberName),
                    $"{typeProperty.stringValue}\n{memberName}"
                ),
                selectedProperty.boolValue
            );

            GUI.backgroundColor = previousColor;
        }

        private static void SetAllMembersSelected(
            SerializedProperty membersProperty,
            bool selected)
        {
            for (int i = 0; i < membersProperty.arraySize; i++)
            {
                SerializedProperty member =
                    membersProperty.GetArrayElementAtIndex(i);

                SerializedProperty selectedProperty =
                    member.FindPropertyRelative("m_isSelected");

                if (selectedProperty != null)
                    selectedProperty.boolValue = selected;
            }
        }

        private static int GetVisibleMemberCount(
            SerializedProperty membersProperty,
            string filter)
        {
            int count = 0;

            for (int i = 0; i < membersProperty.arraySize; i++)
            {
                SerializedProperty member =
                    membersProperty.GetArrayElementAtIndex(i);

                SerializedProperty nameProperty =
                    member.FindPropertyRelative("m_name");

                SerializedProperty typeProperty =
                    member.FindPropertyRelative("m_memberType");

                if (nameProperty == null || typeProperty == null)
                    continue;

                Type memberType = Type.GetType(
                    typeProperty.stringValue
                );

                if (MatchesFilter(
                    nameProperty.stringValue,
                    memberType,
                    filter))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool MatchesFilter(
            string memberName,
            Type memberType,
            string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            bool matchesName = memberName.IndexOf(
                filter,
                StringComparison.OrdinalIgnoreCase
            ) >= 0;

            bool matchesType = memberType != null &&
                memberType.Name.IndexOf(
                    filter,
                    StringComparison.OrdinalIgnoreCase
                ) >= 0;

            return matchesName || matchesType;
        }

        #endregion

        #region Filtering

        private static Dictionary<string, string> GetTargetFilters(
            SerializedProperty property,
            bool create)
        {
            UnityEngine.Object targetObject =
                property.serializedObject.targetObject;

            if (targetObject == null)
                return null;

            EntityId entityId = targetObject.GetEntityId();

            if (s_filters.TryGetValue(entityId, out var filters))
                return filters;

            if (!create)
                return null;

            filters = new Dictionary<string, string>();

            s_filters.Add(entityId, filters);

            return filters;
        }

        private static string GetFilter(SerializedProperty property)
        {
            Dictionary<string, string> filters =
                GetTargetFilters(property, false);

            if (filters == null)
                return string.Empty;

            return filters.TryGetValue(
                property.propertyPath,
                out string filter)
                ? filter
                : string.Empty;
        }

        private static void SetFilter(
            SerializedProperty property,
            string filter)
        {
            Dictionary<string, string> filters =
                GetTargetFilters(property, true);

            if (filters == null)
                return;

            filters[property.propertyPath] = filter;
        }

        #endregion

        #region Definition Management

        private static void RemoveDefinition(
            SerializedProperty definitionProperty)
        {
            if (!TryGetDefinitionsArray(
                definitionProperty,
                out SerializedProperty definitions,
                out int index))
            {
                return;
            }

            Undo.RecordObject(
                definitionProperty.serializedObject.targetObject,
                "Remove Component Serializer Definition"
            );

            definitions.DeleteArrayElementAtIndex(index);

            definitionProperty.serializedObject.ApplyModifiedProperties();

            GUIUtility.ExitGUI();
        }

        private static SerializedProperty GetDefinitionsArray(
            SerializedProperty definitionProperty,
            out int index)
        {
            return TryGetDefinitionsArray(
                definitionProperty,
                out SerializedProperty definitions,
                out index)
                ? definitions
                : null;
        }

        private static bool TryGetDefinitionsArray(
            SerializedProperty definitionProperty,
            out SerializedProperty definitions,
            out int index)
        {
            definitions = null;
            index = -1;

            const string marker = ".Array.data[";

            string path = definitionProperty.propertyPath;

            int markerIndex = path.LastIndexOf(
                marker,
                StringComparison.Ordinal
            );

            if (markerIndex < 0)
                return false;

            int indexStart = markerIndex + marker.Length;

            int indexEnd = path.IndexOf(']', indexStart);

            if (indexEnd < 0 ||
                !int.TryParse(
                    path.Substring(
                        indexStart,
                        indexEnd - indexStart
                    ),
                    out index))
            {
                return false;
            }

            string arrayPath = path.Substring(0, markerIndex);

            definitions =
                definitionProperty.serializedObject.FindProperty(arrayPath);

            return definitions != null && definitions.isArray;
        }

        #endregion

        #region Utilities

        private static Component GetComponent(
            SerializedProperty property)
        {
            return property
                .FindPropertyRelative("m_component")
                ?.objectReferenceValue as Component;
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type == null)
                return "Unknown";

            if (type == typeof(bool)) return "bool";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(char)) return "char";
            if (type == typeof(short)) return "short";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(string)) return "string";
            if (type == typeof(object)) return "object";

            if (type.IsArray)
            {
                return GetFriendlyTypeName(type.GetElementType()) + "[]";
            }

            if (type.IsGenericType)
            {
                string typeName = type.Name;

                int backtickIndex = typeName.IndexOf('`');

                if (backtickIndex > 0)
                {
                    typeName = typeName.Substring(
                        0,
                        backtickIndex
                    );
                }

                string[] arguments = type.GetGenericArguments()
                    .Select(GetFriendlyTypeName)
                    .ToArray();

                return $"{typeName}<{string.Join(", ", arguments)}>";
            }

            return type.Name;
        }

        #endregion
    }
}
