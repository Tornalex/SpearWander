using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum RoomValidationSeverity
{
    Error = 0,
    Warning = 1
}

public readonly struct RoomValidationEntry
{
    public RoomValidationEntry(string ruleId, RoomValidationSeverity severity, string message, UnityEngine.Object context)
    {
        RuleId = ruleId;
        Severity = severity;
        Message = message;
        Context = context;
    }

    public string RuleId { get; }
    public RoomValidationSeverity Severity { get; }
    public string Message { get; }
    public UnityEngine.Object Context { get; }
}

public sealed class RoomValidationReport
{
    private readonly List<RoomValidationEntry> _entries = new List<RoomValidationEntry>();

    public IReadOnlyList<RoomValidationEntry> Entries => _entries;
    public bool HasErrors => ErrorCount > 0;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }

    public void AddError(string ruleId, string message, UnityEngine.Object context = null)
    {
        _entries.Add(new RoomValidationEntry(ruleId, RoomValidationSeverity.Error, message, context));
        ErrorCount++;
    }

    public void AddWarning(string ruleId, string message, UnityEngine.Object context = null)
    {
        _entries.Add(new RoomValidationEntry(ruleId, RoomValidationSeverity.Warning, message, context));
        WarningCount++;
    }

    public bool HasRule(string ruleId)
    {
        foreach (RoomValidationEntry entry in _entries)
        {
            if (string.Equals(entry.RuleId, ruleId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

public static class RoomConnectionValidator
{
    public const string MissingRegistry = "RC001";
    public const string InvalidRoomDefinition = "RC002";
    public const string MissingRoomId = "RC003";
    public const string DuplicateRoomId = "RC004";
    public const string MissingSceneName = "RC005";
    public const string MissingSceneGuid = "RC006";
    public const string DuplicateSceneName = "RC007";
    public const string DuplicateSceneGuid = "RC008";
    public const string InvalidScenePath = "RC009";
    public const string MissingDoorId = "RC010";
    public const string DuplicateDoorId = "RC011";
    public const string InvalidConnectionAsset = "RC012";
    public const string MissingEndpointRoomId = "RC013";
    public const string MissingEndpointDoorId = "RC014";
    public const string InvalidConnectionDirection = "RC015";
    public const string SelfConnection = "RC016";
    public const string IdenticalEndpoints = "RC017";
    public const string UnregisteredEndpointRoom = "RC018";
    public const string MissingEndpointDoor = "RC019";
    public const string MultipleConnectionsForDoor = "RC020";
    public const string MissingBacklink = "RC021";
    public const string InconsistentBacklink = "RC022";
    public const string OrphanBacklink = "RC023";
    public const string UnconnectedDoor = "W101";

    [MenuItem("Tools/Project Spear/Rooms/Validate Room Connections")]
    public static void ValidateMenu()
    {
        RoomValidationReport report = Validate();

        foreach (RoomValidationEntry entry in report.Entries)
        {
            string prefix = entry.Severity == RoomValidationSeverity.Error ? "ERROR" : "WARNING";
            string line = $"[RoomValidation] {prefix} {entry.RuleId}: {entry.Message}";

            if (entry.Severity == RoomValidationSeverity.Error)
                Debug.LogError(line, entry.Context);
            else
                Debug.LogWarning(line, entry.Context);
        }

        Debug.Log($"[RoomValidation] Validation complete: {report.ErrorCount} error(s), {report.WarningCount} warning(s).");
    }

    /// <summary>
    /// Read-only validation of room definitions, door identities and room connections.
    /// No asset, scene or reference is modified or repaired.
    /// </summary>
    public static RoomValidationReport Validate()
    {
        return Validate(null);
    }

    internal static RoomValidationReport Validate(
        IReadOnlyDictionary<RoomDefinition, DoorDiscoveryResult> doorSnapshot)
    {
        RoomValidationReport report = new RoomValidationReport();

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry == null)
        {
            report.AddError(MissingRegistry, $"RoomRegistry asset is missing at '{RoomRegistrationService.RegistryPath}'.");
            return report;
        }

        if (registry.Rooms == null)
        {
            report.AddError(MissingRegistry, "RoomRegistry has no room list.");
            return report;
        }

        HashSet<string> roomIds = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, List<RoomDefinition>> roomsById = new Dictionary<string, List<RoomDefinition>>(StringComparer.Ordinal);
        Dictionary<string, RoomDefinition> roomsByName = new Dictionary<string, RoomDefinition>(StringComparer.Ordinal);
        Dictionary<string, RoomDefinition> roomsByGuid = new Dictionary<string, RoomDefinition>(StringComparer.Ordinal);
        List<RoomDefinition> scannableRooms = new List<RoomDefinition>();

        foreach (RoomDefinition definition in registry.Rooms)
        {
            if (definition == null)
            {
                report.AddError(InvalidRoomDefinition, "RoomRegistry contains a missing RoomDefinition reference.");
                continue;
            }

            string display = string.IsNullOrEmpty(definition.DisplayName) ? definition.name : definition.DisplayName;

            if (string.IsNullOrWhiteSpace(definition.RoomId))
            {
                report.AddError(MissingRoomId, $"RoomDefinition '{display}' has no room ID.", definition);
            }
            else
            {
                roomIds.Add(definition.RoomId);

                if (!roomsById.TryGetValue(definition.RoomId, out List<RoomDefinition> list))
                {
                    list = new List<RoomDefinition>();
                    roomsById.Add(definition.RoomId, list);
                }

                list.Add(definition);
            }

            if (string.IsNullOrWhiteSpace(definition.SceneName))
                report.AddError(MissingSceneName, $"RoomDefinition '{display}' has no scene name.", definition);
            else if (roomsByName.TryGetValue(definition.SceneName, out RoomDefinition sameName))
                report.AddError(DuplicateSceneName,
                    $"RoomDefinition '{display}' and '{sameName.name}' both use scene name '{definition.SceneName}'.",
                    definition);
            else
                roomsByName.Add(definition.SceneName, definition);

            if (string.IsNullOrWhiteSpace(definition.SceneGuid))
            {
                report.AddError(MissingSceneGuid, $"RoomDefinition '{display}' has no scene GUID.", definition);
            }
            else
            {
                if (roomsByGuid.TryGetValue(definition.SceneGuid, out RoomDefinition sameGuid))
                {
                    report.AddError(DuplicateSceneGuid,
                        $"RoomDefinition '{display}' and '{sameGuid.name}' both use scene GUID '{definition.SceneGuid}'.",
                        definition);
                }
                else
                {
                    roomsByGuid.Add(definition.SceneGuid, definition);

                    string scenePath = AssetDatabase.GUIDToAssetPath(definition.SceneGuid);
                    if (string.IsNullOrEmpty(scenePath))
                    {
                        report.AddError(InvalidScenePath,
                            $"RoomDefinition '{display}' points to a scene GUID that does not resolve to a path.",
                            definition);
                    }
                    else if (!string.IsNullOrWhiteSpace(definition.RoomId))
                    {
                        scannableRooms.Add(definition);
                    }
                }
            }
        }

        foreach (KeyValuePair<string, List<RoomDefinition>> pair in roomsById)
        {
            if (pair.Value.Count > 1)
            {
                report.AddError(DuplicateRoomId,
                    $"Room ID '{pair.Key}' is used by {pair.Value.Count} RoomDefinitions.",
                    pair.Value[0]);
            }
        }

        Dictionary<EndpointKey, List<DoorRecord>> doorsByEndpoint =
            new Dictionary<EndpointKey, List<DoorRecord>>();
        List<DoorRecord> doorRecords = new List<DoorRecord>();

        foreach (RoomDefinition room in scannableRooms)
        {
            List<DiscoveredDoor> doors;
            string message;

            if (doorSnapshot != null &&
                doorSnapshot.TryGetValue(room, out DoorDiscoveryResult discovery))
            {
                doors = discovery.Doors;
                message = discovery.Message;
            }
            else
            {
                doors = RoomDiscoveryService.DiscoverDoors(room, out message);
            }

            if (doors == null)
            {
                report.AddError(InvalidScenePath, message, room);
                continue;
            }

            foreach (DiscoveredDoor door in doors)
            {
                if (string.IsNullOrWhiteSpace(door.StableDoorId))
                {
                    report.AddError(MissingDoorId,
                        $"Door '{door.DoorID}' (object '{door.GameObjectName}') in room '{room.RoomId}' has no stable doorId.",
                        room);
                    continue;
                }

                DoorRecord record = new DoorRecord(room, door, new EndpointKey(room.RoomId, door.StableDoorId));
                doorRecords.Add(record);

                if (!doorsByEndpoint.TryGetValue(record.Key, out List<DoorRecord> list))
                {
                    list = new List<DoorRecord>();
                    doorsByEndpoint.Add(record.Key, list);
                }

                list.Add(record);
            }
        }

        foreach (KeyValuePair<EndpointKey, List<DoorRecord>> pair in doorsByEndpoint)
        {
            if (pair.Value.Count > 1)
            {
                report.AddError(DuplicateDoorId,
                    $"Door ID '{pair.Key.DoorId}' appears {pair.Value.Count} times in room '{pair.Key.RoomId}'.",
                    pair.Value[0].Room);
            }
        }

        List<RoomConnection> connections = LoadConnections(report);
        HashSet<RoomConnection> connectionSet = new HashSet<RoomConnection>();
        Dictionary<EndpointKey, List<RoomConnection>> endpointUsage =
            new Dictionary<EndpointKey, List<RoomConnection>>();

        foreach (RoomConnection connection in connections)
        {
            connectionSet.Add(connection);

            EndpointKey keyA = new EndpointKey(connection.EndpointARoomId, connection.EndpointADoorId);
            EndpointKey keyB = new EndpointKey(connection.EndpointBRoomId, connection.EndpointBDoorId);

            bool roomAValid = !string.IsNullOrWhiteSpace(connection.EndpointARoomId);
            bool roomBValid = !string.IsNullOrWhiteSpace(connection.EndpointBRoomId);
            bool doorAValid = !string.IsNullOrWhiteSpace(connection.EndpointADoorId);
            bool doorBValid = !string.IsNullOrWhiteSpace(connection.EndpointBDoorId);

            if (!roomAValid)
                report.AddError(MissingEndpointRoomId, $"Connection '{connection.name}' has no endpoint A room ID.", connection);
            if (!roomBValid)
                report.AddError(MissingEndpointRoomId, $"Connection '{connection.name}' has no endpoint B room ID.", connection);
            if (!doorAValid)
                report.AddError(MissingEndpointDoorId, $"Connection '{connection.name}' has no endpoint A door ID.", connection);
            if (!doorBValid)
                report.AddError(MissingEndpointDoorId, $"Connection '{connection.name}' has no endpoint B door ID.", connection);

            if (connection.Direction != RoomConnectionDirection.Bidirectional &&
                connection.Direction != RoomConnectionDirection.OneWay)
            {
                report.AddError(InvalidConnectionDirection,
                    $"Connection '{connection.name}' has an invalid direction value ({(int)connection.Direction}).",
                    connection);
            }

            if (roomAValid && roomBValid &&
                string.Equals(connection.EndpointARoomId, connection.EndpointBRoomId, StringComparison.Ordinal))
            {
                report.AddError(SelfConnection,
                    $"Connection '{connection.name}' connects room '{connection.EndpointARoomId}' to itself; self-connections are not supported in the MVP.",
                    connection);
            }

            if (roomAValid && roomBValid && doorAValid && doorBValid &&
                string.Equals(connection.EndpointARoomId, connection.EndpointBRoomId, StringComparison.Ordinal) &&
                string.Equals(connection.EndpointADoorId, connection.EndpointBDoorId, StringComparison.Ordinal))
            {
                report.AddError(IdenticalEndpoints,
                    $"Connection '{connection.name}' has identical endpoints.",
                    connection);
            }

            if (roomAValid && !roomIds.Contains(connection.EndpointARoomId))
            {
                report.AddError(UnregisteredEndpointRoom,
                    $"Connection '{connection.name}' endpoint A references unknown room '{connection.EndpointARoomId}'.",
                    connection);
            }

            if (roomBValid && !roomIds.Contains(connection.EndpointBRoomId))
            {
                report.AddError(UnregisteredEndpointRoom,
                    $"Connection '{connection.name}' endpoint B references unknown room '{connection.EndpointBRoomId}'.",
                    connection);
            }

            if (roomAValid && doorAValid && roomIds.Contains(connection.EndpointARoomId) &&
                !doorsByEndpoint.ContainsKey(keyA))
            {
                report.AddError(MissingEndpointDoor,
                    $"Connection '{connection.name}' endpoint A references missing door '{connection.EndpointADoorId}' in room '{connection.EndpointARoomId}'.",
                    connection);
            }

            if (roomBValid && doorBValid && roomIds.Contains(connection.EndpointBRoomId) &&
                !doorsByEndpoint.ContainsKey(keyB))
            {
                report.AddError(MissingEndpointDoor,
                    $"Connection '{connection.name}' endpoint B references missing door '{connection.EndpointBDoorId}' in room '{connection.EndpointBRoomId}'.",
                    connection);
            }

            RegisterUsage(endpointUsage, keyA, connection, roomAValid, doorAValid);
            RegisterUsage(endpointUsage, keyB, connection, roomBValid, doorBValid);
        }

        foreach (KeyValuePair<EndpointKey, List<RoomConnection>> usage in endpointUsage)
        {
            if (usage.Value.Count <= 1)
                continue;

            report.AddError(MultipleConnectionsForDoor,
                $"Door '{usage.Key.DoorId}' in room '{usage.Key.RoomId}' is assigned to {usage.Value.Count} connections.",
                usage.Value[0]);
        }

        foreach (RoomConnection connection in connections)
        {
            ValidateBacklink(report, connection, new EndpointKey(connection.EndpointARoomId, connection.EndpointADoorId), doorsByEndpoint, connection.EndpointARoomId, connection.EndpointADoorId);
            ValidateBacklink(report, connection, new EndpointKey(connection.EndpointBRoomId, connection.EndpointBDoorId), doorsByEndpoint, connection.EndpointBRoomId, connection.EndpointBDoorId);
        }

        HashSet<EndpointKey> reportedUnconnected = new HashSet<EndpointKey>();

        foreach (DoorRecord record in doorRecords)
        {
            RoomConnection backlink = record.Door.Connection;

            if (backlink != null && !connectionSet.Contains(backlink))
            {
                report.AddError(InconsistentBacklink,
                    $"Door '{record.Key.DoorId}' in room '{record.Key.RoomId}' points to a missing connection asset.",
                    record.Room);
            }

            bool usedByConnection = endpointUsage.ContainsKey(record.Key);

            if (backlink == null && !usedByConnection && reportedUnconnected.Add(record.Key))
            {
                report.AddWarning(UnconnectedDoor,
                    $"Door '{record.Key.DoorId}' in room '{record.Key.RoomId}' is not connected.",
                    record.Room);
            }

            if (backlink != null && connectionSet.Contains(backlink) && !ConnectionUsesEndpoint(backlink, record.Key))
            {
                report.AddError(OrphanBacklink,
                    $"Door '{record.Key.DoorId}' in room '{record.Key.RoomId}' points to connection '{backlink.name}', but that connection does not list this endpoint.",
                    backlink);
            }
        }

        return report;
    }

    private static List<RoomConnection> LoadConnections(RoomValidationReport report)
    {
        string[] guids = AssetDatabase.FindAssets("t:RoomConnection");
        List<string> paths = new List<string>(guids.Length);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                paths.Add(path);
        }

        paths.Sort(StringComparer.Ordinal);

        List<RoomConnection> connections = new List<RoomConnection>(paths.Count);

        foreach (string path in paths)
        {
            RoomConnection connection = AssetDatabase.LoadAssetAtPath<RoomConnection>(path);

            if (connection == null)
            {
                report.AddError(InvalidConnectionAsset, $"Could not load RoomConnection asset at '{path}'.");
                continue;
            }

            connections.Add(connection);
        }

        return connections;
    }

    private static void RegisterUsage(
        Dictionary<EndpointKey, List<RoomConnection>> usage,
        EndpointKey key,
        RoomConnection connection,
        bool roomValid,
        bool doorValid)
    {
        if (!roomValid || !doorValid)
            return;

        if (!usage.TryGetValue(key, out List<RoomConnection> list))
        {
            list = new List<RoomConnection>();
            usage.Add(key, list);
        }

        if (!list.Contains(connection))
            list.Add(connection);
    }

    private static void ValidateBacklink(
        RoomValidationReport report,
        RoomConnection connection,
        EndpointKey key,
        Dictionary<EndpointKey, List<DoorRecord>> doorsByEndpoint,
        string roomId,
        string doorId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(doorId))
            return;

        if (!doorsByEndpoint.TryGetValue(key, out List<DoorRecord> records))
            return;

        foreach (DoorRecord record in records)
        {
            if (record.Door.Connection == null)
            {
                report.AddError(MissingBacklink,
                    $"Door '{doorId}' in room '{roomId}' is an endpoint of connection '{connection.name}' but has no backlink.",
                    connection);
            }
            else if (record.Door.Connection != connection)
            {
                report.AddError(InconsistentBacklink,
                    $"Door '{doorId}' in room '{roomId}' points to connection '{record.Door.Connection.name}' instead of '{connection.name}'.",
                    connection);
            }
        }
    }

    private static bool ConnectionUsesEndpoint(RoomConnection connection, EndpointKey key)
    {
        return (string.Equals(connection.EndpointARoomId, key.RoomId, StringComparison.Ordinal) &&
                string.Equals(connection.EndpointADoorId, key.DoorId, StringComparison.Ordinal)) ||
               (string.Equals(connection.EndpointBRoomId, key.RoomId, StringComparison.Ordinal) &&
                string.Equals(connection.EndpointBDoorId, key.DoorId, StringComparison.Ordinal));
    }

    private readonly struct EndpointKey : IEquatable<EndpointKey>
    {
        public EndpointKey(string roomId, string doorId)
        {
            RoomId = roomId ?? string.Empty;
            DoorId = doorId ?? string.Empty;
        }

        public string RoomId { get; }
        public string DoorId { get; }

        public bool Equals(EndpointKey other)
        {
            return string.Equals(RoomId, other.RoomId, StringComparison.Ordinal) &&
                   string.Equals(DoorId, other.DoorId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EndpointKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((RoomId != null ? RoomId.GetHashCode() : 0) * 397) ^
                       (DoorId != null ? DoorId.GetHashCode() : 0);
            }
        }
    }

    private sealed class DoorRecord
    {
        public DoorRecord(RoomDefinition room, DiscoveredDoor door, EndpointKey key)
        {
            Room = room;
            Door = door;
            Key = key;
        }

        public RoomDefinition Room { get; }
        public DiscoveredDoor Door { get; }
        public EndpointKey Key { get; }
    }
}
