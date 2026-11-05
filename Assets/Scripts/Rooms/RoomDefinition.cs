using UnityEngine;

[CreateAssetMenu(fileName = "RoomDefinition", menuName = "Project Spear/Rooms/Room Definition")]
public class RoomDefinition : ScriptableObject
{
    [Header("Room Identity (assigned at registration)")]
    [SerializeField] private string roomId;
    [SerializeField] private string displayName;
    [SerializeField] private string sceneName;
    [SerializeField] private string sceneGuid;

    public string RoomId => roomId;
    public string DisplayName => displayName;
    public string SceneName => sceneName;
    public string SceneGuid => sceneGuid;

#if UNITY_EDITOR
    public void AssignIdentity(string id, string display, string scene, string guid)
    {
        roomId = id;
        displayName = display;
        sceneName = scene;
        sceneGuid = guid;
    }
#endif
}
