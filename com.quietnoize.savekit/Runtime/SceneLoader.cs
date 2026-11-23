
namespace QuietNoize.SaveKit
{
    using QuietNoize.SaveKit.Internal;

    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Provides functionality for loading and unloading scenes while preserving their saved states.
    /// </summary>
    public static class SceneLoader
    {
        private static int s_sceneTransitionDepth;

        internal static bool IsSceneTransitionInProgress =>
            s_sceneTransitionDepth > 0;

        internal const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";

        #region Public API

        /// <summary>
        /// Loads a scene in single mode, saving the current scene states before the transition
        /// and restoring the saved state of the loaded scene afterward.
        /// </summary>
        /// <param name="sceneName">The name of the scene to load.</param>
        /// <param name="progressHandler">
        /// An optional callback invoked with the loading progress, ranging from 0 to 1.
        /// </param>
        /// <returns>A task representing the asynchronous scene loading operation.</returns>
        public static async Task LoadSceneAsync(
            string sceneName,
            Action<float> progressHandler = null)
        {
            var reporter = new ProgressReporter(progressHandler);

            await SaveManager.SaveActiveSceneStatesAsync(
                progress => reporter.Report(progress * 0.2f)
            );

            await ExecuteSceneTransitionAsync(
                () => LoadUnitySceneAsync(
                    sceneName,
                    LoadSceneMode.Single,
                    progress => reporter.Report(
                        0.2f + progress * 0.6f
                    )
                )
            );

            await SaveManager.LoadActiveSceneStatesAsync(
                progress => reporter.Report(
                    0.8f + progress * 0.19f
                )
            );

            reporter.Complete();
        }

        /// <summary>
        /// Loads a scene additively without unloading the currently loaded scenes.
        /// </summary>
        /// <param name="sceneName">The name of the scene to load.</param>
        /// <param name="setActive">
        /// Whether the loaded scene should become the active scene.
        /// </param>
        /// <param name="progressHandler">
        /// An optional callback invoked with the loading progress, ranging from 0 to 1.
        /// </param>
        /// <returns>A task representing the asynchronous scene loading operation.</returns>
        public static Task LoadSceneAdditiveAsync(
            string sceneName,
            bool setActive = false,
            Action<float> progressHandler = null)
        {
            return LoadSceneAdditiveInternalAsync(
                sceneName,
                setActive,
                progressHandler
            );
        }

