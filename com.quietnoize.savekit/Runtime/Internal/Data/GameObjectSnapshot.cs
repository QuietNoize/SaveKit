namespace QuietNoize.SaveKit.Internal.Data
{
    using UnityEngine.Scripting.APIUpdating;
    using System;
    using UnityEngine;

    /// <summary>
    /// Represents a snapshot of a GameObject's state and its serialized component fields.
    /// </summary>
    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Data")]
    [Serializable]
    internal struct GameObjectSnapshot
    {
        [SerializeField] private string m_name;
        [SerializeField] private string m_id;
        [SerializeField] private bool m_isDestroyed;
        [SerializeField] private bool m_isEnabled;
        [SerializeField] private string m_prefabPath;
        [SerializeField] private int m_order;
        [SerializeField] private ComponentSnapshot[] m_componentSnapshots;

        /// <summary>
        /// Gets the name of the GameObject.
        /// </summary>
        internal string Name => m_name;

        /// <summary>
        /// Gets the unique identifier of the GameObject.
        /// </summary>
        internal string Id => m_id;

        /// <summary>
        /// Gets a value indicating whether the GameObject has been destroyed.
        /// </summary>
        internal bool IsDestroyed => m_isDestroyed;

        /// <summary>
        /// Gets a value indicating whether the GameObject is enabled.
        /// </summary>
        internal bool IsEnabled => m_isEnabled;

        /// <summary>
        /// Gets the asset path of the GameObject's source prefab.
        /// </summary>
        internal string PrefabPath => m_prefabPath;

        /// <summary>
        /// Gets the priority used to determine the order of saving and loading.
        /// </summary>
        internal int Order => m_order;

        /// <summary>
        /// Gets the snapshots of the GameObject's component fields.
        /// </summary>
        internal ComponentSnapshot[] ComponentSnapshots => m_componentSnapshots;

        /// <summary>
        /// Initializes a new instance of the <see cref="GameObjectSnapshot"/> struct.
        /// </summary>
        /// <param name="name">The name of the GameObject.</param>
        /// <param name="id">The unique identifier of the GameObject.</param>
        /// <param name="isDestroyed">Indicates whether the GameObject has been destroyed.</param>
        /// <param name="isEnabled">Indicates whether the GameObject is enabled.</param>
        /// <param name="prefabPath">The asset path of the source prefab.</param>
        /// <param name="order">The order of saving and loading.</param>
        /// <param name="componentSnapshots">The snapshots of the GameObject's components.</param>
        internal GameObjectSnapshot(
            string name,
            string id,
            bool isDestroyed,
            bool isEnabled,
            string prefabPath,
            int order,
            ComponentSnapshot[] componentSnapshots)
        {
            m_name = name;
            m_id = id;
            m_isDestroyed = isDestroyed;
            m_isEnabled = isEnabled;
            m_prefabPath = prefabPath;
            m_order = order;
            m_componentSnapshots = componentSnapshots;
        }
    }
}
