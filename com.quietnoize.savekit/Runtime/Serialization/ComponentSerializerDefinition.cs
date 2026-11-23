namespace QuietNoize.SaveKit.Serialization
{
    using QuietNoize.SaveKit.Internal.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Defines the serialization settings for a component, including
    /// the members that should be serialized and its local identifier.
    /// </summary>
    [Serializable]
    public abstract class ComponentSerializerDefinition
    {
        [SerializeField] protected MemberSerializerDefinition[] m_members;
        [SerializeField] protected uint m_localNumber = 0;

        protected static readonly Dictionary<Type, Type> s_definitionTypes = new();

        internal abstract ComponentSerializer ToSerializer();
    }

    /// <summary>
    /// Defines the serialization settings for a specific component type.
    /// Provides default member discovery and allows custom serialization
    /// definitions through inheritance.
    /// </summary>
    /// <typeparam name="T">The type of component to serialize.</typeparam>
    [Serializable]
    public class ComponentSerializerDefinition<T>
        : ComponentSerializerDefinition
        where T : Component
    {
        [SerializeField] protected T m_component;

        internal static bool CanCreate()
        {
            Type componentType = typeof(T);

            return FindDefinitionType(componentType) != null ||
                   typeof(MonoBehaviour).IsAssignableFrom(componentType);
        }

        internal static bool TryCreate(
            Component component,
            uint localNumber,
            out ComponentSerializerDefinition<T> definition)
        {
            if (component == null || component.GetType() != typeof(T))
            {
                definition = null;
                return false;
            }

            Type componentType = typeof(T);
            Type definitionType = FindDefinitionType(componentType);

            if (definitionType == null &&
                !typeof(MonoBehaviour).IsAssignableFrom(componentType))
            {
                definition = null;
                return false;
            }

            definition = definitionType != null
                ? (ComponentSerializerDefinition<T>)Activator.CreateInstance(
                    definitionType,
                    true)
                : new ComponentSerializerDefinition<T>();

            definition.m_component = (T)component;
            definition.m_localNumber = localNumber;
            definition.m_members = definition.GetMembers();

            return true;
        }

        private static Type FindDefinitionType(Type componentType)
        {
            if (s_definitionTypes.TryGetValue(componentType, out Type cachedType))
                return cachedType;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in GetAssemblyTypes(assembly))
                {
                    if (type.IsAbstract || type.IsInterface)
                        continue;

                    Type currentType = type;

                    while (currentType != null)
                    {
                        if (currentType.IsGenericType &&
                            currentType.GetGenericTypeDefinition() ==
                            typeof(ComponentSerializerDefinition<>))
                        {
                            Type genericArgument = currentType.GetGenericArguments()[0];

                            if (genericArgument == componentType)
                            {
                                s_definitionTypes[componentType] = type;
                                return type;
                            }
                        }

                        currentType = currentType.BaseType;
                    }
                }
            }

            s_definitionTypes[componentType] = null;
            return null;

            IEnumerable<Type> GetAssemblyTypes(Assembly assembly)
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    return exception.Types;
                }
            }
        }

        /// <summary>
        /// Gets the members that should be included in serialization.
        /// By default, discovers serializable members of the component.
        /// </summary>
        /// <returns>The member serialization definitions.</returns>
        protected virtual MemberSerializerDefinition[] GetMembers()
        {
            return MemberSerializerDefinition.GetDefaultMembers(m_component);
        }

        internal sealed override ComponentSerializer ToSerializer()
        {
            var memberSerializers = new List<MemberSerializer>();

            foreach (var member in m_members)
            {
                if (!member.IsSelected)
                    continue;

                var newMemberSerializer = member.ToSerializer();
                memberSerializers.Add(newMemberSerializer);
            }

            return new ComponentSerializer(
                m_component,
                m_localNumber,
                memberSerializers.ToArray()
            );
        }
    }
}