        /// <summary>
        /// Saves the state of a loaded scene and unloads it.
        /// If the scene is currently active, another loaded scene is selected as active before unloading.
        /// </summary>
        /// <param name="sceneName">The name of the scene to unload.</param>
        /// <param name="progressHandler">
        /// An optional callback invoked with the unloading progress, ranging from 0 to 1.
        /// </param>
        /// <returns>A task representing the asynchronous scene unloading operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the scene is not loaded, is the last loaded scene,
        /// or another active scene cannot be selected before unloading.
        /// </exception>
        public static async Task UnloadSceneAsync(
            string sceneName,
            Action<float> progressHandler = null)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    $"Scene '{sceneName}' is not loaded."
                );
            }

            if (SceneManager.sceneCount <= 1)
            {
                throw new InvalidOperationException(
                    "Cannot unload the last loaded scene."
                );
            }

            var reporter = new ProgressReporter(progressHandler);

            await SaveManager.SaveSceneStateAsync(
                sceneName,
                progress => reporter.Report(progress * 0.5f)
            );

            if (scene == SceneManager.GetActiveScene())
            {
                Scene fallbackScene = GetFallbackScene(scene);

                if (!fallbackScene.IsValid() ||
                    !SceneManager.SetActiveScene(fallbackScene))
                {
                    throw new InvalidOperationException(
                        "Unable to select another active scene before unloading."
                    );
                }
            }

            await ExecuteSceneTransitionAsync(
                () => UnloadUnitySceneAsync(
                    scene,
                    progress => reporter.Report(
                        0.5f + progress * 0.49f
                    )
                )
            );

            reporter.Complete();
        }

        #endregion

        #region Internal Implementation

        internal static Task LoadScenesWithoutStateAsync(
            IEnumerable<string> sceneNames)
        {
            return LoadScenesWithoutStateAsync(sceneNames, null);
        }

        internal static Task LoadScenesWithoutStateAsync(
            IEnumerable<string> sceneNames,
            Action<float> reportProgress)
        {
            if (sceneNames == null)
                throw new ArgumentNullException(nameof(sceneNames));

            string[] targetSceneNames = sceneNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(name => name != DontDestroyOnLoadSceneName)
                .Distinct()
                .ToArray();

            if (targetSceneNames.Length == 0)
            {
                throw new ArgumentException(
                    "At least one valid scene name must be provided.",
                    nameof(sceneNames)
                );
            }

            return ExecuteSceneTransitionAsync(async () =>
            {
                var reporter = new ProgressReporter(reportProgress);

                if (targetSceneNames.Length == 1)
                {
                    await LoadUnitySceneAsync(
                        targetSceneNames[0],
                        LoadSceneMode.Single,
                        progress => reporter.Report(progress * 0.99f)
                    );

                    reporter.Complete();
                    return;
                }

                var targetSceneSet = new HashSet<string>(targetSceneNames);

                var scenesToLoad = targetSceneNames
                    .Where(sceneName =>
                    {
                        Scene scene = SceneManager.GetSceneByName(sceneName);
                        return !scene.IsValid() || !scene.isLoaded;
                    })
                    .ToArray();

                var scenesToUnload = new List<Scene>();

                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);

                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;

                    if (scene.name == DontDestroyOnLoadSceneName)
                        continue;

                    if (!targetSceneSet.Contains(scene.name))
                        scenesToUnload.Add(scene);
                }

                int completedOperations = 0;
                int totalOperations = scenesToLoad.Length + scenesToUnload.Count;

                foreach (string sceneName in scenesToLoad)
                {
                    await LoadUnitySceneAsync(
                        sceneName,
                        LoadSceneMode.Additive,
                        progress => reporter.Report(
                            0.99f * (completedOperations + progress) /
                            Mathf.Max(1, totalOperations)
                        )
                    );

                    completedOperations++;
                }

                Scene activeScene = SceneManager.GetSceneByName(
                    targetSceneNames[0]
                );

                if (!activeScene.IsValid() ||
                    !SceneManager.SetActiveScene(activeScene))
                {
                    throw new InvalidOperationException(
                        $"Unable to set scene '{targetSceneNames[0]}' as active."
                    );
                }

                foreach (Scene scene in scenesToUnload)
                {
                    await UnloadUnitySceneAsync(
                        scene,
                        progress => reporter.Report(
                            0.99f * (completedOperations + progress) /
                            Mathf.Max(1, totalOperations)
                        )
                    );

                    completedOperations++;
                }

                reporter.Complete();
            });
        }

        internal static string[] GetLoadedSceneNames(bool acrossScenesIncluded)
        {
            var sceneNames = new HashSet<string>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                sceneNames.Add(scene.name);
            }

            var allObjects = GameObject.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include
            );

            if (!acrossScenesIncluded)
                return sceneNames.ToArray();

            foreach (var gameObject in allObjects)
            {
                Scene scene = gameObject.scene;

                if (scene.IsValid() &&
                    scene.name == DontDestroyOnLoadSceneName)
                {
                    sceneNames.Add(scene.name);
                }
            }

            return sceneNames.ToArray();
        }

        #endregion

        #region Internal Operations

        private static async Task LoadSceneAdditiveInternalAsync(
            string sceneName,
            bool setActive,
            Action<float> progressHandler)
        {
            var reporter = new ProgressReporter(progressHandler);

            await ExecuteSceneTransitionAsync(
                () => LoadUnitySceneAsync(
                    sceneName,
                    LoadSceneMode.Additive,
                    progress => reporter.Report(progress * 0.8f)
                )
            );

            await SaveManager.LoadSceneStateAsync(
                sceneName,
                progress => reporter.Report(
                    0.8f + progress * 0.19f
                )
            );

            if (setActive)
            {
                Scene scene = SceneManager.GetSceneByName(sceneName);

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    throw new InvalidOperationException(
                        $"Scene '{sceneName}' was not loaded successfully."
                    );
                }

                if (!SceneManager.SetActiveScene(scene))
                {
                    throw new InvalidOperationException(
                        $"Unable to set scene '{sceneName}' as active."
                    );
                }
            }

            reporter.Complete();
        }

        private static Task LoadUnitySceneAsync(
            string sceneName,
            LoadSceneMode loadSceneMode,
            Action<float> reportProgress = null)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(
                sceneName,
                loadSceneMode
            );

            return WaitForOperation(
                operation,
                $"Unable to load scene '{sceneName}'.",
                reportProgress,
                true
            );
        }

        private static Task UnloadUnitySceneAsync(
            Scene scene,
            Action<float> reportProgress = null)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);

            return WaitForOperation(
                operation,
                $"Unable to unload scene '{scene.name}'.",
                reportProgress,
                false
            );
        }

        private static Scene GetFallbackScene(Scene excludedScene)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.IsValid() &&
                    scene.isLoaded &&
                    scene != excludedScene)
                {
                    return scene;
                }
            }

            return default;
        }

        #endregion

        #region Helpers

        private static async Task WaitForOperation(
            AsyncOperation operation,
            string errorMessage,
            Action<float> reportProgress,
            bool isLoading)
        {
            if (operation == null)
            {
                throw new InvalidOperationException(errorMessage);
            }

            while (!operation.isDone)
            {
                float progress = operation.progress;

                if (isLoading)
                {
                    progress = Mathf.Clamp01(progress / 0.9f) * 0.99f;
                }

                reportProgress?.Invoke(progress);

                await Task.Yield();
            }

            reportProgress?.Invoke(1f);
        }

        #endregion

        #region Scene Transition Management

        private static async Task ExecuteSceneTransitionAsync(
            Func<Task> transition)
        {
            s_sceneTransitionDepth++;

            try
            {
                await transition();
            }
            finally
            {
                s_sceneTransitionDepth--;
            }
        }

        #endregion
    }
}
