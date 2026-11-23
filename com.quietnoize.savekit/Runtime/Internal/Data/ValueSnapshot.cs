namespace QuietNoize.SaveKit.Internal.Data
{
    using UnityEngine.Scripting.APIUpdating;
    using System;
    using QuietNoize.SaveKit.Internal.Serialization;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Data")]
    [Serializable]
    internal sealed class ValueSnapshot : MemberSnapshotBase
    {
        [SerializeField] private string m_name;
        [SerializeField] private string m_fieldType;
        [SerializeField] private SerializedValue m_value;

        internal override string Name => m_name;
        internal override string FieldType => m_fieldType;
        internal override object Value => m_value?.ToObject();

        public ValueSnapshot(
            string name,
            string fieldType,
            object value)
        {
            m_name = name;
            m_fieldType = fieldType;
            m_value = SerializedValue.FromObject(value);
        }
    }
}
