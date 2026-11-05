using UnityEngine;

public enum RoomConnectionDirection
{
    Bidirectional = 0,
    OneWay = 1
}

[CreateAssetMenu(fileName = "RoomConnection", menuName = "Project Spear/Rooms/Room Connection")]
public class RoomConnection : ScriptableObject
{
    [Header("Endpoint A")]
    [SerializeField] private string endpointARoomId;
    [SerializeField] private string endpointADoorId;

    [Header("Endpoint B")]
    [SerializeField] private string endpointBRoomId;
    [SerializeField] private string endpointBDoorId;

    [Header("Direction")]
    [SerializeField] private RoomConnectionDirection direction = RoomConnectionDirection.Bidirectional;

    public string EndpointARoomId => endpointARoomId;
    public string EndpointADoorId => endpointADoorId;
    public string EndpointBRoomId => endpointBRoomId;
    public string EndpointBDoorId => endpointBDoorId;
    public RoomConnectionDirection Direction => direction;

#if UNITY_EDITOR
    public void Assign(
        string roomAId,
        string doorAId,
        string roomBId,
        string doorBId,
        RoomConnectionDirection connectionDirection)
    {
        endpointARoomId = roomAId;
        endpointADoorId = doorAId;
        endpointBRoomId = roomBId;
        endpointBDoorId = doorBId;
        direction = connectionDirection;
    }
#endif
}
