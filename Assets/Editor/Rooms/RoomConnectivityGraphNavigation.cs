using System;
using System.Collections.Generic;

public enum RoomGraphSearchResultKind
{
    Room = 0,
    Door = 1,
    Connection = 2
}

/// <summary>
/// Read-only search result over the already-built graph model. Every result
/// references the existing model objects; no graph rebuild or scene discovery
/// happens while searching.
/// </summary>
public sealed class RoomGraphSearchResult
{
    private RoomGraphSearchResult(
        RoomGraphSearchResultKind kind,
        RoomGraphNode room,
        RoomGraphDoor door,
        RoomGraphEdge connection,
        string title,
        string subtitle)
    {
        Kind = kind;
        Room = room;
        Door = door;
        Connection = connection;
        Title = title;
        Subtitle = subtitle;
    }

    public RoomGraphSearchResultKind Kind { get; }
    public RoomGraphNode Room { get; }
    public RoomGraphDoor Door { get; }
    public RoomGraphEdge Connection { get; }
    public string Title { get; }
    public string Subtitle { get; }

    public string KindLabel
    {
        get
        {
            switch (Kind)
            {
                case RoomGraphSearchResultKind.Door:
                    return "Door";
                case RoomGraphSearchResultKind.Connection:
                    return "Connection";
                default:
                    return "Room";
            }
        }
    }

    internal static RoomGraphSearchResult ForRoom(RoomGraphNode node)
    {
        string subtitle = JoinNonEmpty(node.SceneName, node.RoomId);
        return new RoomGraphSearchResult(
            RoomGraphSearchResultKind.Room,
            node,
            null,
            null,
            node.DisplayName,
            subtitle);
    }

    internal static RoomGraphSearchResult ForDoor(RoomGraphNode node, RoomGraphDoor door)
    {
        string title = !string.IsNullOrWhiteSpace(door.StableDoorId)
            ? door.StableDoorId
            : string.IsNullOrWhiteSpace(door.GameObjectName)
                ? "<missing doorId>"
                : door.GameObjectName;

        string subtitle = JoinNonEmpty(
            node != null ? node.DisplayName : string.Empty,
            door.GameObjectName,
            string.IsNullOrWhiteSpace(door.LegacyDoorId) ? string.Empty : $"legacy {door.LegacyDoorId}");

        return new RoomGraphSearchResult(
            RoomGraphSearchResultKind.Door,
            node,
            door,
            null,
            title,
            subtitle);
    }

    internal static RoomGraphSearchResult ForConnection(RoomGraphEdge edge)
    {
        return new RoomGraphSearchResult(
            RoomGraphSearchResultKind.Connection,
            edge.SourceNode,
            null,
            edge,
            edge.AssetName,
            edge.AssetPath);
    }

    private static string JoinNonEmpty(params string[] parts)
    {
        List<string> values = new List<string>(parts.Length);

        foreach (string part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
                values.Add(part);
        }

        return string.Join(" · ", values);
    }
}

/// <summary>
/// Presentation-only room visibility filters. Filters never change the graph
/// model, its topology, diagnostics, registry, scenes or assets.
///
/// <see cref="AllRooms"/> is the neutral/default state. When one or more
/// specific filters are active they combine as a union: a room is visible when
/// it matches at least one active filter. Selecting a specific filter clears
/// <see cref="AllRooms"/>; clearing every specific filter restores it.
/// </summary>
[Flags]
public enum RoomGraphRoomFilter
{
    None = 0,
    AllRooms = 1 << 0,

    /// <summary>
    /// Rooms with at least one topology reference (a door referenced by a
    /// resolved RoomConnection endpoint) and no invalid incident connection.
    /// Scene backlinks are never used to infer connectivity.
    /// </summary>
    ConnectedRooms = 1 << 1,

    /// <summary>
    /// Rooms containing at least one door that is not referenced by any
    /// resolved RoomConnection endpoint. Scene backlinks are ignored.
    /// </summary>
    RoomsWithUnconnectedDoors = 1 << 2,

    /// <summary>
    /// Rooms whose effective status is Invalid, including errors on incident
    /// connections. Matches the status shown on the room node.
    /// </summary>
    RoomsWithErrors = 1 << 3,

    /// <summary>
    /// Rooms with at least one warning, including warnings on incident
    /// connections. A room with both errors and warnings matches both filters.
    /// </summary>
    RoomsWithWarnings = 1 << 4
}

