using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum LegacyMigrationClassification
{
    SafeBidirectional = 0,
    SafeOneWay = 1,
    Incomplete = 2,
    UnresolvedRoom = 3,
    UnresolvedDoor = 4,
    Ambiguous = 5,
    ConflictWithExistingConnection = 6,
    AlreadyMigrated = 7,
    DuplicateDoorId = 8,
    MissingStableDoorId = 9,
    SelfReference = 10
}

public sealed class LegacyEndpointSnapshot
{
    public LegacyEndpointSnapshot(
        RoomDefinition room,
        string roomId,
        string doorId,
        string legacyDoorId,
        string targetSceneName,
        string targetDoorId)
    {
        Room = room;
        RoomId = roomId ?? string.Empty;
        DoorId = doorId ?? string.Empty;
        LegacyDoorId = legacyDoorId ?? string.Empty;
        TargetSceneName = targetSceneName ?? string.Empty;
        TargetDoorId = targetDoorId ?? string.Empty;
    }

    public RoomDefinition Room { get; }
    public string RoomId { get; }
    public string DoorId { get; }
    public string LegacyDoorId { get; }
    public string TargetSceneName { get; }
    public string TargetDoorId { get; }

    public string Label
    {
        get
        {
            string room = string.IsNullOrEmpty(RoomId) ? "<no room>" : RoomId;
            string door = string.IsNullOrEmpty(DoorId) ? "<no doorId>" : DoorId;
            return $"{room}/{door}";
        }
    }
}

public sealed class LegacyMigrationProposal
{
    public string Key { get; set; }
    public LegacyEndpointSnapshot A { get; set; }
    public LegacyEndpointSnapshot B { get; set; }
    public RoomConnectionDirection Direction { get; set; }
    public LegacyMigrationClassification Classification { get; set; }
    public string Reason { get; set; }
    public string TargetDescription { get; set; }

    public bool IsSafeToApply =>
        Classification == LegacyMigrationClassification.SafeBidirectional ||
        Classification == LegacyMigrationClassification.SafeOneWay;

    public string ClassificationLabel
    {
        get
        {
            switch (Classification)
            {
                case LegacyMigrationClassification.SafeBidirectional:
                    return "Safe / Bidirectional";
                case LegacyMigrationClassification.SafeOneWay:
                    return "Safe / OneWay";
                case LegacyMigrationClassification.Incomplete:
                    return "Incomplete";
                case LegacyMigrationClassification.UnresolvedRoom:
                    return "Unresolved room";
                case LegacyMigrationClassification.UnresolvedDoor:
                    return "Unresolved door";
                case LegacyMigrationClassification.Ambiguous:
                    return "Ambiguous";
                case LegacyMigrationClassification.ConflictWithExistingConnection:
                    return "Conflicts with existing connection";
                case LegacyMigrationClassification.AlreadyMigrated:
                    return "Already migrated";
                case LegacyMigrationClassification.DuplicateDoorId:
                    return "Duplicate door ID";
                case LegacyMigrationClassification.MissingStableDoorId:
                    return "Missing door ID";
                case LegacyMigrationClassification.SelfReference:
                    return "Self reference";
                default:
                    return Classification.ToString();
            }
        }
    }

    public string DisplayLabel
    {
        get
        {
            string source = A != null ? A.Label : "<unknown>";
            string target = B != null ? B.Label : TargetDescription;
            string direction = IsSafeToApply ? $" ({Direction})" : string.Empty;
            return $"{source} -> {target}{direction}";
        }
    }
}

public sealed class LegacyMigrationReport
{
    public DoorIdAnalysisReport DoorIds { get; set; }
    public bool HasIncompleteDoorScan { get; set; }
    public List<LegacyMigrationProposal> Proposals { get; set; } = new List<LegacyMigrationProposal>();
    public List<string> Messages { get; } = new List<string>();

    public int SafeCount
    {
        get
        {
            int count = 0;
            foreach (LegacyMigrationProposal proposal in Proposals)
            {
                if (proposal.IsSafeToApply)
                    count++;
            }

            return count;
        }
    }

    public int BidirectionalCount
    {
        get
        {
            int count = 0;
            foreach (LegacyMigrationProposal proposal in Proposals)
            {
                if (proposal.Classification == LegacyMigrationClassification.SafeBidirectional)
                    count++;
            }

            return count;
        }
    }

