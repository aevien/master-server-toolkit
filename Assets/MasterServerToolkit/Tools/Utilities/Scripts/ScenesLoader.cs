using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Utils
{
    public static class ScenesLoader
    {
        private static ScenesLoadManager sceneLoadManager;

        static ScenesLoader()
        {
            if (!sceneLoadManager)
            {
                var sceneLoaderObject = new GameObject("--SCENES_LOAD_MANAGER");
                sceneLoadManager = sceneLoaderObject.AddComponent<ScenesLoadManager>();
            }
        }

        public static void LoadSceneByName(string sceneName, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            sceneLoadManager.LoadSceneByName(sceneName, onProgress, onLoaded);
        }

        public static void LoadSceneByIndex(int sceneBuildIndex, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            sceneLoadManager.LoadSceneByIndex(sceneBuildIndex, onProgress, onLoaded);
        }

        public static void LoadSceneByNameAdditive(string sceneName, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            sceneLoadManager.LoadSceneByNameAdditive(sceneName, onProgress, onLoaded);
        }

        public static void LoadSceneByIndexAdditive(int sceneBuildIndex, UnityAction<float> onProgress, UnityAction onLoaded)
        {
            sceneLoadManager.LoadSceneByIndexAdditive(sceneBuildIndex, onProgress, onLoaded);
        }
    }
}