/// <summary>
/// Deterministic, read-only search and filter helpers shared by the graph
/// window and the isolated tests. These methods only read the supplied model.
/// </summary>
public static class RoomConnectivityGraphNavigation
{
    public const RoomGraphRoomFilter SpecificRoomFilters =
        RoomGraphRoomFilter.ConnectedRooms |
        RoomGraphRoomFilter.RoomsWithUnconnectedDoors |
        RoomGraphRoomFilter.RoomsWithErrors |
        RoomGraphRoomFilter.RoomsWithWarnings;

    public static bool HasActiveSpecificFilters(RoomGraphRoomFilter filters)
    {
        return (filters & SpecificRoomFilters) != 0;
    }

    /// <summary>
    /// Drops the neutral AllRooms bit whenever a specific filter is active and
    /// restores it when no specific filter remains.
    /// </summary>
    public static RoomGraphRoomFilter Normalize(RoomGraphRoomFilter filters)
    {
        RoomGraphRoomFilter specific = filters & SpecificRoomFilters;
        return specific != RoomGraphRoomFilter.None ? specific : RoomGraphRoomFilter.AllRooms;
    }

    public static List<RoomGraphSearchResult> Search(
        RoomConnectivityGraphModel model,
        string query)
    {
        List<RoomGraphSearchResult> results = new List<RoomGraphSearchResult>();

        if (model == null || string.IsNullOrWhiteSpace(query))
            return results;

        string needle = query.Trim();

        List<RoomGraphNode> rooms = new List<RoomGraphNode>();
        List<RoomGraphSearchResult> doors = new List<RoomGraphSearchResult>();
        List<RoomGraphSearchResult> connections = new List<RoomGraphSearchResult>();

        foreach (RoomGraphNode node in model.Nodes)
        {
            if (MatchesText(node.DisplayName, needle) ||
                MatchesText(node.SceneName, needle) ||
                MatchesText(node.RoomId, needle))
            {
                rooms.Add(node);
            }

            foreach (RoomGraphDoor door in node.Doors)
            {
                if (MatchesText(door.StableDoorId, needle) ||
                    MatchesText(door.GameObjectName, needle) ||
                    MatchesText(door.LegacyDoorId, needle))
                {
                    doors.Add(RoomGraphSearchResult.ForDoor(node, door));
                }
            }
        }

        foreach (RoomGraphEdge edge in model.Edges)
        {
            if (MatchesText(edge.AssetName, needle) ||
                MatchesText(edge.AssetPath, needle))
            {
                connections.Add(RoomGraphSearchResult.ForConnection(edge));
            }
        }

        rooms.Sort(CompareRooms);

        foreach (RoomGraphNode node in rooms)
            results.Add(RoomGraphSearchResult.ForRoom(node));

        doors.Sort(CompareSearchResults);
        results.AddRange(doors);

        connections.Sort(CompareSearchResults);
        results.AddRange(connections);

        return results;
    }

