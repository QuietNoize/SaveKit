namespace QuietNoize.SaveKit
{
    using QuietNoize.SaveKit.Identification;
    using QuietNoize.SaveKit.Internal;
    using QuietNoize.SaveKit.Internal.Data;
    using QuietNoize.SaveKit.Internal.Serialization;
    using QuietNoize.SaveKit.Serialization;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;

    /// <summary>
    /// Manages the serialization and restoration of a GameObject's state.
    /// </summary>
    [AddComponentMenu("SaveKit/GameObject Save Handler"), DisallowMultipleComponent]
    public sealed class GameObjectSaveHandler : SaveHandlerBase
    {
        [SerializeField] private IdentifierBase m_identifier;
        [SerializeField] private string m_prefabPath;
        [SerializeField] private int m_order = 0;
        [SerializeReference] private List<ComponentSerializerDefinition> m_componentDefinitions = new();

        private List<ComponentSerializer> m_componentsToSave = new();

        internal override int Order => m_order;

        #region Unity Lifecycle

        void Awake()
        {
            SetupIdentifier();
            CreateComponentSerializers();

            foreach (var component in m_componentsToSave)
            {
                component.OnAwake();
            }
        }

        void OnDestroy()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying &&
                !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            if (SceneLoader.IsSceneTransitionInProgress)
                return;

            CreateTombstone();
        }

        #endregion

        #region Initialization

        private void CreateComponentSerializers()
        {
            m_componentsToSave.Clear();

            foreach (var definition in m_componentDefinitions)
            {
                var componentSerializer = definition.ToSerializer();
                m_componentsToSave.Add(componentSerializer);
            }
        }

        #endregion

        #region Serialization

        internal override bool TryGetSnapshot(out GameObjectSnapshot snapshot)
        {
            snapshot = default;

            if (m_identifier == null)
            {
                Debug.LogError(
                    $"[{nameof(GameObjectSaveHandler)}] Unable to save GameObject '{gameObject.name}': missing identifier.",
                    gameObject
                );

                return false;
            }

            List<ComponentSnapshot> componentSnapshots = new();

            foreach (var component in m_componentsToSave)
            {
                componentSnapshots.Add(component.ToSnapshot());
            }

            snapshot = new GameObjectSnapshot(
                gameObject.name,
                m_identifier.Id,
                false,
                gameObject.activeSelf,
                m_prefabPath,
                m_order,
                componentSnapshots.ToArray()
            );

            return true;
        }

        internal void ApplyComponentSnapshots(ComponentSnapshot[] snapshots)
        {
            List<Component> assignedComponents = new();

            foreach (var snapshot in snapshots)
            {
                Type componentType = Type.GetType(snapshot.Type);
                uint localNumber = snapshot.LocalNumber;

                if (componentType == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(GameObjectSaveHandler)}] Unable to restore component: " +
                        $"type '{snapshot.Type}' could not be found.",
                        gameObject
                    );

                    continue;
                }

                var componentSerializer = m_componentsToSave
                    .FirstOrDefault(s =>
                        s.ComponentType == componentType &&
                        s.LocalNumber == localNumber
                    );

                if (componentSerializer == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(GameObjectSaveHandler)}] Unable to restore component " +
                        $"'{componentType.Name}': component is not registered for saving.",
                        gameObject
                    );

                    continue;
                }

                Component component = gameObject
                    .GetComponents(componentType)
                    .FirstOrDefault(c => !assignedComponents.Contains(c));

                if (component == null)
                {
                    component = gameObject.AddComponent(componentType);
                }

                assignedComponents.Add(component);

                componentSerializer.OverrideComponent(component);

                if (component is IPreLoadHandler preLoadHandler)
                {
                    preLoadHandler.OnPreLoad();
                }

                componentSerializer.ApplySnapshot(snapshot);

                if (component is IPostLoadHandler postLoadHandler)
                {
                    postLoadHandler.OnPostLoad();
                }
            }
        }

        #endregion

        #region Identifier Management

        private void SetupIdentifier()
        {
            if (m_identifier == null)
            {
                m_identifier = GetComponent<IdentifierBase>();
            }
        }

        private void CreateTombstone()
        {
            if (m_identifier == null)
                throw new ArgumentNullException(nameof(m_identifier));

            Type identifierType = this.m_identifier.GetType();
            string id = m_identifier.Id;

            this.m_identifier.UnregisterId();

            TombstoneSaveHandler.Create(identifierType, id, m_order);
        }

        #endregion

        #region Editor

#if UNITY_EDITOR
        void OnValidate()
        {
            SetupIdentifier();
            UpdatePrefabPath();
        }

        private void UpdatePrefabPath()
        {
            string assetPath = null;

            var prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);

            if (prefabStage != null)
            {
                assetPath = prefabStage.assetPath;
            }
            else if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                assetPath = AssetDatabase.GetAssetPath(gameObject);
            }

            if (string.IsNullOrEmpty(assetPath))
                return;

            const string resourcesFolder = "/Resources/";

            int resourcesIndex = assetPath.LastIndexOf(
                resourcesFolder,
                StringComparison.OrdinalIgnoreCase
            );

            if (resourcesIndex < 0)
                return;

            m_prefabPath = assetPath.Substring(
                resourcesIndex + resourcesFolder.Length
            );

            m_prefabPath = System.IO.Path.ChangeExtension(m_prefabPath, null);
        }
#endif

        #endregion
    }
}
