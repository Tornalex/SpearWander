using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomRegistry))]
public class RoomRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RoomRegistry registry = (RoomRegistry)target;

        EditorGUILayout.LabelField("Registered Rooms", EditorStyles.boldLabel);

        if (registry.Rooms.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No rooms are registered. Open a saved gameplay scene and use Tools/Project Spear/Rooms/Register Open Scene….",
                MessageType.Info);
        }
        else
        {
            for (int i = 0; i < registry.Rooms.Count; i++)
            {
                RoomDefinition definition = registry.Rooms[i];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (definition == null)
                {
                    EditorGUILayout.LabelField($"Entry {i}", "Missing RoomDefinition reference");
                }
                else
                {
                    string title = string.IsNullOrEmpty(definition.DisplayName)
                        ? definition.name
                        : definition.DisplayName;

                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Scene", definition.SceneName);
                    EditorGUILayout.LabelField("Room ID", definition.RoomId);

                    if (GUILayout.Button("Select Definition"))
                        Selection.activeObject = definition;
                }

                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Register Open Scene…"))
            RoomRegistrationService.RegisterOpenSceneMenu();
    }
}
