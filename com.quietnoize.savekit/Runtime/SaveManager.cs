namespace QuietNoize.SaveKit
{
    using QuietNoize.SaveKit.Internal;
    using QuietNoize.SaveKit.Internal.Data;
    using QuietNoize.SaveKit.Identification;
    using QuietNoize.SaveKit.Internal.IO;
    using QuietNoize.SaveKit.Internal.Serialization;

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Provides methods for saving and loading the game state to and from files.
    /// </summary>
    public static class SaveManager
    {
        private class Runner : MonoBehaviour { }

        private static Runner s_runner;
        private static List<SceneSnapshot> s_sceneSnapshots = new();
        private static Task s_currentTask;
        private static string s_currentOperationName;

        #region Public API

        /// <summary>
        /// Saves the current game state to a file.
        /// </summary>
        /// <param name="pathToFile">The path to the save file.</param>
        /// <param name="progressHandler">
        /// An optional callback invoked with the save progress, ranging from 0 to 1.
        /// </param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public static async Task SaveGameToFileAsync(
            string pathToFile,
            Action<float> progressHandler = null)
        {
            var reporter = new ProgressReporter(progressHandler);

            GameSnapshot snapshot = default;

            await StartOperation(
                nameof(SaveGameToFileAsync),
                GetGameSnapshotAsyncRoutine(
                    value => snapshot = value,
                    progress => reporter.Report(progress * 0.9f)
                ),
                () =>
                {
                    reporter.Report(0.95f);

                    string json = JsonSaveSerializer.ToJson(snapshot);
                    SaveFileIO.Write(json, pathToFile);

                    return true;
                }
            );

            reporter.Complete();
        }

        /// <summary>
        /// Loads the game state from a file, restoring the active scenes and their saved states.
        /// </summary>
        /// <param name="pathToFile">The path to the save file.</param>
        /// <param name="progressHandler">
        /// An optional callback invoked with the load progress, ranging from 0 to 1.
        /// </param>
        /// <returns>A task representing the asynchronous load operation.</returns>
        public static async Task LoadGameFromFileAsync(
            string pathToFile,
            Action<float> progressHandler = null)
        {
            var reporter = new ProgressReporter(progressHandler);

            string json = SaveFileIO.Read(pathToFile);

            reporter.Report(0.05f);

            var gameSnapshot = JsonSaveSerializer.FromJson<GameSnapshot>(json);

            s_sceneSnapshots = gameSnapshot.SceneSnapshots.ToList();

            reporter.Report(0.1f);

            string[] activeScenes = gameSnapshot.ActiveScenes;

            await SceneLoader.LoadScenesWithoutStateAsync(
                activeScenes,
                progress => reporter.Report(
                    0.1f + progress * 0.15f
                )
            );

            reporter.Report(0.25f);

            await StartOperation(
                nameof(LoadGameFromFileAsync),
                LoadSceneStatesAsyncRoutine(
                    activeScenes,
                    progress => reporter.Report(
                        0.25f + progress * 0.74f
                    )
                )
            );

            reporter.Complete();
        }

        #endregion

        #region Internal Implementation

        internal static async Task SaveActiveSceneStatesAsync(
            Action<float> progressHandler = null,
            bool persistAcrossScenes = false)
        {
            var reporter = new ProgressReporter(progressHandler);

            await StartOperation(
                nameof(SaveActiveSceneStatesAsync),
                SaveActiveSceneStatesAsyncRoutine(
                    reporter.Report,
                    persistAcrossScenes
                )
            );

            reporter.Complete();
        }

        internal static async Task SaveSceneStateAsync(
            string sceneName,
            Action<float> progressHandler = null)
        {
            var reporter = new ProgressReporter(progressHandler);

            await StartOperation(
                nameof(SaveSceneStateAsync),
                SaveSceneStateAsyncRoutine(
                    sceneName,
                    reporter.Report
                )
            );

            reporter.Complete();
        }

        internal static async Task LoadActiveSceneStatesAsync(
            Action<float> progressHandler = null,
            bool persistAcrossScenes = false)
        {
            var reporter = new ProgressReporter(progressHandler);

            await StartOperation(
                nameof(LoadActiveSceneStatesAsync),
                LoadActiveSceneStatesAsyncRoutine(
                    reporter.Report,
                    persistAcrossScenes
                )
            );

            reporter.Complete();
        }

        internal static async Task LoadSceneStateAsync(
            string sceneName,
            Action<float> progressHandler = null)
        {
            var reporter = new ProgressReporter(progressHandler);

            await StartOperation(
                nameof(LoadSceneStateAsync),
                LoadSceneStateAsyncRoutine(
                    sceneName,
                    reporter.Report
                )
            );

            reporter.Complete();
        }

        #endregion

        #region Save Operations

        private static IEnumerator GetGameSnapshotAsyncRoutine(
            Action<GameSnapshot> setResult,
            Action<float> reportProgress)
        {
            string[] activeSceneNames = SceneLoader.GetLoadedSceneNames(true);

            yield return SaveSceneStatesAsyncRoutine(
                activeSceneNames,
                reportProgress
            );

            setResult(new GameSnapshot(
                activeSceneNames,
                s_sceneSnapshots.ToArray()
            ));
        }

        private static IEnumerator SaveActiveSceneStatesAsyncRoutine(
            Action<float> reportProgress,
            bool persistAcrossScenes)
        {
            string[] activeSceneNames = SceneLoader.GetLoadedSceneNames(
                persistAcrossScenes
            );

            yield return SaveSceneStatesAsyncRoutine(
                activeSceneNames,
                reportProgress
            );
        }

        private static IEnumerator SaveSceneStatesAsyncRoutine(
            string[] sceneNames,
            Action<float> reportProgress)
        {
            if (sceneNames.Length == 0)
            {
                reportProgress(1f);
                yield break;
            }

            for (int i = 0; i < sceneNames.Length; i++)
            {
                int sceneIndex = i;

                yield return SaveSceneStateAsyncRoutine(
                    sceneNames[i],
                    progress => reportProgress(
                        (sceneIndex + progress) / sceneNames.Length
                    )
                );
            }

            reportProgress(1f);
        }

        private static IEnumerator SaveSceneStateAsyncRoutine(
            string sceneName,
            Action<float> reportProgress)
        {
            Log($"Saving scene state '{sceneName}'.");

            bool isDontDestroyOnLoad =
                sceneName == SceneLoader.DontDestroyOnLoadSceneName;

            Scene targetScene = default;

            if (!isDontDestroyOnLoad)
            {
                targetScene = SceneManager.GetSceneByName(sceneName);

                if (!targetScene.IsValid() || !targetScene.isLoaded)
                {
                    throw new InvalidOperationException(
                        $"Scene '{sceneName}' is not loaded."
                    );
                }
            }

            var sortedHandlers = GameObject
                .FindObjectsByType<SaveHandlerBase>(FindObjectsInactive.Include)
                .Where(component => BelongsToScene(
                    component.gameObject,
                    sceneName,
                    targetScene
                ))
                .OrderBy(handler => handler.Order)
                .ToArray();

            Log(
                $"Found {sortedHandlers.Length} save handlers " +
                $"in scene '{sceneName}'."
            );

            var gameObjectSnapshots = new List<GameObjectSnapshot>();
            var exceptions = new List<Exception>();

            int processedHandlers = 0;
            int totalHandlers = sortedHandlers.Length;

            reportProgress(0f);

            foreach (var handler in sortedHandlers)
            {
                try
                {
                    if (!handler.TryGetSnapshot(out var snapshot))
                    {
                        exceptions.Add(new InvalidOperationException(
                            $"Failed to create snapshot for GameObject '{handler.gameObject.name}' " +
                            $"using '{handler.GetType().Name}'."
                        ));

                        continue;
                    }

                    gameObjectSnapshots.Add(snapshot);
                }
                catch (Exception ex)
                {
                    exceptions.Add(new InvalidOperationException(
                        $"Failed to save GameObject '{handler.gameObject.name}' " +
                        $"using '{handler.GetType().Name}'.",
                        ex
                    ));
                }
                finally
                {
                    processedHandlers++;

                    float progress = totalHandlers > 0
                        ? (float)processedHandlers / totalHandlers
                        : 1f;

                    reportProgress(progress * 0.95f);
                }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(
                    "Failed to save scene because some objects could not be serialized.",
                    exceptions
                );
            }

            var sceneSnapshot = new SceneSnapshot(
                sceneName,
                gameObjectSnapshots.ToArray()
            );

            int snapshotIndex = s_sceneSnapshots.FindIndex(
                s => s.Name == sceneName
            );

            if (snapshotIndex >= 0)
                s_sceneSnapshots[snapshotIndex] = sceneSnapshot;
            else
                s_sceneSnapshots.Add(sceneSnapshot);

            Log(
                $"Scene state '{sceneName}' saved successfully. " +
                $"GameObjects: {gameObjectSnapshots.Count}."
            );

            reportProgress(1f);
            yield break;
        }

        #endregion

        #region Load Operations

        private static IEnumerator LoadActiveSceneStatesAsyncRoutine(
            Action<float> reportProgress,
            bool persistAcrossScenes)
        {
            string[] activeSceneNames = SceneLoader.GetLoadedSceneNames(
                persistAcrossScenes
            );

            yield return LoadSceneStatesAsyncRoutine(
                activeSceneNames,
                reportProgress
            );
        }

        private static IEnumerator LoadSceneStatesAsyncRoutine(
            string[] sceneNames,
            Action<float> reportProgress)
        {
            if (sceneNames.Length == 0)
            {
                reportProgress(1f);
                yield break;
            }

            for (int i = 0; i < sceneNames.Length; i++)
            {
                int sceneIndex = i;

                yield return LoadSceneStateAsyncRoutine(
                    sceneNames[i],
                    progress => reportProgress(
                        (sceneIndex + progress) / sceneNames.Length
                    )
                );
            }

            reportProgress(1f);
        }

        private static IEnumerator LoadSceneStateAsyncRoutine(
            string sceneName,
            Action<float> reportProgress)
        {
            Log($"Loading scene state '{sceneName}'.");

            bool isDontDestroyOnLoad =
                sceneName == SceneLoader.DontDestroyOnLoadSceneName;

            Scene targetScene = default;

            if (!isDontDestroyOnLoad)
            {
                targetScene = SceneManager.GetSceneByName(sceneName);

                if (!targetScene.IsValid() || !targetScene.isLoaded)
                {
                    throw new InvalidOperationException(
                        $"Scene '{sceneName}' is not loaded."
                    );
                }
            }

            int snapshotIndex = s_sceneSnapshots.FindIndex(
                s => s.Name == sceneName
            );

            if (snapshotIndex == -1)
            {
                Log(
                    $"[{nameof(SaveManager)}] No saved state found for scene '{sceneName}'. " +
                    "Skipping restoration."
                );

                reportProgress(1f);
                yield break;
            }

            var targetSceneSnapshot = s_sceneSnapshots[snapshotIndex];

            var allSaveHandlers = GameObject
                .FindObjectsByType<GameObjectSaveHandler>(FindObjectsInactive.Include)
                .Where(handler => BelongsToScene(
                    handler.gameObject,
                    sceneName,
                    targetScene
                ))
                .ToArray();

            var exceptions = new List<Exception>();

            var sortedGameObjectSnapshots = targetSceneSnapshot
                .GameObjectSnapshots
                .OrderBy(s => s.Order)
                .ToArray();

            var handlersToDestroyed = new List<GameObjectSaveHandler>();
            var tombstonesToDestroyed = new List<TombstoneSaveHandler>();
            var objectSnaphsotsToCreateTombstones = new List<GameObjectSnapshot>();

            int processedSnapshots = 0;
            int totalSnapshots = sortedGameObjectSnapshots.Length;

            reportProgress(0f);

            var processedHandlers = new HashSet<GameObjectSaveHandler>();

            foreach (var snapshot in sortedGameObjectSnapshots)
            {
                if (snapshot.IsDestroyed)
                    continue;

                var sameIdTombstone = (TombstoneSaveHandler)null;

                if (IdentifierBase.TryGetIdentifierById<IdentifierBase>(
                        snapshot.Id,
                        out var targetIdentifier))
                {
                    sameIdTombstone = targetIdentifier.GetComponent<TombstoneSaveHandler>();
                }

                if (sameIdTombstone != null)
                {
                    targetIdentifier.UnregisterId();
                    tombstonesToDestroyed.Add(sameIdTombstone);
                    GameObject.Destroy(sameIdTombstone.gameObject);
                }
            }

            yield return new WaitUntil(() =>
                tombstonesToDestroyed.All(tombstone => tombstone == null)
            );

            foreach (var snapshot in sortedGameObjectSnapshots)
            {
                try
                {
                    var targetHandler = (GameObjectSaveHandler)null;

                    if (IdentifierBase.TryGetIdentifierById<IdentifierBase>(
                        snapshot.Id,
                        out var targetIdentifier))
                    {
                        targetHandler = targetIdentifier.GetComponent<GameObjectSaveHandler>();
                    }

                    if (snapshot.IsDestroyed)
                    {
                        objectSnaphsotsToCreateTombstones.Add(snapshot);

                        if (targetHandler != null)
                        {
                            processedHandlers.Add(targetHandler);

                            Log(
                                $"Destroying GameObject '{targetHandler.gameObject.name}' " +
                                $"(ID: {snapshot.Id})."
                            );

                            targetIdentifier.UnregisterId();
                            handlersToDestroyed.Add(targetHandler);
                            GameObject.Destroy(targetHandler.gameObject);
                        }

                        continue;
                    }

                    if (targetHandler == null)
                    {
                        if (string.IsNullOrWhiteSpace(snapshot.PrefabPath))
                        {
                            throw new InvalidOperationException(
                                $"Unable to restore GameObject '{snapshot.Name}' (ID: {snapshot.Id}). " +
                                "Prefab path is null or empty."
                            );
                        }

                        var prefab = Resources.Load<GameObject>(snapshot.PrefabPath);

                        if (prefab == null)
                        {
                            throw new InvalidOperationException(
                                $"Unable to restore GameObject '{snapshot.Name}' (ID: {snapshot.Id}). " +
                                $"Prefab was not found at path '{snapshot.PrefabPath}'."
                            );
                        }

                        Log(
                            $"Instantiating GameObject '{snapshot.Name}' " +
                            $"(ID: {snapshot.Id}, Prefab: '{snapshot.PrefabPath}')."
                        );

                        var handlerObject = GameObject.Instantiate(prefab);

                        if (isDontDestroyOnLoad)
                        {
                            GameObject.DontDestroyOnLoad(handlerObject);
                        }
                        else
                        {
                            SceneManager.MoveGameObjectToScene(
                                handlerObject,
                                targetScene
                            );
                        }

                        handlerObject.name = snapshot.Name;

                        targetHandler = handlerObject.GetComponent<GameObjectSaveHandler>();

                        if (targetHandler == null)
                        {
                            targetHandler = handlerObject.AddComponent<GameObjectSaveHandler>();
                        }

                        var handlerIdentifier = handlerObject.GetComponent<IdentifierBase>();

                        if (handlerIdentifier == null)
                        {
                            handlerIdentifier = handlerObject.AddComponent<StringIdentifier>();
                        }

                        handlerIdentifier.OverrideId(snapshot.Id);
                    }

                    targetHandler.ApplyComponentSnapshots(snapshot.ComponentSnapshots);
                    targetHandler.gameObject.SetActive(snapshot.IsEnabled);

                    processedHandlers.Add(targetHandler);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                finally
                {
                    processedSnapshots++;

                    float progress = totalSnapshots > 0
                        ? (float)processedSnapshots / totalSnapshots
                        : 1f;

                    reportProgress(progress * 0.8f);
                }
            }

            foreach (var handler in allSaveHandlers)
            {
                if (handler == null || processedHandlers.Contains(handler))
                    continue;

                Log(
                    $"Destroying obsolete GameObject '{handler.gameObject.name}' " +
                    $"(ID: {handler.GetComponent<IdentifierBase>()?.Id})."
                );

                handler.GetComponent<IdentifierBase>()?.UnregisterId();

                handlersToDestroyed.Add(handler);
                GameObject.Destroy(handler.gameObject);
            }

            yield return new WaitUntil(() =>
                handlersToDestroyed.All(handler => handler == null)
            );

            reportProgress(0.9f);

            int totalTombstones = objectSnaphsotsToCreateTombstones.Count;

            for (int i = 0; i < totalTombstones; i++)
            {
                var snapshot = objectSnaphsotsToCreateTombstones[i];
                var tombstone = (TombstoneSaveHandler)null;

                if (IdentifierBase.TryGetIdentifierById<IdentifierBase>(
                    snapshot.Id,
                    out var tombStoneIdentifier))
                {
                    tombstone = tombStoneIdentifier.GetComponent<TombstoneSaveHandler>();
                }

                if (tombstone == null)
                {
                    if (tombStoneIdentifier != null)
                    {
                        tombStoneIdentifier.UnregisterId();
                        GameObject.Destroy(tombStoneIdentifier.gameObject);

                        yield return new WaitUntil(() =>
                            tombStoneIdentifier == null
                        );
                    }

                    TombstoneSaveHandler.Create(
                        typeof(StringIdentifier),
                        snapshot.Id,
                        snapshot.Order
                    );
                }

                reportProgress(
                    0.9f + 0.09f * (i + 1) / Mathf.Max(1, totalTombstones)
                );
            }

            reportProgress(0.99f);

            if (exceptions.Count > 0)
            {
                throw new AggregateException(
                    "Failed to load scene because some objects could not be deserialized.",
                    exceptions
                );
            }

            reportProgress(1f);
            yield break;
        }

        #endregion

        #region Operation Management

        private static Task StartOperation(
            string operationName,
            IEnumerator routine)
        {
            return StartOperation(
                operationName,
                routine,
                () => true
            );
        }

        private static Task<T> StartOperation<T>(
            string operationName,
            IEnumerator routine,
            Func<T> getResult)
        {
            Log($"Starting operation '{operationName}'.");

            if (s_currentTask != null && !s_currentTask.IsCompleted)
            {
                string message =
                    $"Cannot start '{operationName}'. " +
                    $"Previous operation '{s_currentOperationName}' is still running. " +
                    "Wait for it to complete.";

                LogWarning($"[{nameof(SaveManager)}] {message}");

                return Task.FromException<T>(
                    new InvalidOperationException(message)
                );
            }

            var completion = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            s_currentTask = completion.Task;
            s_currentOperationName = operationName;

            try
            {
                GetRunner().StartCoroutine(
                    ExecuteOperation(routine, completion, getResult)
                );
            }
            catch (Exception ex)
            {
                LogException(ex);

                completion.TrySetException(ex);
                s_currentOperationName = null;
            }

            return completion.Task;
        }

        private static IEnumerator ExecuteOperation<T>(
            IEnumerator routine,
            TaskCompletionSource<T> completion,
            Func<T> getResult)
        {
            var routines = new Stack<IEnumerator>();
            routines.Push(routine);

            try
            {
                while (routines.Count > 0)
                {
                    IEnumerator currentRoutine = routines.Peek();

                    bool hasNext = false;
                    object current = null;
                    Exception exception = null;

                    try
                    {
                        hasNext = currentRoutine.MoveNext();

                        if (hasNext)
                            current = currentRoutine.Current;
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }

                    if (exception != null)
                    {
                        LogError($"Operation '{s_currentOperationName}' failed.");

                        LogException(exception);
                        completion.TrySetException(exception);
                        yield break;
                    }

                    if (!hasNext)
                    {
                        routines.Pop();

                        if (currentRoutine is IDisposable disposable)
                            disposable.Dispose();

                        continue;
                    }

                    if (current is IEnumerator nestedRoutine)
                    {
                        routines.Push(nestedRoutine);
                        continue;
                    }

                    yield return current;
                }

                try
                {
                    completion.TrySetResult(getResult());

                    Log(
                        $"Operation '{s_currentOperationName}' " +
                        "completed successfully."
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, s_runner);
                    completion.TrySetException(ex);
                }
            }
            finally
            {
                while (routines.Count > 0)
                {
                    IEnumerator pendingRoutine = routines.Pop();

                    try
                    {
                        if (pendingRoutine is IDisposable disposable)
                            disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex, s_runner);
                    }
                }

                s_currentOperationName = null;
            }
        }

        #endregion

        #region Helpers

        private static Runner GetRunner()
        {
            if (s_runner == null)
            {
                s_runner = GameObject.FindAnyObjectByType<Runner>();

                if (s_runner == null)
                {
                    var runnerObject = new GameObject("SaveManagerRunner");

                    runnerObject.hideFlags =
                        HideFlags.HideInHierarchy |
                        HideFlags.NotEditable;

                    GameObject.DontDestroyOnLoad(runnerObject);

                    s_runner = runnerObject.AddComponent<Runner>();
                }
            }

            return s_runner;
        }

        private static bool BelongsToScene(
            GameObject gameObject,
            string sceneName,
            Scene targetScene)
        {
            Scene objectScene = gameObject.scene;

            if (!objectScene.IsValid())
                return false;

            if (sceneName == SceneLoader.DontDestroyOnLoadSceneName)
            {
                return objectScene.name ==
                    SceneLoader.DontDestroyOnLoadSceneName;
            }

            return objectScene == targetScene;
        }

        #endregion

        #region Logging

        private static void Log(string message)
        {
            Debug.Log($"[SaveKit] {message}", s_runner);
        }

        private static void LogWarning(string message)
        {
            Debug.LogWarning($"[SaveKit] {message}", s_runner);
        }

        private static void LogError(string message)
        {
            Debug.LogError($"[SaveKit] {message}", s_runner);
        }

        private static void LogException(Exception exception)
        {
            Debug.LogException(exception, s_runner);
        }

        #endregion
    }
}
