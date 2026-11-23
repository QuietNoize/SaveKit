namespace QuietNoize.SaveKit.Internal.Data
{
    using UnityEngine.Scripting.APIUpdating;
    using System;
    using UnityEngine;

    [MovedFrom(true, sourceNamespace: "QuietNoize.SaveKit.Data")]
    [Serializable]
    internal struct GameSnapshot
    {
        [SerializeField] private string[] m_activeScenes;
        [SerializeField] private SceneSnapshot[] m_sceneSnapshots;

        internal string[] ActiveScenes => m_activeScenes;
        internal SceneSnapshot[] SceneSnapshots => m_sceneSnapshots;

        internal GameSnapshot(string[] activeScenes, SceneSnapshot[] sceneSnapshots)
        {
            m_activeScenes = activeScenes;
            m_sceneSnapshots = sceneSnapshots;
        }
    }
}
