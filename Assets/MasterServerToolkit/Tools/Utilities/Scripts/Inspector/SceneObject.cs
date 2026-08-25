using UnityEngine;

namespace MasterServerToolkit.Utils
{
    [System.Serializable]
    public class SceneObject
    {
        [SerializeField]
        [Tooltip("Name of the scene selected by the SceneObject drawer. The scene must be included in Build Settings; an empty value means no scene is assigned.")]
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
