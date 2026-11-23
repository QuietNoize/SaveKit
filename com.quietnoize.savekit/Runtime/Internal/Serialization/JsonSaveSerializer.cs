namespace QuietNoize.SaveKit.Internal.Serialization
{
    using System;
    using UnityEngine;

    internal static class JsonSaveSerializer
    {
        internal static string ToJson<T>(T value, bool prettyPrint = true)
        {
            if (ReferenceEquals(value, null))
                throw new ArgumentNullException(nameof(value));

            Type valueType = value.GetType();

            return JsonUtility.ToJson(value, prettyPrint);
        }

        internal static T FromJson<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException(
                    "JSON cannot be null or empty.",
                    nameof(json)
                );

            Type targetType = typeof(T);

            return JsonUtility.FromJson<T>(json);
        }
    }
}