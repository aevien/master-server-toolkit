using Aevien.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Aevien.UI
{
    public class ScenesLoadManager : Singleton<ScenesLoadManager>
    {
        public void LoadSceneByName(string sceneName, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            StartCoroutine(LoadAsyncScene(sceneName, onProgress, onLoaded));
        }

        public void LoadSceneByIndex(int sceneBuildIndex, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            StartCoroutine(LoadAsyncScene(SceneManager.GetSceneAt(sceneBuildIndex).name, onProgress, onLoaded));
        }

        IEnumerator LoadAsyncScene(string sceneName, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName);
            asyncOperation.completed += (op) => {
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
    }
}