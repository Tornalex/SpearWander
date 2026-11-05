using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RoomConnectionCreatorWindow : EditorWindow
{
    private RoomConnectionDirection _direction = RoomConnectionDirection.Bidirectional;
    private readonly List<RoomDefinition> _rooms = new List<RoomDefinition>();

    private int _roomAIndex;
    private int _roomBIndex;
    private int _doorAIndex;
    private int _doorBIndex;

    private readonly List<DoorChoice> _doorsA = new List<DoorChoice>();
    private readonly List<DoorChoice> _doorsB = new List<DoorChoice>();
    private string _doorsRoomGuidA;
    private string _doorsRoomGuidB;

    private string _assetName = string.Empty;
    private string _resultMessage;
    private MessageType _resultType = MessageType.Info;
    private Vector2 _scroll;

    [MenuItem("Tools/Project Spear/Rooms/Create Room Connection…")]
    public static void Open()
    {
        RoomConnectionCreatorWindow window = GetWindow<RoomConnectionCreatorWindow>("Room Connections");
        window.minSize = new Vector2(460f, 360f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshRooms();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Rooms"))
            RefreshRooms();
        if (GUILayout.Button("Load Doors"))
            LoadDoors();
        if (GUILayout.Button("Validate Connections"))
        {
            RoomValidationReport report = RoomConnectionValidator.Validate();
            _resultMessage = $"Validation: {report.ErrorCount} error(s), {report.WarningCount} warning(s).";
            _resultType = report.HasErrors ? MessageType.Error : MessageType.Info;
            RoomConnectionValidator.ValidateMenu();
        }
        EditorGUILayout.EndHorizontal();

        if (_rooms.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No rooms are registered. Register gameplay scenes with Tools/Project Spear/Rooms/Register Open Scene… first.",
                MessageType.Warning);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        _direction = (RoomConnectionDirection)EditorGUILayout.EnumPopup("Direction", _direction);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Endpoint A", EditorStyles.boldLabel);
        int newRoomAIndex = DrawRoomPopup(_roomAIndex);
        if (newRoomAIndex != _roomAIndex)
        {
            _roomAIndex = newRoomAIndex;
            _resultMessage = null;
            ResetDoors(_doorsA, ref _doorsRoomGuidA, ref _doorAIndex);
        }

        DrawDoorPopup(_roomAIndex, ref _doorAIndex, _doorsA, _doorsRoomGuidA);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Endpoint B", EditorStyles.boldLabel);
        int newRoomBIndex = DrawRoomPopup(_roomBIndex);
        if (newRoomBIndex != _roomBIndex)
        {
            _roomBIndex = newRoomBIndex;
            _resultMessage = null;
            ResetDoors(_doorsB, ref _doorsRoomGuidB, ref _doorBIndex);
        }

        DrawDoorPopup(_roomBIndex, ref _doorBIndex, _doorsB, _doorsRoomGuidB);

        EditorGUILayout.Space();
        _assetName = EditorGUILayout.TextField("Connection Asset Name", _assetName);

        string roomAId = GetRoomId(_roomAIndex);
        string roomBId = GetRoomId(_roomBIndex);
        string doorAId = GetDoorId(_doorsA, _doorAIndex);
        string doorBId = GetDoorId(_doorsB, _doorBIndex);

        bool canCreate =
            _roomAIndex != _roomBIndex &&
            !string.IsNullOrWhiteSpace(roomAId) &&
            !string.IsNullOrWhiteSpace(roomBId) &&
            !string.IsNullOrWhiteSpace(doorAId) &&
            !string.IsNullOrWhiteSpace(doorBId);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!canCreate))
        {
            if (GUILayout.Button("Create Connection", GUILayout.Height(28f)))
                CreateConnection(roomAId, roomBId, doorAId, doorBId);
        }

        if (_roomAIndex == _roomBIndex && _roomAIndex >= 0)
        {
            EditorGUILayout.HelpBox(
                "Self-connections are not supported in the MVP. Select two different rooms.",
                MessageType.Warning);
        }

        if (!string.IsNullOrEmpty(_resultMessage))
            EditorGUILayout.HelpBox(_resultMessage, _resultType);

        EditorGUILayout.EndScrollView();
    }

    private int DrawRoomPopup(int index)
    {
        string[] labels = new string[_rooms.Count];

        for (int i = 0; i < _rooms.Count; i++)
        {
            RoomDefinition room = _rooms[i];
            labels[i] = string.IsNullOrEmpty(room.DisplayName)
                ? room.name
                : $"{room.DisplayName} ({room.RoomId})";
        }

        int newIndex = EditorGUILayout.Popup("Room", Mathf.Clamp(index, 0, _rooms.Count - 1), labels);
        return newIndex;
    }

    private void DrawDoorPopup(int roomIndex, ref int doorIndex, List<DoorChoice> doors, string cachedRoomGuid)
    {
        if (roomIndex < 0 || roomIndex >= _rooms.Count)
            return;

        RoomDefinition room = _rooms[roomIndex];

        if (string.IsNullOrEmpty(cachedRoomGuid) ||
            !string.Equals(cachedRoomGuid, room.SceneGuid, StringComparison.Ordinal))
        {
            doorIndex = 0;
            EditorGUILayout.HelpBox(
                "Doors are not loaded for this room. Use Load Doors to read them.",
                MessageType.Info);
            return;
        }

        if (doors.Count == 0)
        {
            EditorGUILayout.HelpBox("This room has no doors with a stable doorId.", MessageType.Warning);
            doorIndex = 0;
            return;
        }

        string[] labels = new string[doors.Count];

        for (int i = 0; i < doors.Count; i++)
        {
            DoorChoice door = doors[i];
            string stable = string.IsNullOrWhiteSpace(door.DoorId) ? "<missing doorId>" : door.DoorId;
            string legacy = string.IsNullOrWhiteSpace(door.LegacyDoorId) ? string.Empty : $" / legacy '{door.LegacyDoorId}'";
            string connected = door.Connection != null ? $" / connected to '{door.Connection.name}'" : string.Empty;
            labels[i] = $"{stable}{legacy}{connected}";
        }

        doorIndex = EditorGUILayout.Popup("Door", Mathf.Clamp(doorIndex, 0, doors.Count - 1), labels);
    }

    private void LoadDoors()
    {
        _resultMessage = null;

        if (_roomAIndex == _roomBIndex)
        {
            if (LoadDoorsFor(_roomAIndex, _doorsA, ref _doorsRoomGuidA, ref _doorAIndex))
            {
                _doorsB.Clear();
                _doorsB.AddRange(_doorsA);
                _doorsRoomGuidB = _doorsRoomGuidA;
                _doorBIndex = _doorAIndex;
            }
            else
            {
                ResetDoors(_doorsB, ref _doorsRoomGuidB, ref _doorBIndex);
            }
        }
        else
        {
            LoadDoorsFor(_roomAIndex, _doorsA, ref _doorsRoomGuidA, ref _doorAIndex);
            LoadDoorsFor(_roomBIndex, _doorsB, ref _doorsRoomGuidB, ref _doorBIndex);
        }

        Repaint();
    }

    private bool LoadDoorsFor(
        int roomIndex,
        List<DoorChoice> doors,
        ref string cachedRoomGuid,
        ref int doorIndex)
    {
        if (roomIndex < 0 || roomIndex >= _rooms.Count)
            return false;

        RoomDefinition room = _rooms[roomIndex];
        List<DiscoveredDoor> discovered = RoomDiscoveryService.DiscoverDoors(room, out string message);

        if (discovered == null)
        {
            ResetDoors(doors, ref cachedRoomGuid, ref doorIndex);
            _resultMessage = message;
            _resultType = MessageType.Error;
            return false;
        }

        doors.Clear();
        cachedRoomGuid = room.SceneGuid;
        doorIndex = 0;

        foreach (DiscoveredDoor door in discovered)
        {
            doors.Add(new DoorChoice(
                door.StableDoorId,
                door.DoorID,
                door.Connection));
        }

        return true;
    }

    private static void ResetDoors(List<DoorChoice> doors, ref string cachedRoomGuid, ref int doorIndex)
    {
        doors.Clear();
        cachedRoomGuid = null;
        doorIndex = 0;
    }

    private void CreateConnection(string roomAId, string roomBId, string doorAId, string doorBId)
    {
        RoomDefinition roomA = _rooms[_roomAIndex];
        RoomDefinition roomB = _rooms[_roomBIndex];

        bool created = RoomConnectionService.CreateConnection(
            roomA,
            doorAId,
            roomB,
            doorBId,
            _direction,
            _assetName,
            out string message);

        _resultMessage = message;
        _resultType = created ? MessageType.Info : MessageType.Error;

        ResetDoors(_doorsA, ref _doorsRoomGuidA, ref _doorAIndex);
        ResetDoors(_doorsB, ref _doorsRoomGuidB, ref _doorBIndex);

        if (created)
            _assetName = string.Empty;
    }

    private void RefreshRooms()
    {
        _rooms.Clear();

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry != null && registry.Rooms != null)
        {
            foreach (RoomDefinition room in registry.Rooms)
            {
                if (room != null)
                    _rooms.Add(room);
            }
        }

        _roomAIndex = Mathf.Clamp(_roomAIndex, 0, Mathf.Max(0, _rooms.Count - 1));
        _roomBIndex = Mathf.Clamp(_roomBIndex + 1 < _rooms.Count ? _roomBIndex + 1 : 0, 0, Mathf.Max(0, _rooms.Count - 1));

        ResetDoors(_doorsA, ref _doorsRoomGuidA, ref _doorAIndex);
        ResetDoors(_doorsB, ref _doorsRoomGuidB, ref _doorBIndex);
        Repaint();
    }

    private string GetRoomId(int index)
    {
        return index >= 0 && index < _rooms.Count ? _rooms[index].RoomId : null;
    }

    private static string GetDoorId(List<DoorChoice> doors, int index)
    {
        return index >= 0 && index < doors.Count ? doors[index].DoorId : null;
    }

    private readonly struct DoorChoice
    {
        public DoorChoice(string doorId, string legacyDoorId, RoomConnection connection)
        {
            DoorId = doorId;
            LegacyDoorId = legacyDoorId;
            Connection = connection;
        }

        public string DoorId { get; }
        public string LegacyDoorId { get; }
        public RoomConnection Connection { get; }
    }
}
