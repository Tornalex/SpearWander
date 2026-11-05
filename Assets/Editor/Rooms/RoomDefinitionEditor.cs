using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomDefinition))]
public class RoomDefinitionEditor : Editor
{
    private SerializedProperty _displayName;

    private void OnEnable()
    {
        _displayName = serializedObject.FindProperty("displayName");
    }

    public override void OnInspectorGUI()
    {
        RoomDefinition definition = (RoomDefinition)target;

        serializedObject.Update();

        EditorGUILayout.LabelField("Room Identity", EditorStyles.boldLabel);

        if (string.IsNullOrEmpty(definition.RoomId))
        {
            EditorGUILayout.HelpBox(
                "This RoomDefinition has no identity. Register its scene with Tools/Project Spear/Rooms/Register Open Scene….",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Room ID", definition.RoomId);
            EditorGUILayout.LabelField("Scene GUID");
            EditorGUILayout.SelectableLabel(
                definition.SceneGuid,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight),
                GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("Scene Name", definition.SceneName);
            EditorGUILayout.PropertyField(_displayName);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
