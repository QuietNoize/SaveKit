namespace QuietNoize.SaveKit.Internal.Data
{
    using UnityEngine.Scripting.APIUpdating;
    using QuietNoize.SaveKit.Internal.Serialization;
    using System;
    using System.Collections;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Data")]
    [Serializable]
    internal sealed class CollectionSnapshot : MemberSnapshotBase
    {
        [SerializeField] private string m_name;
        [SerializeField] private string m_fieldType;
        [SerializeField] private string m_collectionType;
        [SerializeField] private bool m_isNull;
        [SerializeField] private SerializedCollection m_values;

        internal override string Name => m_name;
        internal override string FieldType => m_fieldType;
        internal bool IsNull => m_isNull;

        internal override object Value
        {
            get
            {
                if (m_isNull)
                    return null;

                if (m_values == null)
                    throw new InvalidOperationException(
                        $"Serialized values are missing for collection '{m_name}'."
                    );

                return m_values.ToCollection(m_collectionType);
            }
        }

        internal CollectionSnapshot(
            string name,
            string fieldType,
            object value)
        {
            m_name = name;
            m_fieldType = fieldType;

            if (value == null)
            {
                m_collectionType = null;
                m_isNull = true;
                m_values = null;
                return;
            }

            if (value is string || !(value is IEnumerable items))
            {
                throw new ArgumentException(
                    "Value must be a collection.",
                    nameof(value)
                );
            }

            m_collectionType = value.GetType().AssemblyQualifiedName;
            m_isNull = false;
            m_values = new SerializedCollection(items);
        }
    }
}