    public static bool MatchesText(string value, string query)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(query))
            return false;

        return value.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static HashSet<RoomGraphNode> ComputeVisibleRooms(
        RoomConnectivityGraphModel model,
        RoomGraphRoomFilter filters,
        IEnumerable<RoomGraphNode> revealedNodes)
    {
        HashSet<RoomGraphNode> visible = new HashSet<RoomGraphNode>();

        if (model == null)
            return visible;

        HashSet<RoomGraphNode> revealed = revealedNodes != null
            ? new HashSet<RoomGraphNode>(revealedNodes)
            : null;

        foreach (RoomGraphNode node in model.Nodes)
        {
            if (PassesRoomFilter(model, node, filters) ||
                (revealed != null && revealed.Contains(node)))
            {
                visible.Add(node);
            }
        }

        return visible;
    }

    public static bool PassesRoomFilter(
        RoomConnectivityGraphModel model,
        RoomGraphNode node,
        RoomGraphRoomFilter filters)
    {
        if (node == null)
            return false;

        RoomGraphRoomFilter specific = filters & SpecificRoomFilters;

        if (specific == RoomGraphRoomFilter.None)
            return true;

        if ((specific & RoomGraphRoomFilter.ConnectedRooms) != 0 &&
            IsRoomConnected(model, node))
        {
            return true;
        }

        if ((specific & RoomGraphRoomFilter.RoomsWithUnconnectedDoors) != 0 &&
            IsRoomWithUnconnectedDoors(node))
        {
            return true;
        }

        if ((specific & RoomGraphRoomFilter.RoomsWithErrors) != 0 &&
            GetRoomStatus(model, node) == RoomGraphStatus.Invalid)
        {
            return true;
        }

        if ((specific & RoomGraphRoomFilter.RoomsWithWarnings) != 0 &&
            HasRoomWarnings(model, node))
        {
            return true;
        }

        return false;
    }

    public static bool IsEdgeVisible(
        RoomGraphEdge edge,
        ICollection<RoomGraphNode> visibleNodes)
    {
        if (edge == null || visibleNodes == null)
            return false;

        if (edge.SourceNode == null || edge.TargetNode == null)
            return false;

        return visibleNodes.Contains(edge.SourceNode) &&
               visibleNodes.Contains(edge.TargetNode);
    }

    /// <summary>
    /// Topology-aware connectivity. A room is connected only when it has at
    /// least one door referenced by the topology, no door involved in an
    /// invalid connection, and no invalid incident connection. Backlinks alone
    /// never make a room connected, and a room whose only connection is
    /// invalid is not reported as connected.
    /// </summary>
    public static bool IsRoomConnected(
        RoomConnectivityGraphModel model,
        RoomGraphNode node)
    {
        if (model == null || node == null)
            return false;

        if (node.ReferencedDoorCount <= 0)
            return false;

        if (node.InvalidConnectionDoorCount > 0)
            return false;

        foreach (RoomGraphEdge edge in model.Edges)
        {
            if (IsEdgeIncidentToNode(edge, node) && edge.HasErrors)
                return false;
        }

        return true;
    }

    public static bool IsRoomWithUnconnectedDoors(RoomGraphNode node)
    {
        return node != null && node.UnreferencedDoorCount > 0;
    }

    public static bool HasRoomWarnings(
        RoomConnectivityGraphModel model,
        RoomGraphNode node)
    {
        if (node == null)
            return false;

        if (node.WarningCount > 0)
            return true;

        if (model == null)
            return false;

        foreach (RoomGraphEdge edge in model.Edges)
        {
            if (IsEdgeIncidentToNode(edge, node) && edge.WarningCount > 0)
                return true;
        }

        return false;
    }

    public static RoomGraphStatus GetRoomStatus(
        RoomConnectivityGraphModel model,
        RoomGraphNode node)
    {
        if (node == null)
            return RoomGraphStatus.Valid;

        RoomGraphStatus status = node.Status;

        if (status == RoomGraphStatus.Invalid)
            return status;

        bool invalidIncident = false;
        bool warningIncident = false;

        if (model != null)
        {
            foreach (RoomGraphEdge edge in model.Edges)
            {
                if (!IsEdgeIncidentToNode(edge, node))
                    continue;

                if (edge.HasErrors)
                    invalidIncident = true;
                else if (edge.WarningCount > 0)
                    warningIncident = true;
            }
        }

        if (invalidIncident)
            return RoomGraphStatus.Invalid;

        if (warningIncident && status == RoomGraphStatus.Valid)
            return RoomGraphStatus.Warning;

        return status;
    }

    public static bool IsEdgeIncidentToNode(RoomGraphEdge edge, RoomGraphNode node)
    {
        if (edge == null || node == null)
            return false;

        if (ReferenceEquals(edge.SourceNode, node) || ReferenceEquals(edge.TargetNode, node))
            return true;

        if (edge.SourceNode == null &&
            !string.IsNullOrEmpty(node.RoomId) &&
            string.Equals(edge.EndpointARoomId, node.RoomId, StringComparison.Ordinal))
        {
            return true;
        }

        if (edge.TargetNode == null &&
            !string.IsNullOrEmpty(node.RoomId) &&
            string.Equals(edge.EndpointBRoomId, node.RoomId, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static int CompareRooms(RoomGraphNode a, RoomGraphNode b)
    {
        int display = string.CompareOrdinal(a.DisplayName, b.DisplayName);

        if (display != 0)
            return display;

        int roomId = string.CompareOrdinal(a.RoomId, b.RoomId);

        if (roomId != 0)
            return roomId;

        return string.CompareOrdinal(a.NodeKey, b.NodeKey);
    }

    private static int CompareSearchResults(RoomGraphSearchResult a, RoomGraphSearchResult b)
    {
        int title = string.CompareOrdinal(a.Title, b.Title);

        if (title != 0)
            return title;

        int subtitle = string.CompareOrdinal(a.Subtitle, b.Subtitle);

        if (subtitle != 0)
            return subtitle;

        return a.Kind.CompareTo(b.Kind);
    }
}
