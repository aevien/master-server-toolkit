using MasterServerToolkit.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.Utils
{
    public class ScenesLoader : DynamicSingletonBehaviour<ScenesLoader>
    {
        private List<string> loadedScenes = new List<string>();

        public static void LoadSceneByName(string sceneName, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            if (TryGetOrCreate(out var instance))
                instance.StartCoroutine(instance.LoadAsyncScene(sceneName, false, onProgress, onLoaded));
        }

        public static void LoadSceneByIndex(int sceneBuildIndex, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            if (TryGetOrCreate(out var instance))
                instance.StartCoroutine(instance.LoadAsyncScene(SceneManager.GetSceneAt(sceneBuildIndex).name, false, onProgress, onLoaded));
        }

        public static void LoadSceneByNameAdditive(string sceneName, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            if (TryGetOrCreate(out var instance))
                instance.StartCoroutine(instance.LoadAsyncScene(sceneName, true, onProgress, onLoaded));
        }

        public static void LoadSceneByIndexAdditive(int sceneBuildIndex, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            if (TryGetOrCreate(out var instance))
                instance.StartCoroutine(instance.LoadAsyncScene(SceneManager.GetSceneAt(sceneBuildIndex).name, true, onProgress, onLoaded));
        }

        IEnumerator LoadAsyncScene(string sceneName, bool isAdditive, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            if (loadedScenes.Contains(sceneName))
            {
                logger.Info($"Scene {sceneName} is already loading".ToRed());
                yield return null;
            }

            loadedScenes.Add(sceneName);

            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);

            if (asyncOperation == null) yield return null;

            asyncOperation.completed += (op) =>
            {
                loadedScenes.Remove(sceneName);
                onLoaded?.Invoke();
            };

            while (!asyncOperation.isDone)
            {
                onProgress?.Invoke(asyncOperation.progress);

                if (asyncOperation.progress >= 0.9f)
                {
                    asyncOperation.allowSceneActivation = true;
                }

                yield return null;
            }
        }

        public static void UnloadSceneByName(string sceneName, bool unloadUnusedAssets, UnityAction<float> onProgress, UnityAction onUnloaded)
        {
            if (TryGetOrCreate(out var instance))
                instance.StartCoroutine(instance.UnloadScene(sceneName, unloadUnusedAssets, onProgress, onUnloaded));
        }

        IEnumerator UnloadScene(string sceneName, bool unloadUnusedAssets, UnityAction<float> onProgress, UnityAction onUnloaded)
        {

            AsyncOperation asyncOperation = SceneManager.UnloadSceneAsync(sceneName);

            if (asyncOperation == null) yield return null;

            asyncOperation.completed += (op) =>
            {
                loadedScenes.Remove(sceneName);
                onUnloaded?.Invoke();
            };

            while (!asyncOperation.isDone)
            {
                onProgress?.Invoke(asyncOperation.progress);
                yield return null;
            }

            if (unloadUnusedAssets) yield return Resources.UnloadUnusedAssets();
        }
    }
}