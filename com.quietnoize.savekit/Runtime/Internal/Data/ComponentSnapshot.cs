namespace QuietNoize.SaveKit.Internal.Data
{
    using UnityEngine.Scripting.APIUpdating;
    using System;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Data")]
    [Serializable]
    internal struct ComponentSnapshot
    {
        [SerializeField] private string m_type;
        [SerializeField] private uint m_localNumber;
        [SerializeField] private bool m_isEnabled;
        [SerializeReference] private MemberSnapshotBase[] m_members;

        internal string Type => m_type;

        internal uint LocalNumber => m_localNumber;

        internal bool IsEnabled => m_isEnabled;

        internal MemberSnapshotBase[] Members => m_members;

        internal ComponentSnapshot(
            string type,
            uint localNumber,
            bool isEnabled,
            MemberSnapshotBase[] members)
        {
            m_type = type;
            m_localNumber = localNumber;
            m_isEnabled = isEnabled;
            m_members = members;
        }
    }
}
