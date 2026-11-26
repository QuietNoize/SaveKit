namespace QuietNoize.SaveKit.Internal.Serialization
{
    using QuietNoize.SaveKit.Internal.Data;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;

    [Serializable]
    internal sealed class ComponentSerializer
    {
        private Component m_component;
        private uint m_localNumber = 0;
        private MemberSerializer[] m_members;

        internal Type ComponentType => m_component?.GetType();
        internal uint LocalNumber => m_localNumber;

        internal ComponentSerializer() { }

        internal ComponentSerializer(
            Component component,
            uint localNumber,
            MemberSerializer[] members)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            m_component = component;
            m_localNumber = localNumber;
            m_members = members;
        }

        internal void OnAwake()
        {
            if (m_component == null)
                return;

            foreach (var member in m_members)
            {
                member.OnAwake(m_component);
            }
        }

        internal ComponentSnapshot ToSnapshot()
        {
            if (m_component == null)
                throw new InvalidOperationException("Component is null.");

            List<MemberSnapshotBase> memberSnapshots = new();

            foreach (var member in m_members)
            {
                object value = member.GetValue(m_component);

                if (!member.IsDirty(value))
                    continue;

                memberSnapshots.Add(member.ToSnapshot(value));
            }

            bool isEnabled = m_component switch
            {
                Behaviour b => b.enabled,
                Renderer r => r.enabled,
                Collider c => c.enabled,
                _ => true
            };

            return new ComponentSnapshot(
                m_component.GetType().AssemblyQualifiedName,
                m_localNumber,
                isEnabled,
                memberSnapshots.ToArray()
            );
        }

        internal void ApplySnapshot(ComponentSnapshot snapshot)
        {
            if (m_component == null)
                throw new InvalidOperationException("Component is null.");

            if (snapshot.Type != m_component.GetType().AssemblyQualifiedName)
            {
                throw new InvalidOperationException(
                    $"Snapshot type mismatch. Expected '{m_component.GetType().FullName}', " +
                    $"got '{snapshot.Type}'."
                );
            }

            foreach (var memberSnapshots in snapshot.Members)
            {
                var member = m_members.FirstOrDefault(
                    f => f.Name == memberSnapshots.Name
                );

                if (member == null)
                    continue;

                member.ApplySnapshot(m_component, memberSnapshots);
            }

            switch (m_component)
            {
                case Behaviour b: b.enabled = snapshot.IsEnabled; break;
                case Renderer r: r.enabled = snapshot.IsEnabled; break;
                case Collider c: c.enabled = snapshot.IsEnabled; break;
            }

        }

        internal void OverrideComponent(Component component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            if (component.GetType() != ComponentType)
            {
                throw new InvalidOperationException(
                    $"Component type mismatch. Expected '{ComponentType}', " +
                    $"got '{component.GetType()}'."
                );
            }

            m_component = component;
        }
    }
}