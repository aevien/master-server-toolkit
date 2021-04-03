using UnityEditor;
using UnityEngine;

namespace MasterServerToolkit.Utils.Editor
{
    [CustomPropertyDrawer(typeof(HelpBox))]
    public class HelpBoxPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var info = fieldInfo.GetValue(property.serializedObject.targetObject) as HelpBox;

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.HelpBox(position, info.Text, (MessageType)info.Type);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var info = fieldInfo.GetValue(property.serializedObject.targetObject) as HelpBox;
            return info.Height;
        }
    }
}