    public int OneWayCount
    {
        get
        {
            int count = 0;
            foreach (LegacyMigrationProposal proposal in Proposals)
            {
                if (proposal.Classification == LegacyMigrationClassification.SafeOneWay)
                    count++;
            }

            return count;
        }
    }

    public int AlreadyMigratedCount
    {
        get
        {
            int count = 0;
            foreach (LegacyMigrationProposal proposal in Proposals)
            {
                if (proposal.Classification == LegacyMigrationClassification.AlreadyMigrated)
                    count++;
            }

            return count;
        }
    }

    public int UnsafeCount => Proposals.Count - SafeCount;

    public LegacyMigrationProposal FindByKey(string key)
    {
        foreach (LegacyMigrationProposal proposal in Proposals)
        {
            if (string.Equals(proposal.Key, key, StringComparison.Ordinal))
                return proposal;
        }

        return null;
    }
}

/// <summary>
/// Read-only analysis of legacy doorway data. Resolves legacy scene names through
/// registered RoomDefinitions and legacy target door IDs through discovered doorways.
/// Never opens a connection, modifies a scene, or saves an asset.
/// </summary>
public static class LegacyMigrationAnalyzer
{
    private const string EndpointSeparator = "\u001f";
    private const string PairSeparator = "\u001e";

    public static LegacyMigrationReport Analyze()
    {
        LegacyMigrationReport report = new LegacyMigrationReport();
        DoorIdAnalysisReport doorIds = DoorIdMigrationService.Analyze();
        report.DoorIds = doorIds;
        report.HasIncompleteDoorScan = !doorIds.IsComplete;

        foreach (string message in doorIds.Messages)
            report.Messages.Add(message);

        if (!TryLoadRegistry(out RoomRegistry registry, out string registryMessage))
        {
            report.HasIncompleteDoorScan = true;
            report.Messages.Add(registryMessage);
            return report;
        }

        Dictionary<RoomDefinition, List<DoorIdAnalysisEntry>> entriesByRoom =
            new Dictionary<RoomDefinition, List<DoorIdAnalysisEntry>>();
        Dictionary<string, List<RoomDefinition>> roomsBySceneName =
            new Dictionary<string, List<RoomDefinition>>(StringComparer.Ordinal);
        List<RoomDefinition> rooms = new List<RoomDefinition>();

        foreach (RoomDefinition room in registry.Rooms)
        {
            if (room == null)
                continue;

            rooms.Add(room);

            if (!string.IsNullOrWhiteSpace(room.SceneName))
            {
                if (!roomsBySceneName.TryGetValue(room.SceneName, out List<RoomDefinition> list))
                {
                    list = new List<RoomDefinition>();
                    roomsBySceneName.Add(room.SceneName, list);
                }

                list.Add(room);
            }
        }

        foreach (DoorIdAnalysisEntry entry in doorIds.Entries)
        {
            if (entry.Room == null)
                continue;

            if (!entriesByRoom.TryGetValue(entry.Room, out List<DoorIdAnalysisEntry> list))
            {
                list = new List<DoorIdAnalysisEntry>();
                entriesByRoom.Add(entry.Room, list);
            }

            list.Add(entry);
        }

        ConnectionIndex connections = ConnectionIndex.Build(report.Messages);
        Dictionary<string, LegacyMigrationProposal> pairProposals =
            new Dictionary<string, LegacyMigrationProposal>(StringComparer.Ordinal);

        foreach (RoomDefinition room in rooms)
        {
            if (!entriesByRoom.TryGetValue(room, out List<DoorIdAnalysisEntry> entries))
                continue;

            foreach (DoorIdAnalysisEntry entry in entries)
            {
                LegacyMigrationProposal proposal = Evaluate(
                    entry,
                    roomsBySceneName,
                    entriesByRoom,
                    connections);

                if (proposal == null)
                    continue;

                if (string.IsNullOrEmpty(proposal.Key))
                {
                    report.Proposals.Add(proposal);
                    continue;
                }

                if (!pairProposals.TryGetValue(proposal.Key, out LegacyMigrationProposal existing))
                {
                    pairProposals.Add(proposal.Key, proposal);
                    report.Proposals.Add(proposal);
                    continue;
                }

                MergePairProposal(existing, proposal);
            }
        }

        return report;
    }

    private static bool TryLoadRegistry(out RoomRegistry registry, out string message)
    {
        registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);

