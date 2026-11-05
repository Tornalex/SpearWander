using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum RoomGraphStatus
{
    Valid = 0,
    Warning = 1,
    Invalid = 2
}

public sealed class RoomGraphIssue
{
    public RoomGraphIssue(
        string ruleId,
        RoomValidationSeverity severity,
        string message,
        UnityEngine.Object context)
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
    public bool IsError => Severity == RoomValidationSeverity.Error;
}

public sealed class RoomGraphDoor
{
    public RoomGraphDoor(
        RoomDefinition room,
        string scenePath,
        string stableDoorId,
        string legacyDoorId,
        string gameObjectName,
        bool isActive,
        RoomConnection connection)
    {
        Room = room;
        ScenePath = scenePath;
        StableDoorId = stableDoorId;
        LegacyDoorId = legacyDoorId;
        GameObjectName = gameObjectName;
        IsActive = isActive;
        Connection = connection;
    }

    public RoomDefinition Room { get; }
    public string ScenePath { get; }
    public string StableDoorId { get; }
    public string LegacyDoorId { get; }
    public string GameObjectName { get; }
    public bool IsActive { get; }

    /// <summary>
    /// Scene-derived RoomTransition.connection backlink. This is not the
    /// topology reference list; use ReferencingEdges for topology usage.
    /// </summary>
    public RoomConnection Connection { get; }

    /// <summary>
    /// Legacy last-writer-wins path assigned while resolving connection endpoints.
    /// It is not a topology index and must not be used as one.
    /// </summary>
    public string ConnectionAssetPath { get; internal set; }

    /// <summary>
    /// RoomGraphEdges whose resolved RoomConnection endpoints reference this door.
    /// Populated from connection endpoint data, never from the scene backlink.
    /// </summary>
    public List<RoomGraphEdge> ReferencingEdges { get; } = new List<RoomGraphEdge>();

    public int ReferenceCount => ReferencingEdges.Count;
    public bool ReferencedByTopology => ReferenceCount > 0;
    public bool HasMultipleReferences => ReferenceCount > 1;

    public bool IsMissingId => string.IsNullOrWhiteSpace(StableDoorId);
    public bool IsDuplicateId { get; internal set; }
    public bool HasIdentityIssue => IsMissingId || IsDuplicateId;

    /// <summary>
    /// Backlink-only compatibility flag. True when a scene backlink exists.
    /// This does not mean the door is referenced by a RoomConnection asset.
    /// </summary>
    public bool IsConnected => Connection != null;

    public bool HasBacklink => Connection != null;

