namespace QuietNoize.SaveKit.Identification
{
    using System.Collections.Generic;
    using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;
#endif

    /// <summary>
    /// Defines the base class for components that assign and manage unique identifiers.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public abstract class IdentifierBase : MonoBehaviour
    {
        protected static readonly Dictionary<string, IdentifierBase> s_pool = new();

        public abstract string Id { get; }
        protected abstract bool IsIdAssigned { get; }
        protected virtual bool CanGenerateId { get; } = true;

        protected virtual void Awake()
        {
            Register();
        }

        public static T GetIdentifierById<T>(string id)
            where T : IdentifierBase
        {
            T identifier = null;

            if (s_pool.TryGetValue(id, out var value))
                identifier = (T)value;

            return identifier;
        }

        public static bool TryGetIdentifierById<T>(string id, out T identifier)
            where T : IdentifierBase
        {
            identifier = GetIdentifierById<T>(id);
            return identifier != null;
        }

        internal void UnregisterId()
        {
            Unregister();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeEditorRegistry()
        {
            EditorApplication.delayCall += RebuildPoolInEditor;

            EditorApplication.hierarchyChanged += RebuildPoolInEditor;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RebuildPoolInEditor();
        }

        private static void RebuildPoolInEditor()
        {
            s_pool.Clear();

            var identifiers = Resources.FindObjectsOfTypeAll<IdentifierBase>();

            foreach (var identifier in identifiers)
            {
                if (identifier == null)
                    continue;

                var scene = identifier.gameObject.scene;

                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                identifier.Register();
            }
        }

        protected virtual void OnValidate()
        {
            Unregister();
            Register();
        }
#endif

        protected virtual void OnDestroy()
        {
            Unregister();
        }

        protected abstract void Register();
        protected abstract void Unregister();
        protected abstract bool HasCollision();
        protected abstract void SetId(string id);
        internal abstract void OverrideId(string id);
    }

    /// <summary>
    /// Provides a base implementation for identifiers that store their values using a specified type.
    /// </summary>
    public abstract class IdentifierBase<T> : IdentifierBase
    {
        [SerializeField] protected T m_id = default;

        protected override void Register()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (IsInPrefabAssetOrStage())
            {
                if (!CanGenerateId)
                {
                    ResetId();
                    return;
                }

                if (!IsIdAssigned)
                    m_id = GenerateNewId();

                return;
            }
#endif

            if (!IsIdAssigned || HasCollision())
            {
                if (!CanGenerateId)
                    return;

                m_id = GenerateNewId();

                if (!IsIdAssigned)
                {
                    Debug.LogError(
                        $"[{GetType().Name}] Failed to generate a valid identifier for {gameObject.name}!",
                        gameObject
                    );
                    return;
                }
            }

            s_pool[Id] = this;
        }

        protected override void Unregister()
        {
            var keysToRemove = new List<string>();

            foreach (var pair in s_pool)
            {
                if (pair.Value == null || pair.Value == this)
                    keysToRemove.Add(pair.Key);
            }

            foreach (var key in keysToRemove)
                s_pool.Remove(key);
        }

        protected override bool HasCollision()
        {
            if (!s_pool.TryGetValue(Id, out var identifier))
                return false;

            if (identifier == null)
            {
                s_pool.Remove(Id);
                return false;
            }

            if (identifier == this)
                return false;

            Debug.LogWarning(
                $"[{GetType().Name}] Identifier collision detected! " +
                $"Current: {gameObject.name} " +
                $"Conflicting: {identifier.gameObject.name} ({identifier.GetType().Name}) " +
                $"ID: {Id}",
                gameObject
            );

            return true;
        }

        internal override void OverrideId(string id)
        {
            Unregister();
            SetId(id);
            Register();
        }

        protected override void SetId(string id)
        {
            m_id = GetTypedId(id);
        }

        protected abstract T GenerateNewId();
        protected abstract T GetTypedId(string idString);

        protected virtual void ResetId()
        {
            m_id = default;
        }

        protected bool IsInPrefabAssetOrStage()
        {
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                return true;

            if (PrefabStageUtility.GetPrefabStage(gameObject) != null)
                return true;
#endif
            return false;
        }

    }
}