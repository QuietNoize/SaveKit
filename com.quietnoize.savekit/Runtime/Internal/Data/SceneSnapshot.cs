namespace QuietNoize.SaveKit.Internal.Data
{
    using UnityEngine.Scripting.APIUpdating;
    using System;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Data")]
    [Serializable]
    internal struct SceneSnapshot
    {
        [SerializeField] private string m_name;
        [SerializeField] private GameObjectSnapshot[] m_gameObjectSnapshots;

        internal string Name => m_name;
        internal GameObjectSnapshot[] GameObjectSnapshots => m_gameObjectSnapshots;

        internal SceneSnapshot(string name, GameObjectSnapshot[] snapshots)
        {
            m_name = name;
            m_gameObjectSnapshots = snapshots;
        }
    }
}