    /// <summary>
    /// True when the scene backlink is one of the connections that actually
    /// references this door through its resolved endpoint data.
    /// </summary>
    public bool BacklinkMatchesTopology
    {
        get
        {
            if (Connection == null)
                return false;

            foreach (RoomGraphEdge edge in ReferencingEdges)
            {
                if (edge.Connection != null && ReferenceEquals(edge.Connection, Connection))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// True when the door is referenced by topology but its backlink points to
    /// a different connection.
    /// </summary>
    public bool BacklinkMismatched =>
        HasBacklink && ReferencedByTopology && !BacklinkMatchesTopology;

    /// <summary>
    /// True when a backlink exists but the connection it points to does not
    /// list this door as an endpoint. This can overlap with BacklinkMismatched.
    /// </summary>
    public bool BacklinkOrphaned => HasBacklink && !BacklinkMatchesTopology;

    /// <summary>
    /// True only when the door has no topology reference and no backlink.
    /// </summary>
    public bool IsFullyUnconnected => !ReferencedByTopology && !HasBacklink;

    public int InvalidReferenceCount
    {
        get
        {
            int count = 0;

            foreach (RoomGraphEdge edge in ReferencingEdges)
            {
                if (!edge.IsValid)
                    count++;
            }

            return count;
        }
    }

    public bool InvolvedInInvalidConnection => InvalidReferenceCount > 0;

    /// <summary>
    /// Legacy exclusive label retained for compatibility. It does not represent
    /// the orthogonal topology, backlink and identity diagnostics above.
    /// </summary>
    public string StatusLabel
    {
        get
        {
            if (IsMissingId)
                return "Missing doorId";

            if (IsDuplicateId)
                return "Duplicate doorId";

            if (Connection == null)
                return "Unconnected";

            return "Connected";
        }
    }
}

public sealed class RoomGraphNode
{
    public RoomGraphNode(RoomDefinition definition, string scenePath)
    {
        Definition = definition;
        ScenePath = scenePath;
        NodeKey = BuildNodeKey(definition, scenePath);
    }

    public RoomDefinition Definition { get; }
    public string ScenePath { get; }

    /// <summary>
    /// Stable identity of this node's RoomDefinition asset. Two definitions with
    /// the same Room ID, display name or scene path still have distinct keys.
    /// </summary>
    public string NodeKey { get; }

    public List<RoomGraphDoor> Doors { get; } = new List<RoomGraphDoor>();
    public List<RoomGraphIssue> Issues { get; } = new List<RoomGraphIssue>();

    public string RoomId => Definition != null ? Definition.RoomId : string.Empty;
    public string DisplayName =>
        Definition == null
            ? "<missing room>"
            : string.IsNullOrWhiteSpace(Definition.DisplayName)
                ? Definition.name
                : Definition.DisplayName;
    public string SceneName => Definition != null ? Definition.SceneName : string.Empty;
    public bool SceneAvailable => !string.IsNullOrEmpty(ScenePath);

    public int DoorCount => Doors.Count;

    /// <summary>
    /// Legacy backlink-only count. A door is counted when its scene
    /// RoomTransition.connection is non-null, regardless of whether any
    /// RoomConnection asset references it. Use ReferencedDoorCount and the
    /// backlink diagnostic counts for topology-aware reporting.
    /// </summary>
    public int ConnectedDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.IsConnected)
                    count++;
            }

            return count;
        }
    }

    /// <summary>
    /// Legacy backlink-only count. A door is counted when its scene
    /// RoomTransition.connection is null, even if a RoomConnection asset still
    /// references it. Use ReferencedDoorCount and the backlink diagnostic
    /// counts for topology-aware reporting.
    /// </summary>
    public int UnconnectedDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (!door.IsConnected)
                    count++;
            }

            return count;
        }
    }

    public int InvalidDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.HasIdentityIssue)
                    count++;
            }

            return count;
        }
    }

    // The following counts are orthogonal diagnostics. A door may contribute to
    // more than one count (for example: referenced, backlink mismatched, and
    // identity duplicated). Do not treat them as a partition of DoorCount.

    public int ReferencedDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.ReferencedByTopology)
                    count++;
            }

            return count;
        }
    }

    public int BacklinkConsistentDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.BacklinkMatchesTopology)
                    count++;
            }

            return count;
        }
    }

    public int BacklinkMissingDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.ReferencedByTopology && !door.HasBacklink)
                    count++;
            }

            return count;
        }
    }

    public int BacklinkMismatchedDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.BacklinkMismatched)
                    count++;
            }

            return count;
        }
    }

    public int BacklinkOrphanedDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.BacklinkOrphaned)
                    count++;
            }

            return count;
        }
    }

    public int UnreferencedDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (!door.ReferencedByTopology)
                    count++;
            }

            return count;
        }
    }

    public int FullyUnconnectedDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.IsFullyUnconnected)
                    count++;
            }

            return count;
        }
    }

    public int InvalidIdentityDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.HasIdentityIssue)
                    count++;
            }

            return count;
        }
    }

    public int InvalidConnectionDoorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphDoor door in Doors)
            {
                if (door.InvolvedInInvalidConnection)
                    count++;
            }

            return count;
        }
    }

    public int ErrorCount => CountIssues(true);
    public int WarningCount => CountIssues(false);
    public bool HasErrors => ErrorCount > 0;

    public RoomGraphStatus Status =>
        HasErrors
            ? RoomGraphStatus.Invalid
            : WarningCount > 0
                ? RoomGraphStatus.Warning
                : RoomGraphStatus.Valid;

    public string StatusLabel =>
        Status == RoomGraphStatus.Invalid
            ? "Invalid"
            : Status == RoomGraphStatus.Warning
                ? "Warnings"
                : "Valid";

    private static string BuildNodeKey(RoomDefinition definition, string scenePath)
    {
        if (definition == null)
            return string.Empty;

        string assetPath = AssetDatabase.GetAssetPath(definition);

        if (!string.IsNullOrEmpty(assetPath))
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            if (!string.IsNullOrEmpty(guid))
                return guid;

            return assetPath;
        }

        return $"{definition.name}:{scenePath}";
    }

    internal void AddIssue(RoomGraphIssue issue)
    {
        if (issue == null || ContainsIssue(Issues, issue.RuleId, issue.Message))
            return;

        Issues.Add(issue);
    }

    private int CountIssues(bool errors)
    {
        int count = 0;

        foreach (RoomGraphIssue issue in Issues)
        {
            if (issue.IsError == errors)
                count++;
        }

        return count;
    }

    internal static bool ContainsIssue(List<RoomGraphIssue> issues, string ruleId, string message)
    {
        foreach (RoomGraphIssue issue in issues)
        {
            if (string.Equals(issue.RuleId, ruleId, StringComparison.Ordinal) &&
                string.Equals(issue.Message, message, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class RoomGraphEdge
{
    public RoomGraphEdge(RoomConnection connection, string assetPath)
    {
        Connection = connection;
        AssetPath = assetPath;
    }

    public RoomConnection Connection { get; }
    public string AssetPath { get; }
    public string AssetName => Connection != null ? Connection.name : "<missing connection>";
    public List<RoomGraphIssue> Issues { get; } = new List<RoomGraphIssue>();

    public RoomGraphNode SourceNode { get; internal set; }
    public RoomGraphNode TargetNode { get; internal set; }
    public RoomGraphDoor EndpointADoor { get; internal set; }
    public RoomGraphDoor EndpointBDoor { get; internal set; }
    public bool EndpointsResolved { get; internal set; }

    /// <summary>
    /// True when both endpoints resolved to the same RoomGraphNode.
    /// Self-loop geometry is intentionally not produced in Phase 4.2A.
    /// </summary>
    public bool IsSelfConnection =>
        SourceNode != null && ReferenceEquals(SourceNode, TargetNode);

    public string EndpointARoomId => Connection != null ? Connection.EndpointARoomId : string.Empty;
    public string EndpointADoorId => Connection != null ? Connection.EndpointADoorId : string.Empty;
    public string EndpointBRoomId => Connection != null ? Connection.EndpointBRoomId : string.Empty;
    public string EndpointBDoorId => Connection != null ? Connection.EndpointBDoorId : string.Empty;

    public RoomConnectionDirection Direction =>
        Connection != null ? Connection.Direction : RoomConnectionDirection.OneWay;

    public bool HasDirectionError =>
        Direction != RoomConnectionDirection.Bidirectional &&
        Direction != RoomConnectionDirection.OneWay;

    public int ErrorCount => CountIssues(true);
    public int WarningCount => CountIssues(false);
    public bool HasErrors => ErrorCount > 0;
    public bool IsValid => EndpointsResolved && !HasErrors;

    public string StatusLabel => IsValid ? "Valid" : "Invalid";

    public string DirectionLabel
    {
        get
        {
            if (HasDirectionError)
                return $"Invalid ({(int)Direction})";

            return Direction == RoomConnectionDirection.Bidirectional
                ? "Bidirectional (A ↔ B)"
                : "OneWay (A → B)";
        }
    }

    public string DirectionArrow =>
        HasDirectionError
            ? "?"
            : Direction == RoomConnectionDirection.Bidirectional
                ? "↔"
                : "→";

    public string EndpointALabel =>
        $"{RoomLabel(SourceNode, EndpointARoomId)} / {DoorLabel(EndpointADoor, EndpointADoorId)}";

    public string EndpointBLabel =>
        $"{RoomLabel(TargetNode, EndpointBRoomId)} / {DoorLabel(EndpointBDoor, EndpointBDoorId)}";

    internal void AddIssue(RoomGraphIssue issue)
    {
        if (issue == null || RoomGraphNode.ContainsIssue(Issues, issue.RuleId, issue.Message))
            return;

        Issues.Add(issue);
    }

    private static string RoomLabel(RoomGraphNode node, string roomId)
    {
        if (node != null)
            return node.DisplayName;

        return string.IsNullOrWhiteSpace(roomId) ? "<missing room>" : roomId;
    }

    private static string DoorLabel(RoomGraphDoor door, string doorId)
    {
        if (door != null)
            return string.IsNullOrWhiteSpace(door.StableDoorId) ? "<missing doorId>" : door.StableDoorId;

        return string.IsNullOrWhiteSpace(doorId) ? "<missing door>" : doorId;
    }

    private int CountIssues(bool errors)
    {
        int count = 0;

        foreach (RoomGraphIssue issue in Issues)
        {
            if (issue.IsError == errors)
                count++;
        }

        return count;
    }
}

public sealed class RoomConnectivityGraphModel
{
    public List<RoomGraphNode> Nodes { get; } = new List<RoomGraphNode>();
    public List<RoomGraphEdge> Edges { get; } = new List<RoomGraphEdge>();
    public List<RoomGraphIssue> GlobalIssues { get; } = new List<RoomGraphIssue>();
    public RoomValidationReport Validation { get; internal set; }
    public bool RegistryAvailable { get; internal set; }
    public DateTime BuiltAtUtc { get; } = DateTime.UtcNow;

    public int ErrorCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphIssue issue in GlobalIssues)
            {
                if (issue.IsError)
                    count++;
            }

            foreach (RoomGraphNode node in Nodes)
                count += node.ErrorCount;

            foreach (RoomGraphEdge edge in Edges)
                count += edge.ErrorCount;

            return count;
        }
    }

    public int WarningCount
    {
        get
        {
            int count = 0;
            foreach (RoomGraphIssue issue in GlobalIssues)
            {
                if (!issue.IsError)
                    count++;
            }

            foreach (RoomGraphNode node in Nodes)
                count += node.WarningCount;

            foreach (RoomGraphEdge edge in Edges)
                count += edge.WarningCount;

            return count;
        }
    }

    public bool HasErrors => ErrorCount > 0;

    /// <summary>
    /// Legacy Room ID lookup. Returns the first matching node and is ambiguous
    /// when duplicate Room IDs exist. Do not use for unique node identity or
    /// selection restoration; use FindNodeByKey instead.
    /// </summary>
    public RoomGraphNode FindNode(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return null;

        foreach (RoomGraphNode node in Nodes)
        {
            if (string.Equals(node.RoomId, roomId, StringComparison.Ordinal))
                return node;
        }

        return null;
    }

    public RoomGraphNode FindNodeByKey(string nodeKey)
    {
        if (string.IsNullOrEmpty(nodeKey))
            return null;

        foreach (RoomGraphNode node in Nodes)
        {
            if (string.Equals(node.NodeKey, nodeKey, StringComparison.Ordinal))
                return node;
        }

        return null;
    }

    public RoomGraphNode FindNode(RoomDefinition definition)
    {
        foreach (RoomGraphNode node in Nodes)
        {
            if (ReferenceEquals(node.Definition, definition))
                return node;
        }

        return null;
    }

    public RoomGraphEdge FindEdge(RoomConnection connection)
    {
        foreach (RoomGraphEdge edge in Edges)
        {
            if (ReferenceEquals(edge.Connection, connection))
                return edge;
        }

        return null;
    }

    public RoomGraphEdge FindEdgeByPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        foreach (RoomGraphEdge edge in Edges)
        {
            if (string.Equals(edge.AssetPath, assetPath, StringComparison.Ordinal))
                return edge;
        }

        return null;
    }

    public List<RoomGraphEdge> EdgesBetween(string roomAId, string roomBId)
    {
        List<RoomGraphEdge> edges = new List<RoomGraphEdge>();

        foreach (RoomGraphEdge edge in Edges)
        {
            bool direct =
                string.Equals(edge.EndpointARoomId, roomAId, StringComparison.Ordinal) &&
                string.Equals(edge.EndpointBRoomId, roomBId, StringComparison.Ordinal);

            bool reverse =
                string.Equals(edge.EndpointARoomId, roomBId, StringComparison.Ordinal) &&
                string.Equals(edge.EndpointBRoomId, roomAId, StringComparison.Ordinal);

            if (direct || reverse)
                edges.Add(edge);
        }

        return edges;
    }

    public void AddGlobalIssue(RoomGraphIssue issue)
    {
        if (issue == null || RoomGraphNode.ContainsIssue(GlobalIssues, issue.RuleId, issue.Message))
            return;

        GlobalIssues.Add(issue);
    }
}

