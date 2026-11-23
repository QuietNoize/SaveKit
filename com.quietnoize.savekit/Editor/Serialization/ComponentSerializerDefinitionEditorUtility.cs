namespace QuietNoize.SaveKit.Editor.Serialization
{
    using QuietNoize.SaveKit.Identification;
    using QuietNoize.SaveKit.Serialization;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using UnityEngine;
    using UnityEditor;

    internal static class ComponentSerializerDefinitionEditorUtility
    {
        private static readonly Dictionary<Type, bool> s_availability = new();

        #region Definition Creation

        internal static bool CanCreateDefinition(Type componentType)
        {
            if (componentType == null ||
                !typeof(Component).IsAssignableFrom(componentType) ||
                componentType.IsAbstract)
            {
                return false;
            }

            if (s_availability.TryGetValue(componentType, out bool result))
                return result;

            Type genericType = typeof(ComponentSerializerDefinition<>)
                .MakeGenericType(componentType);

            MethodInfo method = genericType.GetMethod(
                "CanCreate",
                BindingFlags.Static | BindingFlags.NonPublic
            );

            if (method == null)
            {
                Debug.LogError(
                    $"[{nameof(ComponentSerializerDefinitionEditorUtility)}] " +
                    $"Could not find CanCreate in '{genericType.FullName}'."
                );

                s_availability[componentType] = false;
                return false;
            }

            try
            {
                result = (bool)method.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
                result = false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = false;
            }

            s_availability[componentType] = result;

            return result;
        }

        internal static ComponentSerializerDefinition CreateDefinition(
            Component component,
            uint localNumber)
        {
            if (component == null)
                return null;

            Type componentType = component.GetType();

            Type genericType = typeof(ComponentSerializerDefinition<>)
                .MakeGenericType(componentType);

            MethodInfo method = genericType.GetMethod(
                "TryCreate",
                BindingFlags.Static | BindingFlags.NonPublic
            );

            if (method == null)
            {
                Debug.LogError(
                    $"[{nameof(ComponentSerializerDefinitionEditorUtility)}] " +
                    $"Could not find TryCreate in '{genericType.FullName}'."
                );

                return null;
            }

            object[] arguments =
            {
                component,
                localNumber,
                null
            };

            try
            {
                bool success = (bool)method.Invoke(null, arguments);

                return success
                    ? arguments[2] as ComponentSerializerDefinition
                    : null;
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return null;
            }
        }

        #endregion

        #region Component Selection

        internal static List<Component> GetAvailableComponents(
            GameObject gameObject,
            SerializedProperty definitions,
            int currentIndex = -1,
            Component currentComponent = null)
        {
            var result = new List<Component>();

            if (gameObject == null)
                return result;

            Component[] components = gameObject.GetComponents<Component>();

            foreach (Component component in components)
            {
                if (component == null ||
                    component is GameObjectSaveHandler ||
                    component is IdentifierBase)
                {
                    continue;
                }

                // Keep the currently selected component available,
                // even if its definition is no longer discoverable.
                if (component != currentComponent &&
                    !CanCreateDefinition(component.GetType()))
                {
                    continue;
                }

                bool alreadyAdded = false;

                if (definitions != null)
                {
                    for (int i = 0; i < definitions.arraySize; i++)
                    {
                        if (i == currentIndex)
                            continue;

                        SerializedProperty element =
                            definitions.GetArrayElementAtIndex(i);

                        if (element.managedReferenceValue == null)
                            continue;

                        SerializedProperty componentProperty =
                            element.FindPropertyRelative("m_component");

                        if (componentProperty?.objectReferenceValue == component)
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }
                }

                if (!alreadyAdded || component == currentComponent)
                    result.Add(component);
            }

            return result;
        }

        internal static string GetComponentDisplayName(
            Component component,
            List<Component> availableComponents)
        {
            if (component == null)
                return "Missing Component";

            Type componentType = component.GetType();

            List<Component> sameTypeComponents = availableComponents
                .Where(c => c != null && c.GetType() == componentType)
                .ToList();

            if (sameTypeComponents.Count <= 1)
                return componentType.Name;

            int index = sameTypeComponents.IndexOf(component) + 1;

            return $"{componentType.Name} ({index})";
        }

        #endregion

        #region Local Number

        internal static uint GetNextLocalNumber(
            SerializedProperty definitions,
            Type componentType,
            int excludedIndex = -1)
        {
            if (definitions == null || componentType == null)
                return 0;

            var usedNumbers = new HashSet<uint>();

            for (int i = 0; i < definitions.arraySize; i++)
            {
                if (i == excludedIndex)
                    continue;

                SerializedProperty element =
                    definitions.GetArrayElementAtIndex(i);

                if (element.managedReferenceValue == null)
                    continue;

                SerializedProperty componentProperty =
                    element.FindPropertyRelative("m_component");

                SerializedProperty numberProperty =
                    element.FindPropertyRelative("m_localNumber");

                Component component =
                    componentProperty?.objectReferenceValue as Component;

                if (component == null || numberProperty == null)
                    continue;

                if (component.GetType() != componentType)
                    continue;

                usedNumbers.Add(numberProperty.uintValue);
            }

            return GetFirstAvailableNumber(usedNumbers);
        }

        internal static bool EnsureUniqueLocalNumbers(
            SerializedProperty definitions)
        {
            if (definitions == null || !definitions.isArray)
                return false;

            var usedNumbers = new Dictionary<Type, HashSet<uint>>();

            bool changed = false;

            for (int i = 0; i < definitions.arraySize; i++)
            {
                SerializedProperty element =
                    definitions.GetArrayElementAtIndex(i);

                if (element.managedReferenceValue == null)
                    continue;

                SerializedProperty componentProperty =
                    element.FindPropertyRelative("m_component");

                SerializedProperty numberProperty =
                    element.FindPropertyRelative("m_localNumber");

                Component component =
                    componentProperty?.objectReferenceValue as Component;

                if (component == null || numberProperty == null)
                    continue;

                Type componentType = component.GetType();

                if (!usedNumbers.TryGetValue(
                    componentType,
                    out HashSet<uint> used))
                {
                    used = new HashSet<uint>();
                    usedNumbers.Add(componentType, used);
                }

                uint currentNumber = numberProperty.uintValue;

                if (used.Add(currentNumber))
                    continue;

                uint newNumber = GetFirstAvailableNumber(used);

                numberProperty.uintValue = newNumber;

                used.Add(newNumber);

                changed = true;
            }

            return changed;
        }

        private static uint GetFirstAvailableNumber(
            HashSet<uint> usedNumbers)
        {
            uint candidate = 0;

            while (usedNumbers.Contains(candidate))
            {
                if (candidate == uint.MaxValue)
                {
                    throw new InvalidOperationException(
                        "No available local numbers remain."
                    );
                }

                candidate++;
            }

            return candidate;
        }

        #endregion
    }
}
