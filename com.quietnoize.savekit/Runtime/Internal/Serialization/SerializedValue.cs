namespace QuietNoize.SaveKit.Internal.Serialization
{
    using UnityEngine.Scripting.APIUpdating;
    using System;
    using System.Reflection;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Serialization")]
    [Serializable]
    internal sealed class SerializedValue
    {
        [SerializeField] private string m_type;
        [SerializeField] private string m_json;

        public string TypeName => m_type;
        public string Json => m_json;

        private SerializedValue(string type, string json)
        {
            m_type = type;
            m_json = json;
        }

        public static SerializedValue FromObject(object value)
        {
            if (value == null)
                return new SerializedValue(null, null);

            var valueType = value.GetType();

            var wrapperType = typeof(ValueWrapper<>)
                .MakeGenericType(valueType);

            object wrapper = Activator.CreateInstance(
                wrapperType,
                value
            );

            string json = JsonUtility.ToJson(wrapper);

            return new SerializedValue(
                valueType.AssemblyQualifiedName,
                json
            );
        }

        public object ToObject()
        {
            if (string.IsNullOrEmpty(m_type))
                return null;

            var valueType = System.Type.GetType(m_type);

            if (valueType == null)
                throw new TypeLoadException(
                    $"Unable to resolve saved type '{m_type}'."
                );

            var wrapperType = typeof(ValueWrapper<>)
                .MakeGenericType(valueType);

            object wrapper = JsonUtility.FromJson(
                m_json,
                wrapperType
            );

            FieldInfo field = wrapperType.GetField(
                nameof(ValueWrapper<object>.Value)
            );

            return field.GetValue(wrapper);
        }

        [Serializable]
        private class ValueWrapper<T>
        {
            public T Value;

            public ValueWrapper(T value)
            {
                Value = value;
            }
        }
    }
}
