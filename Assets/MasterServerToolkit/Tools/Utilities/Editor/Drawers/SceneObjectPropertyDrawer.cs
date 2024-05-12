#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MasterServerToolkit.Utils
{
    [CustomPropertyDrawer(typeof(SceneObject))]
    public class SceneObjectPropertyDrawer : PropertyDrawer
    {
        protected SceneAsset GetSceneObject(string sceneObjectName)
        {
            if (string.IsNullOrEmpty(sceneObjectName))
                return null;

            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = EditorBuildSettings.scenes[i];

                if (scene.path.IndexOf(sceneObjectName) != -1)
                {
                    return AssetDatabase.LoadAssetAtPath(scene.path, typeof(SceneAsset)) as SceneAsset;
                }
            }

            Debug.Log("Scene [" + sceneObjectName + "] cannot be used. Add this scene to the 'Scenes in the Build' in the build settings.");
            
            return null;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var sceneObj = GetSceneObject(property.FindPropertyRelative("sceneName").stringValue);
            var newScene = EditorGUI.ObjectField(position, label, sceneObj, typeof(SceneAsset), false);

            if (newScene == null)
            {
                var prop = property.FindPropertyRelative("sceneName");
                prop.stringValue = "";
            }
            else if (newScene.name != property.FindPropertyRelative("sceneName").stringValue)
            {
                var scnObj = GetSceneObject(newScene.name);

                if (scnObj == null)
                {
                    Debug.LogWarning("The scene " + newScene.name + " cannot be used. To use this scene add it to the build settings for the project.");
                }
                else
                {
                    var prop = property.FindPropertyRelative("sceneName");
                    prop.stringValue = newScene.name;
                }
            }
        }
    }
}
#endif