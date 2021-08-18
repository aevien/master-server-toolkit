using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.Utils
{
    public class ScenesLoadManager : SingletonBehaviour<ScenesLoadManager>
    {
        public void LoadSceneByName(string sceneName, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            StartCoroutine(LoadAsyncScene(sceneName, false, onProgress, onLoaded));
        }

        public void LoadSceneByIndex(int sceneBuildIndex, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            StartCoroutine(LoadAsyncScene(SceneManager.GetSceneAt(sceneBuildIndex).name, false, onProgress, onLoaded));
        }

        public void LoadSceneByNameAdditive(string sceneName, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            StartCoroutine(LoadAsyncScene(sceneName, true, onProgress, onLoaded));
        }

        public void LoadSceneByIndexAdditive(int sceneBuildIndex, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            StartCoroutine(LoadAsyncScene(SceneManager.GetSceneAt(sceneBuildIndex).name, true, onProgress, onLoaded));
        }

        IEnumerator LoadAsyncScene(string sceneName, bool isAdditive, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            var scene = SceneManager.GetSceneByName(sceneName);

            if (scene.isLoaded)
            {
                onLoaded?.Invoke();
                yield return null;
            }
            else
            {
                AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(sceneName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);

                if(asyncOperation == null) yield return null;

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
}