/// <summary>
/// Read-only collection of graph data from RoomRegistry, scene discoveries and
/// RoomConnection assets. Validation is delegated to RoomConnectionValidator.
/// </summary>
public static class RoomConnectivityGraphService
{
    public static RoomConnectivityGraphModel Build()
    {
        RoomConnectivityGraphModel model = new RoomConnectivityGraphModel();

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry == null)
        {
            model.RegistryAvailable = false;
            model.AddGlobalIssue(new RoomGraphIssue(
                RoomConnectionValidator.MissingRegistry,
                RoomValidationSeverity.Error,
                $"RoomRegistry is missing at '{RoomRegistrationService.RegistryPath}'.",
                null));
            return model;
        }

        model.RegistryAvailable = true;

        Dictionary<RoomDefinition, DoorDiscoveryResult> doorSnapshot =
            new Dictionary<RoomDefinition, DoorDiscoveryResult>();

        foreach (RoomDefinition definition in registry.Rooms)
        {
            if (definition == null)
            {
                model.AddGlobalIssue(new RoomGraphIssue(
                    RoomConnectionValidator.InvalidRoomDefinition,
                    RoomValidationSeverity.Error,
                    "RoomRegistry contains a missing RoomDefinition reference.",
                    null));
                continue;
            }

            string scenePath = AssetDatabase.GUIDToAssetPath(definition.SceneGuid);
            RoomGraphNode node = new RoomGraphNode(definition, scenePath);
            model.Nodes.Add(node);

            if (string.IsNullOrWhiteSpace(definition.RoomId))
            {
                node.AddIssue(new RoomGraphIssue(
                    RoomConnectionValidator.MissingRoomId,
                    RoomValidationSeverity.Error,
                    $"RoomDefinition '{node.DisplayName}' has no room ID.",
                    definition));
            }

            if (string.IsNullOrEmpty(scenePath))
            {
                node.AddIssue(new RoomGraphIssue(
                    RoomConnectionValidator.InvalidScenePath,
                    RoomValidationSeverity.Error,
                    $"RoomDefinition '{node.DisplayName}' does not resolve to a scene path.",
                    definition));
                continue;
            }

            List<DiscoveredDoor> doors = RoomDiscoveryService.DiscoverDoors(definition, out string message);
            doorSnapshot[definition] = new DoorDiscoveryResult(doors, message);

            if (doors == null)
            {
                node.AddIssue(new RoomGraphIssue(
                    RoomConnectionValidator.InvalidScenePath,
                    RoomValidationSeverity.Error,
                    message,
                    definition));
                continue;
            }

            foreach (DiscoveredDoor door in doors)
            {
                node.Doors.Add(new RoomGraphDoor(
                    definition,
                    scenePath,
                    door.StableDoorId,
                    door.DoorID,
                    door.GameObjectName,
                    door.IsActive,
                    door.Connection));
            }

            MarkDoorIdIssues(node);
        }

