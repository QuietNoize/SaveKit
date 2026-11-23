namespace QuietNoize.SaveKit.Internal.Serialization
{
    using UnityEngine.Scripting.APIUpdating;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Serialization")]
    [Serializable]
    internal class SerializedCollection
    {
        [SerializeField]
        private SerializedValue[] m_items = Array.Empty<SerializedValue>();

        public IReadOnlyList<SerializedValue> Items => m_items;

        public SerializedCollection() { }

        public SerializedCollection(IEnumerable items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            var serializedItems = new List<SerializedValue>();

            foreach (object item in items)
                serializedItems.Add(SerializedValue.FromObject(item));

            m_items = serializedItems.ToArray();
        }

        public object ToCollection(string collectionTypeName)
        {
            if (string.IsNullOrWhiteSpace(collectionTypeName))
                throw new ArgumentException(
                    "Collection type cannot be null or empty.",
                    nameof(collectionTypeName)
                );

            Type collectionType = Type.GetType(collectionTypeName);

            if (collectionType == null)
                throw new TypeLoadException(
                    $"Unable to resolve collection type '{collectionTypeName}'."
                );

            if (IsDictionary(collectionType))
                throw new NotSupportedException(
                    "Dictionary deserialization requires a separate implementation."
                );

            Type elementType = GetElementType(collectionType);

            if (collectionType.IsArray)
            {
                if (collectionType.GetArrayRank() != 1)
                    throw new NotSupportedException(
                        "Only one-dimensional arrays are supported."
                    );

                Array array = Array.CreateInstance(elementType, m_items.Length);

                for (int i = 0; i < m_items.Length; i++)
                    array.SetValue(GetItemValue(i, elementType), i);

                return array;
            }

            Type concreteType = GetConcreteType(collectionType, elementType);

            object collection = Activator.CreateInstance(concreteType);

            bool isStack = IsGenericType(concreteType, typeof(Stack<>));

            for (int i = 0; i < m_items.Length; i++)
            {
                int index = isStack ? m_items.Length - 1 - i : i;

                object value = GetItemValue(index, elementType);

                AddItem(collection, concreteType, elementType, value);
            }

            return collection;
        }

        private object GetItemValue(int index, Type elementType)
        {
            object value = m_items[index]?.ToObject();

            if (value == null)
            {
                if (elementType.IsValueType &&
                    Nullable.GetUnderlyingType(elementType) == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot assign null to collection element of type '{elementType.FullName}'."
                    );
                }

                return null;
            }

            Type nullableType = Nullable.GetUnderlyingType(elementType);
            Type expectedType = nullableType ?? elementType;

            if (!expectedType.IsAssignableFrom(value.GetType()))
                throw new InvalidCastException(
                    $"Cannot assign '{value.GetType().FullName}' " +
                    $"to collection element of type '{elementType.FullName}'."
                );

            return value;
        }

        private static Type GetElementType(Type collectionType)
        {
            if (collectionType.IsArray)
                return collectionType.GetElementType();

            if (IsGenericType(collectionType, typeof(IEnumerable<>)))
                return collectionType.GetGenericArguments()[0];

            foreach (Type interfaceType in collectionType.GetInterfaces())
            {
                if (IsGenericType(interfaceType, typeof(IEnumerable<>)))
                    return interfaceType.GetGenericArguments()[0];
            }

            return typeof(object);
        }

        private static Type GetConcreteType(
            Type collectionType,
            Type elementType)
        {
            if (!collectionType.IsInterface && !collectionType.IsAbstract)
                return collectionType;

            if (collectionType == typeof(IEnumerable) ||
                collectionType == typeof(ICollection) ||
                collectionType == typeof(IList))
                return typeof(ArrayList);

            Type hashSetType = typeof(HashSet<>).MakeGenericType(elementType);

            if (collectionType.IsAssignableFrom(hashSetType))
                return hashSetType;

            Type listType = typeof(List<>).MakeGenericType(elementType);

            if (collectionType.IsAssignableFrom(listType))
                return listType;

            throw new NotSupportedException(
                $"Unable to determine a concrete collection type for '{collectionType.FullName}'."
            );
        }

        private static void AddItem(
            object collection,
            Type collectionType,
            Type elementType,
            object value)
        {
            if (collection is IList list)
            {
                list.Add(value);
                return;
            }

            string[] methodNames =
            {
                "Add",
                "Enqueue",
                "Push",
                "AddLast"
            };

            foreach (string methodName in methodNames)
            {
                MethodInfo method = collectionType.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { elementType },
                    null
                );

                if (method == null)
                    continue;

                method.Invoke(collection, new[] { value });
                return;
            }

            throw new NotSupportedException(
                $"Collection '{collectionType.FullName}' does not have a supported insertion method."
            );
        }

        private static bool IsDictionary(Type type)
        {
            if (typeof(IDictionary).IsAssignableFrom(type))
                return true;

            foreach (Type interfaceType in type.GetInterfaces())
            {
                if (interfaceType.IsGenericType)
                {
                    Type definition = interfaceType.GetGenericTypeDefinition();

                    if (definition == typeof(IDictionary<,>) ||
                        definition == typeof(IReadOnlyDictionary<,>))
                        return true;
                }
            }

            return false;
        }

        private static bool IsGenericType(Type type, Type definition)
        {
            return type.IsGenericType &&
                   type.GetGenericTypeDefinition() == definition;
        }
    }
}
