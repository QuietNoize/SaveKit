namespace QuietNoize.SaveKit.Internal
{
    using UnityEngine.Scripting.APIUpdating;
    using QuietNoize.SaveKit.Internal.Data;
    using UnityEngine;
    using System;
    using QuietNoize.SaveKit.Identification;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Components")]
    [AddComponentMenu(""), DisallowMultipleComponent]
    internal sealed class TombstoneSaveHandler : SaveHandlerBase
    {
        [SerializeField] private IdentifierBase m_identifier;
        [SerializeField] private int m_order;

        internal override int Order => m_order;

        void Awake()
        {
            gameObject.hideFlags = HideFlags.NotEditable | HideFlags.HideInHierarchy;
        }

        internal override bool TryGetSnapshot(out GameObjectSnapshot snapshot)
        {
            snapshot = new GameObjectSnapshot(
                string.Empty,
                m_identifier.Id,
                true,
                false,
                string.Empty,
                m_order,
                Array.Empty<ComponentSnapshot>()
            );

            return true;
        }

        internal static TombstoneSaveHandler Create(Type identifierType, string id, int order)
        {
            var tombstoneObject = new GameObject(
                $"{id}_Tombstone"
            );

            var tombstone = tombstoneObject.AddComponent<TombstoneSaveHandler>();

            tombstone.m_identifier = (IdentifierBase)tombstoneObject.AddComponent(identifierType);
            tombstone.m_identifier.OverrideId(id);

            tombstone.m_order = order;

            return tombstone;
        }
    }
}
