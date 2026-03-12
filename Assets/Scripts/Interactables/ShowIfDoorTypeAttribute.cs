using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShowIfDoorTypeAttribute : PropertyAttribute
{
    public DoorHandler.DoorType[] requiredDoorTypes;

    public ShowIfDoorTypeAttribute(params DoorHandler.DoorType[] doorTypes)
    {
        requiredDoorTypes = doorTypes;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ShowIfDoorTypeAttribute))]
public class ShowIfDoorTypeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ShowIfDoorTypeAttribute attr = attribute as ShowIfDoorTypeAttribute;
        SerializedObject serializedObject = property.serializedObject;
        SerializedProperty doorTypeProp = serializedObject.FindProperty("doorType");

        if (doorTypeProp != null)
        {
            DoorHandler.DoorType currentDoorType = (DoorHandler.DoorType)doorTypeProp.enumValueIndex;
            bool shouldShow = false;

            foreach (DoorHandler.DoorType doorType in attr.requiredDoorTypes)
            {
                if (currentDoorType == doorType)
                {
                    shouldShow = true;
                    break;
                }
            }

            if (shouldShow)
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ShowIfDoorTypeAttribute attr = attribute as ShowIfDoorTypeAttribute;
        SerializedObject serializedObject = property.serializedObject;
        SerializedProperty doorTypeProp = serializedObject.FindProperty("doorType");

        if (doorTypeProp != null)
        {
            DoorHandler.DoorType currentDoorType = (DoorHandler.DoorType)doorTypeProp.enumValueIndex;
            bool shouldShow = false;

            foreach (DoorHandler.DoorType doorType in attr.requiredDoorTypes)
            {
                if (currentDoorType == doorType)
                {
                    shouldShow = true;
                    break;
                }
            }

            if (shouldShow)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }
        }
        return 0f;
    }
}
#endif