        MarkDuplicateRoomIds(model);
        List<RoomConnection> connections = LoadConnections(model);

        foreach (RoomConnection connection in connections)
        {
            string path = AssetDatabase.GetAssetPath(connection);
            model.Edges.Add(BuildEdge(model, connection, path));
        }

        BuildTopologyUsageIndex(model);

        RoomValidationReport validation = RoomConnectionValidator.Validate(doorSnapshot);
        model.Validation = validation;
        AttachValidation(model, validation);

        return model;
    }

    /// <summary>
    /// Builds the topology usage index from resolved RoomConnection endpoints.
    /// The scene backlink is deliberately not consulted here.
    /// </summary>
    private static void BuildTopologyUsageIndex(RoomConnectivityGraphModel model)
    {
        foreach (RoomGraphEdge edge in model.Edges)
        {
            AddDoorReference(edge, edge.EndpointADoor);
            AddDoorReference(edge, edge.EndpointBDoor);
        }
    }

    private static void AddDoorReference(RoomGraphEdge edge, RoomGraphDoor door)
    {
        if (edge == null || door == null)
            return;

        if (!door.ReferencingEdges.Contains(edge))
            door.ReferencingEdges.Add(edge);
    }

    private static void MarkDoorIdIssues(RoomGraphNode node)
    {
        Dictionary<string, List<RoomGraphDoor>> byId =
            new Dictionary<string, List<RoomGraphDoor>>(StringComparer.Ordinal);

        foreach (RoomGraphDoor door in node.Doors)
        {
            if (door.IsMissingId)
            {
                node.AddIssue(new RoomGraphIssue(
                    RoomConnectionValidator.MissingDoorId,
                    RoomValidationSeverity.Error,
                    $"Door '{door.GameObjectName}' in room '{node.RoomId}' has no stable doorId.",
                    node.Definition));
                continue;
            }

            if (!byId.TryGetValue(door.StableDoorId, out List<RoomGraphDoor> list))
            {
                list = new List<RoomGraphDoor>();
                byId.Add(door.StableDoorId, list);
            }

            list.Add(door);
        }

        foreach (KeyValuePair<string, List<RoomGraphDoor>> pair in byId)
        {
            if (pair.Value.Count <= 1)
                continue;

            foreach (RoomGraphDoor door in pair.Value)
                door.IsDuplicateId = true;

            node.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.DuplicateDoorId,
                RoomValidationSeverity.Error,
                $"Door ID '{pair.Key}' appears {pair.Value.Count} times in room '{node.RoomId}'.",
                node.Definition));
        }
    }

    private static void MarkDuplicateRoomIds(RoomConnectivityGraphModel model)
    {
        Dictionary<string, List<RoomGraphNode>> byRoomId =
            new Dictionary<string, List<RoomGraphNode>>(StringComparer.Ordinal);

        foreach (RoomGraphNode node in model.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.RoomId))
                continue;

            if (!byRoomId.TryGetValue(node.RoomId, out List<RoomGraphNode> list))
            {
                list = new List<RoomGraphNode>();
                byRoomId.Add(node.RoomId, list);
            }

            list.Add(node);
        }

        foreach (KeyValuePair<string, List<RoomGraphNode>> pair in byRoomId)
        {
            if (pair.Value.Count <= 1)
                continue;

            foreach (RoomGraphNode node in pair.Value)
            {
                node.AddIssue(new RoomGraphIssue(
                    RoomConnectionValidator.DuplicateRoomId,
                    RoomValidationSeverity.Error,
                    $"Room ID '{pair.Key}' is used by {pair.Value.Count} RoomDefinitions.",
                    node.Definition));
            }
        }
    }

    private static List<RoomConnection> LoadConnections(RoomConnectivityGraphModel model)
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
                model.AddGlobalIssue(new RoomGraphIssue(
                    RoomConnectionValidator.InvalidConnectionAsset,
                    RoomValidationSeverity.Error,
                    $"Could not load RoomConnection asset at '{path}'.",
                    null));
                continue;
            }

            connections.Add(connection);
        }

        return connections;
    }

    private static RoomGraphEdge BuildEdge(
        RoomConnectivityGraphModel model,
        RoomConnection connection,
        string assetPath)
    {
        RoomGraphEdge edge = new RoomGraphEdge(connection, assetPath);

        ResolveNode(model, edge, connection.EndpointARoomId, true);
        ResolveNode(model, edge, connection.EndpointBRoomId, false);

        edge.EndpointADoor = ResolveDoor(model, edge, connection.EndpointARoomId, connection.EndpointADoorId, true);
        edge.EndpointBDoor = ResolveDoor(model, edge, connection.EndpointBRoomId, connection.EndpointBDoorId, false);

        if (edge.EndpointADoor != null)
            edge.EndpointADoor.ConnectionAssetPath = assetPath;

        if (edge.EndpointBDoor != null)
            edge.EndpointBDoor.ConnectionAssetPath = assetPath;

        if (edge.HasDirectionError)
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.InvalidConnectionDirection,
                RoomValidationSeverity.Error,
                $"Connection '{edge.AssetName}' has an invalid direction value ({(int)connection.Direction}).",
                connection));
        }

        ValidateBacklink(edge, connection, edge.EndpointADoor, connection.EndpointADoorId, connection.EndpointARoomId);
        ValidateBacklink(edge, connection, edge.EndpointBDoor, connection.EndpointBDoorId, connection.EndpointBRoomId);

        edge.EndpointsResolved =
            edge.SourceNode != null &&
            edge.TargetNode != null &&
            edge.EndpointADoor != null &&
            edge.EndpointBDoor != null;

        return edge;
    }

    private static void ResolveNode(
        RoomConnectivityGraphModel model,
        RoomGraphEdge edge,
        string roomId,
        bool endpointA)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.MissingEndpointRoomId,
                RoomValidationSeverity.Error,
                $"Connection '{edge.AssetName}' has a missing endpoint room ID.",
                edge.Connection));
            return;
        }

        List<RoomGraphNode> matches = new List<RoomGraphNode>();

        foreach (RoomGraphNode node in model.Nodes)
        {
            if (string.Equals(node.RoomId, roomId, StringComparison.Ordinal))
                matches.Add(node);
        }

        if (matches.Count == 0)
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.UnregisteredEndpointRoom,
                RoomValidationSeverity.Error,
                $"Connection '{edge.AssetName}' references unknown room '{roomId}'.",
                edge.Connection));
            return;
        }

        if (matches.Count > 1)
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.DuplicateRoomId,
                RoomValidationSeverity.Error,
                $"Connection '{edge.AssetName}' references room ID '{roomId}', which is used by multiple RoomDefinitions.",
                edge.Connection));
            return;
        }

        if (endpointA)
            edge.SourceNode = matches[0];
        else
            edge.TargetNode = matches[0];
    }

    private static RoomGraphDoor ResolveDoor(
        RoomConnectivityGraphModel model,
        RoomGraphEdge edge,
        string roomId,
        string doorId,
        bool endpointA)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(doorId))
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.MissingEndpointDoorId,
                RoomValidationSeverity.Error,
                $"Connection '{edge.AssetName}' has a missing endpoint door ID.",
                edge.Connection));
            return null;
        }

        RoomGraphNode node = endpointA ? edge.SourceNode : edge.TargetNode;

        if (node == null)
            return null;

        List<RoomGraphDoor> matches = new List<RoomGraphDoor>();

        foreach (RoomGraphDoor door in node.Doors)
        {
            if (string.Equals(door.StableDoorId, doorId, StringComparison.Ordinal))
                matches.Add(door);
        }

        if (matches.Count == 0)
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.MissingEndpointDoor,
                RoomValidationSeverity.Error,
                $"Connection '{edge.AssetName}' references missing door '{doorId}' in room '{roomId}'.",
                edge.Connection));
            return null;
        }

        if (matches.Count > 1)
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.DuplicateDoorId,
                RoomValidationSeverity.Error,
                $"Connection '{edge.AssetName}' references door ID '{doorId}', which is duplicated in room '{roomId}'.",
                edge.Connection));
            return null;
        }

        return matches[0];
    }

    private static void ValidateBacklink(
        RoomGraphEdge edge,
        RoomConnection connection,
        RoomGraphDoor door,
        string doorId,
        string roomId)
    {
        if (door == null)
            return;

        if (door.Connection == null)
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.MissingBacklink,
                RoomValidationSeverity.Error,
                $"Door '{doorId}' in room '{roomId}' is an endpoint of connection '{connection.name}' but has no backlink.",
                connection));
        }
        else if (!ReferenceEquals(door.Connection, connection))
        {
            edge.AddIssue(new RoomGraphIssue(
                RoomConnectionValidator.InconsistentBacklink,
                RoomValidationSeverity.Error,
                $"Door '{doorId}' in room '{roomId}' points to connection '{door.Connection.name}' instead of '{connection.name}'.",
                connection));
        }
    }

    private static void AttachValidation(RoomConnectivityGraphModel model, RoomValidationReport validation)
    {
        if (validation == null)
            return;

        foreach (RoomValidationEntry entry in validation.Entries)
        {
            RoomGraphIssue issue = new RoomGraphIssue(
                entry.RuleId,
                entry.Severity,
                entry.Message,
                entry.Context);

            bool attached = false;

            if (entry.Context is RoomDefinition definition)
            {
                RoomGraphNode node = model.FindNode(definition);
                if (node != null)
                {
                    node.AddIssue(issue);
                    attached = true;
                }
            }
            else if (entry.Context is RoomConnection connection)
            {
                RoomGraphEdge edge = model.FindEdge(connection);
                if (edge != null)
                {
                    edge.AddIssue(issue);
                    attached = true;
                }
            }

            if (!attached)
                model.AddGlobalIssue(issue);
        }
    }
}
