using UnityEngine;

namespace MasterServerToolkit.Utils
{
    [System.Serializable]
    public class SceneObject
    {
        [SerializeField]
        private string sceneName;

        public static implicit operator string(SceneObject sceneObject)
        {
            return sceneObject.sceneName;
        }

        public static implicit operator SceneObject(string sceneName)
        {
            return new SceneObject() { sceneName = sceneName };
        }
    }
}
