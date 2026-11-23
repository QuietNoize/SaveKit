namespace QuietNoize.SaveKit.Internal.Serialization
{
    using QuietNoize.SaveKit.Internal.Data;
    using System;
    using System.Collections;
    using System.Reflection;
    using UnityEngine;

    [Serializable]
    internal sealed class MemberSerializer
    {
        [SerializeField] private string m_name;
        [SerializeField] private string m_memberType;

        [NonSerialized] private object m_initialValue;

        internal string Name => m_name;

        internal Type MemberType => Type.GetType(m_memberType);

        internal MemberSerializer() { }

        internal MemberSerializer(string name, Type type)
        {
            m_name = name;
            m_memberType = type.AssemblyQualifiedName;
        }

        internal void OnAwake(Component component)
        {
            if (!TryGetCompatibleMember(component, out MemberInfo memberInfo))
                return;

            m_initialValue = GetMemberValue(component, memberInfo);
        }

        internal object GetValue(Component component)
        {
            if (!TryGetCompatibleMember(component, out MemberInfo memberInfo))
                return null;

            return GetMemberValue(component, memberInfo);
        }

        internal bool IsDirty(object value)
        {
            Type memberType = MemberType;
            Type valueType = value?.GetType();

            bool isCollection =
                IsCollection(memberType) ||
                IsCollection(valueType);

            if (isCollection)
                return m_initialValue != null || value != null;

            return !Equals(m_initialValue, value);
        }

        internal MemberSnapshotBase ToSnapshot(object value)
        {
            Type memberType = MemberType;
            Type valueType = value?.GetType();

            bool isCollection =
                IsCollection(memberType) ||
                IsCollection(valueType);

            if (isCollection)
            {
                return new CollectionSnapshot(
                    m_name,
                    m_memberType,
                    value
                );
            }

            return new ValueSnapshot(
                m_name,
                m_memberType,
                value
            );
        }

        internal void ApplySnapshot(
            Component component,
            MemberSnapshotBase snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning(
                    $"[{nameof(MemberSerializer)}] Snapshot is null for member '{m_name}'.",
                    component
                );

                return;
            }

            if (!TryGetCompatibleMember(component, out MemberInfo memberInfo))
                return;

            object value = snapshot.Value;

            Type memberType = MemberType;

            Type nullableType = memberType != null
                ? Nullable.GetUnderlyingType(memberType)
                : null;

            Type expectedType = nullableType ?? memberType;

            if (value == null &&
                memberType != null &&
                memberType.IsValueType &&
                nullableType == null)
            {
                Debug.LogWarning(
                    $"[{nameof(MemberSerializer)}] Cannot assign null to member '{m_name}' " +
                    $"of type '{memberType.FullName}'.",
                    component
                );

                return;
            }

            if (value != null &&
                (expectedType == null ||
                 !expectedType.IsInstanceOfType(value)))
            {
                Debug.LogWarning(
                    $"[{nameof(MemberSerializer)}] Value type mismatch for '{m_name}'. " +
                    $"Expected '{memberType?.FullName}', " +
                    $"got '{value.GetType().FullName}'.",
                    component
                );

                return;
            }

            SetMemberValue(component, memberInfo, value);
        }

        #region Helpers

        private bool TryGetCompatibleMember(
            Component component,
            out MemberInfo memberInfo)
        {
            memberInfo = GetMemberInfo(component);

            if (memberInfo == null)
                return false;

            Type memberType = GetMemberType(memberInfo);

            if (memberType != MemberType)
            {
                Debug.LogWarning(
                    $"[{nameof(MemberSerializer)}] Member '{m_name}' changed its type. " +
                    $"Expected '{MemberType?.FullName}', " +
                    $"got '{memberType?.FullName}'.",
                    component
                );

                return false;
            }

            if (memberInfo is PropertyInfo propertyInfo)
            {
                MethodInfo getter = propertyInfo.GetGetMethod(true);
                MethodInfo setter = propertyInfo.GetSetMethod(true);

                if (getter == null ||
                    setter == null ||
                    getter.IsStatic ||
                    setter.IsStatic)
                {
                    Debug.LogWarning(
                        $"[{nameof(MemberSerializer)}] Property '{m_name}' " +
                        "must have instance getter and setter.",
                        component
                    );

                    return false;
                }
            }

            return true;
        }

        private MemberInfo GetMemberInfo(Component component)
        {
            if (component == null)
                return null;

            Type currentType = component.GetType();

            while (currentType != null)
            {
                const BindingFlags flags =
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly;

                FieldInfo fieldInfo = currentType.GetField(m_name, flags);

                if (fieldInfo != null)
                    return fieldInfo;

                PropertyInfo propertyInfo = Array.Find(
                    currentType.GetProperties(flags),
                    property => property.Name == m_name &&
                                property.GetIndexParameters().Length == 0
                );

                if (propertyInfo != null)
                    return propertyInfo;

                currentType = currentType.BaseType;
            }

            Debug.LogWarning(
                $"[{nameof(MemberSerializer)}] Member '{m_name}' was not found " +
                $"in component '{component.GetType().Name}'.",
                component
            );

            return null;
        }

        private static Type GetMemberType(MemberInfo memberInfo)
        {
            if (memberInfo is FieldInfo fieldInfo)
                return fieldInfo.FieldType;

            if (memberInfo is PropertyInfo propertyInfo)
                return propertyInfo.PropertyType;

            return null;
        }

        private static object GetMemberValue(
            Component component,
            MemberInfo memberInfo)
        {
            if (memberInfo is FieldInfo fieldInfo)
                return fieldInfo.GetValue(component);

            if (memberInfo is PropertyInfo propertyInfo)
                return propertyInfo.GetValue(component);

            return null;
        }

        private static void SetMemberValue(
            Component component,
            MemberInfo memberInfo,
            object value)
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                fieldInfo.SetValue(component, value);
                return;
            }

            if (memberInfo is PropertyInfo propertyInfo)
            {
                propertyInfo.SetValue(component, value);
            }
        }

        private static bool IsCollection(Type type)
        {
            return type != null &&
                   type != typeof(string) &&
                   typeof(IEnumerable).IsAssignableFrom(type);
        }

        #endregion
    }
}