        if (registry == null)
        {
            message = $"RoomRegistry is missing at '{RoomRegistrationService.RegistryPath}'. Register rooms first.";
            return false;
        }

        message = null;
        return true;
    }

    private static LegacyMigrationProposal Evaluate(
        DoorIdAnalysisEntry entry,
        Dictionary<string, List<RoomDefinition>> roomsBySceneName,
        Dictionary<RoomDefinition, List<DoorIdAnalysisEntry>> entriesByRoom,
        ConnectionIndex connections)
    {
        string targetScene = entry.TargetSceneName;
        string targetDoor = entry.TargetDoorId;

        if (string.IsNullOrWhiteSpace(targetScene) && string.IsNullOrWhiteSpace(targetDoor))
            return null;

        LegacyEndpointSnapshot source = new LegacyEndpointSnapshot(
            entry.Room, entry.RoomId, entry.StableDoorId,
            entry.LegacyDoorId, entry.TargetSceneName, entry.TargetDoorId);

        if (string.IsNullOrWhiteSpace(targetScene) || string.IsNullOrWhiteSpace(targetDoor))
        {
            return Unmergeable(
                entry,
                LegacyMigrationClassification.Incomplete,
                $"Legacy target is incomplete: targetSceneName='{targetScene}', targetDoorID='{targetDoor}'.",
                $"{targetScene}/{targetDoor}");
        }

        if (!roomsBySceneName.TryGetValue(targetScene, out List<RoomDefinition> targetRooms) ||
            targetRooms.Count == 0)
        {
            return Unmergeable(
                entry,
                LegacyMigrationClassification.UnresolvedRoom,
                $"No registered room uses scene name '{targetScene}'.",
                $"{targetScene}/{targetDoor}");
        }

        if (targetRooms.Count > 1)
        {
            return Unmergeable(
                entry,
                LegacyMigrationClassification.Ambiguous,
                $"{targetRooms.Count} registered rooms use scene name '{targetScene}'.",
                $"{targetScene}/{targetDoor}");
        }

        RoomDefinition targetRoom = targetRooms[0];

        if (!entriesByRoom.TryGetValue(targetRoom, out List<DoorIdAnalysisEntry> targetEntries))
        {
            return Unmergeable(
                entry,
                LegacyMigrationClassification.UnresolvedDoor,
                $"Room '{targetRoom.RoomId}' has no discoverable doors.",
                $"{targetScene}/{targetDoor}");
        }

        List<DoorIdAnalysisEntry> matches = new List<DoorIdAnalysisEntry>();

        foreach (DoorIdAnalysisEntry candidate in targetEntries)
        {
            if (string.Equals(candidate.LegacyDoorId, targetDoor, StringComparison.Ordinal))
                matches.Add(candidate);
        }

        if (matches.Count == 0)
        {
            return Unmergeable(
                entry,
                LegacyMigrationClassification.UnresolvedDoor,
                $"No door in room '{targetRoom.RoomId}' has legacy doorID '{targetDoor}'.",
                $"{targetScene}/{targetDoor}");
        }

        if (matches.Count > 1)
        {
            return Unmergeable(
                entry,
                LegacyMigrationClassification.Ambiguous,
                $"{matches.Count} doors in room '{targetRoom.RoomId}' use legacy doorID '{targetDoor}'.",
                $"{targetScene}/{targetDoor}");
        }

        DoorIdAnalysisEntry target = matches[0];

        bool sameRoom =
            ReferenceEquals(entry, target) ||
            string.Equals(entry.RoomId, target.RoomId, StringComparison.Ordinal) ||
            (!string.IsNullOrEmpty(entry.Room.SceneGuid) &&
             string.Equals(entry.Room.SceneGuid, target.Room.SceneGuid, StringComparison.Ordinal));

        if (sameRoom)
        {
            return Unmergeable(
                entry,
                LegacyMigrationClassification.SelfReference,
                "The legacy relationship stays inside the same room or doorway; self-connections are not supported in the MVP.",
                $"{targetScene}/{targetDoor}");
        }

        LegacyEndpointSnapshot targetSnapshot = new LegacyEndpointSnapshot(
            target.Room, target.RoomId, target.StableDoorId,
            target.LegacyDoorId, target.TargetSceneName, target.TargetDoorId);

        LegacyMigrationClassification classification;
        string reason;
        RoomConnection pairConnection = null;

        if (entry.Status == DoorIdStatus.Duplicate || target.Status == DoorIdStatus.Duplicate)
        {
            classification = LegacyMigrationClassification.DuplicateDoorId;
            reason = "At least one endpoint has a duplicated stable doorId; endpoint identity is ambiguous.";
        }
        else if (entry.Status == DoorIdStatus.Missing || target.Status == DoorIdStatus.Missing)
        {
            classification = LegacyMigrationClassification.MissingStableDoorId;
            reason = "At least one endpoint has no stable doorId; assign missing door IDs first.";
        }
        else
        {
            string pairKey = PairKey(entry.RoomId, entry.StableDoorId, target.RoomId, target.StableDoorId);

            if (connections.TryGetPair(pairKey, out RoomConnection existing))
            {
                pairConnection = existing;
                classification = LegacyMigrationClassification.AlreadyMigrated;
                reason = $"A RoomConnection already links these endpoints ('{existing.name}').";
            }
            else if (connections.EndpointUsed(entry.RoomId, entry.StableDoorId, out RoomConnection sourceConnection))
            {
                classification = LegacyMigrationClassification.ConflictWithExistingConnection;
                reason = $"Source endpoint is already part of connection '{sourceConnection.name}'.";
            }
            else if (connections.EndpointUsed(target.RoomId, target.StableDoorId, out RoomConnection targetConnection))
            {
                classification = LegacyMigrationClassification.ConflictWithExistingConnection;
                reason = $"Target endpoint is already part of connection '{targetConnection.name}'.";
            }
            else
            {
                bool reciprocal =
                    string.Equals(target.TargetSceneName, entry.Room.SceneName, StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(entry.LegacyDoorId) &&
                    string.Equals(target.TargetDoorId, entry.LegacyDoorId, StringComparison.Ordinal);

                classification = reciprocal
                    ? LegacyMigrationClassification.SafeBidirectional
                    : LegacyMigrationClassification.SafeOneWay;

                reason = reciprocal
                    ? "Both legacy doorways point at each other; verified reciprocal relationship."
                    : "Only this doorway points at the target; the target does not point back, so it is a OneWay relationship.";
            }
        }

        if (classification == LegacyMigrationClassification.DuplicateDoorId ||
            classification == LegacyMigrationClassification.MissingStableDoorId)
        {
            return Unmergeable(
                entry,
                classification,
                reason,
                $"{targetRoom.RoomId}/{target.StableDoorId}");
        }

        bool canonicalSourceFirst = CompareEndpoints(
            entry.RoomId, entry.StableDoorId, target.RoomId, target.StableDoorId) <= 0;

        LegacyMigrationProposal proposal = new LegacyMigrationProposal
        {
            Key = PairKey(entry.RoomId, entry.StableDoorId, target.RoomId, target.StableDoorId),
            A = canonicalSourceFirst ? source : targetSnapshot,
            B = canonicalSourceFirst ? targetSnapshot : source,
            Direction = classification == LegacyMigrationClassification.SafeBidirectional
                ? RoomConnectionDirection.Bidirectional
                : pairConnection != null
                    ? pairConnection.Direction
                    : RoomConnectionDirection.OneWay,
            Classification = classification,
            Reason = reason,
            TargetDescription = $"{targetRoom.RoomId}/{target.StableDoorId}"
        };

        return proposal;
    }

    private static LegacyMigrationProposal Unmergeable(
        DoorIdAnalysisEntry entry,
        LegacyMigrationClassification classification,
        string reason,
        string targetDescription)
    {
        LegacyEndpointSnapshot source = new LegacyEndpointSnapshot(
            entry.Room, entry.RoomId, entry.StableDoorId,
            entry.LegacyDoorId, entry.TargetSceneName, entry.TargetDoorId);

        return new LegacyMigrationProposal
        {
            Key = "u:" + EndpointKey(entry.RoomId, entry.StableDoorId ?? entry.LegacyDoorId) +
                  PairSeparator + targetDescription + PairSeparator + entry.GameObjectName,
            A = source,
            B = null,
            Direction = RoomConnectionDirection.OneWay,
            Classification = classification,
            Reason = reason,
            TargetDescription = targetDescription
        };
    }

    private static void MergePairProposal(LegacyMigrationProposal existing, LegacyMigrationProposal candidate)
    {
        int existingRank = Rank(existing.Classification);
        int candidateRank = Rank(candidate.Classification);

        if (candidateRank > existingRank)
        {
            existing.A = candidate.A;
            existing.B = candidate.B;
            existing.Classification = candidate.Classification;
            existing.Reason = candidate.Reason;
            existing.Direction = candidate.Direction;
            existing.TargetDescription = candidate.TargetDescription;
            return;
        }

        if (candidateRank < existingRank)
            return;

        if (!existing.IsSafeToApply)
            return;

        if (candidate.Direction == RoomConnectionDirection.Bidirectional ||
            existing.Direction == RoomConnectionDirection.Bidirectional)
        {
            existing.Direction = RoomConnectionDirection.Bidirectional;
            existing.Classification = LegacyMigrationClassification.SafeBidirectional;
            existing.Reason = "Both legacy doorways point at each other; verified reciprocal relationship.";
        }
    }

    private static int Rank(LegacyMigrationClassification classification)
    {
        switch (classification)
        {
            case LegacyMigrationClassification.AlreadyMigrated:
                return 100;
            case LegacyMigrationClassification.ConflictWithExistingConnection:
                return 90;
            case LegacyMigrationClassification.DuplicateDoorId:
                return 80;
            case LegacyMigrationClassification.MissingStableDoorId:
                return 70;
            case LegacyMigrationClassification.SelfReference:
                return 60;
            default:
                return 0;
        }
    }

    private static string EndpointKey(string roomId, string doorId)
    {
        return (roomId ?? string.Empty) + EndpointSeparator + (doorId ?? string.Empty);
    }

    internal static string PairKey(string roomA, string doorA, string roomB, string doorB)
    {
        string first = EndpointKey(roomA, doorA);
        string second = EndpointKey(roomB, doorB);

        return CompareEndpoints(roomA, doorA, roomB, doorB) <= 0
            ? first + PairSeparator + second
            : second + PairSeparator + first;
    }

    private static int CompareEndpoints(string roomA, string doorA, string roomB, string doorB)
    {
        int roomComparison = string.CompareOrdinal(roomA ?? string.Empty, roomB ?? string.Empty);
        if (roomComparison != 0)
            return roomComparison;

        return string.CompareOrdinal(doorA ?? string.Empty, doorB ?? string.Empty);
    }

    private sealed class ConnectionIndex
    {
        private readonly Dictionary<string, RoomConnection> _byPair =
            new Dictionary<string, RoomConnection>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<RoomConnection>> _byEndpoint =
            new Dictionary<string, List<RoomConnection>>(StringComparer.Ordinal);

        public static ConnectionIndex Build(List<string> messages)
        {
            ConnectionIndex index = new ConnectionIndex();
            string[] guids = AssetDatabase.FindAssets("t:RoomConnection");
            List<string> paths = new List<string>(guids.Length);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);

            foreach (string path in paths)
            {
                RoomConnection connection = AssetDatabase.LoadAssetAtPath<RoomConnection>(path);
                if (connection == null)
                {
                    messages?.Add($"Could not load RoomConnection asset at '{path}'.");
                    continue;
                }

                index.Add(connection);
            }

            return index;
        }

        private void Add(RoomConnection connection)
        {
            string pairKey = PairKey(
                connection.EndpointARoomId,
                connection.EndpointADoorId,
                connection.EndpointBRoomId,
                connection.EndpointBDoorId);

            if (!_byPair.ContainsKey(pairKey))
                _byPair.Add(pairKey, connection);

            AddEndpoint(connection, connection.EndpointARoomId, connection.EndpointADoorId);
            AddEndpoint(connection, connection.EndpointBRoomId, connection.EndpointBDoorId);
        }

        private void AddEndpoint(RoomConnection connection, string roomId, string doorId)
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(doorId))
                return;

            string key = EndpointKey(roomId, doorId);

            if (!_byEndpoint.TryGetValue(key, out List<RoomConnection> list))
            {
                list = new List<RoomConnection>();
                _byEndpoint.Add(key, list);
            }

            if (!list.Contains(connection))
                list.Add(connection);
        }

        public bool TryGetPair(string pairKey, out RoomConnection connection)
        {
            return _byPair.TryGetValue(pairKey, out connection);
        }

        public bool EndpointUsed(string roomId, string doorId, out RoomConnection connection)
        {
            connection = null;

            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(doorId))
                return false;

            if (!_byEndpoint.TryGetValue(EndpointKey(roomId, doorId), out List<RoomConnection> list) ||
                list.Count == 0)
            {
                return false;
            }

            connection = list[0];
            return true;
        }
    }
}
