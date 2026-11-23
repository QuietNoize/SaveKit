namespace QuietNoize.SaveKit.Serialization
{
    using QuietNoize.SaveKit.Internal.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    /// <summary>
    /// Defines a component member to be included in serialization,
    /// specifying its name, type, and default selection state.
    /// </summary>
    [Serializable]
    public struct MemberSerializerDefinition
    {
        private static readonly BindingFlags FieldFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        [SerializeField] private string m_name;
        [SerializeField] private string m_memberType;
        [SerializeField] private bool m_isSelected;

        internal string Name => m_name;
        internal Type MemberType => Type.GetType(m_memberType);
        internal bool IsSelected => m_isSelected;

        /// <summary>
        /// Initializes a new member serializer definition.
        /// </summary>
        /// <param name="name">The name of the component member.</param>
        /// <param name="memberType">The type of the component member.</param>
        /// <param name="isSelected">
        /// Whether the member should be selected for serialization by default.
        /// </param>
        public MemberSerializerDefinition(
            string name,
            Type memberType,
            bool isSelected = false)
        {
            m_name = name;
            m_memberType = memberType.AssemblyQualifiedName;
            m_isSelected = isSelected;
        }

        internal static MemberSerializerDefinition[] GetDefaultMembers(
            Component component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            var members = new List<MemberSerializerDefinition>();

            Type currentType = component?.GetType();

            while (currentType != null &&
                   currentType != typeof(Component) &&
                   currentType != typeof(Behaviour) &&
                   currentType != typeof(MonoBehaviour))
            {
                FieldInfo[] fields = currentType.GetFields(FieldFlags);

                foreach (FieldInfo field in fields)
                {
                    if (field.IsStatic ||
                        field.IsLiteral ||
                        field.IsInitOnly ||
                        field.IsDefined(typeof(NonSerializedAttribute), false))
                    {
                        continue;
                    }

                    bool isSerializable =
                        field.IsPublic ||
                        field.IsDefined(typeof(SerializeField), true) ||
                        field.IsDefined(typeof(SerializeReference), true);

                    if (!isSerializable)
                        continue;

                    members.Add(new MemberSerializerDefinition(
                        field.Name,
                        field.FieldType
                    ));
                }

                currentType = currentType.BaseType;
            }

            return members.ToArray();
        }

        internal MemberSerializer ToSerializer()
        {
            return new MemberSerializer(
                m_name,
                MemberType
            );
        }
    }
}
