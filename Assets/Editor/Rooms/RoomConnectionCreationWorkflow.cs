using System;

internal enum RoomConnectionCreationStep
{
    Inactive = 0,
    SelectSource = 1,
    SelectDestination = 2,
    SelectMode = 3,
    Confirm = 4
}

/// <summary>
/// Transient, editor-window-only state for the guided connection authoring
/// flow. It never creates or mutates RoomConnection assets.
/// </summary>
internal sealed class RoomConnectionCreationWorkflow
{
    public bool IsActive => Step != RoomConnectionCreationStep.Inactive;
    public RoomConnectionCreationStep Step { get; private set; }
    public RoomGraphDoor Source { get; private set; }
    public RoomGraphDoor Destination { get; private set; }
    public RoomConnectionDirection Direction { get; private set; } = RoomConnectionDirection.Bidirectional;

    public void Start()
    {
        Source = null;
        Destination = null;
        Direction = RoomConnectionDirection.Bidirectional;
        Step = RoomConnectionCreationStep.SelectSource;
    }

    public void Cancel()
    {
        Source = null;
        Destination = null;
        Step = RoomConnectionCreationStep.Inactive;
    }

    public bool ChooseSource(RoomGraphDoor door, out string error)
    {
        error = null;

        if (Step != RoomConnectionCreationStep.SelectSource)
        {
            error = "Go back to the source step before changing endpoint A.";
            return false;
        }

        Source = door;
        Step = RoomConnectionCreationStep.SelectDestination;
        return true;
    }

    public bool ChooseDestination(RoomGraphDoor door, out string error)
    {
        error = null;

        if (Step != RoomConnectionCreationStep.SelectDestination)
        {
            error = "Go back to the destination step before changing endpoint B.";
            return false;
        }

        if (AreSameEndpoint(Source, door))
        {
            error = "Endpoint B must be a different door from endpoint A.";
            return false;
        }

        if (Source != null && door != null &&
            (ReferenceEquals(Source.Room, door.Room) ||
             string.Equals(Source.Room != null ? Source.Room.RoomId : null,
                 door.Room != null ? door.Room.RoomId : null,
                 StringComparison.Ordinal)))
        {
            error = "Self-connections are not supported. Select a door in a different room.";
            return false;
        }

        Destination = door;
        Step = RoomConnectionCreationStep.SelectMode;
        return true;
    }

    public bool SetDirection(RoomConnectionDirection direction)
    {
        if (!IsActive ||
            (direction != RoomConnectionDirection.Bidirectional &&
             direction != RoomConnectionDirection.OneWay))
        {
            return false;
        }

        Direction = direction;
        return true;
    }

    public bool ContinueToConfirmation(out string error)
    {
        error = null;

        if (Step != RoomConnectionCreationStep.SelectMode || Source == null || Destination == null)
        {
            error = "Select both endpoints before reviewing the connection.";
            return false;
        }

        Step = RoomConnectionCreationStep.Confirm;
        return true;
    }

    public bool GoBack()
    {
        switch (Step)
        {
            case RoomConnectionCreationStep.SelectDestination:
                Step = RoomConnectionCreationStep.SelectSource;
                return true;
            case RoomConnectionCreationStep.SelectMode:
                Step = RoomConnectionCreationStep.SelectDestination;
                return true;
            case RoomConnectionCreationStep.Confirm:
                Step = RoomConnectionCreationStep.SelectMode;
                return true;
            default:
                return false;
        }
    }

    public static bool AreSameEndpoint(RoomGraphDoor a, RoomGraphDoor b)
    {
        if (a == null || b == null)
            return false;

        return (ReferenceEquals(a, b)) ||
               (ReferenceEquals(a.Room, b.Room) &&
                !string.IsNullOrWhiteSpace(a.StableDoorId) &&
                string.Equals(a.StableDoorId, b.StableDoorId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Current-model eligibility used to explain disabled door choices. The
    /// RoomConnectionService repeats authoritative checks at confirmation time.
    /// </summary>
    public static string GetIneligibilityReason(
        RoomConnectivityGraphModel model,
        RoomGraphNode node,
        RoomGraphDoor door,
        RoomGraphDoor otherEndpoint = null)
    {
        if (model == null || !model.RegistryAvailable)
            return "RoomRegistry is unavailable; registered room doors cannot be selected.";

        if (node == null || door == null || door.Room == null ||
            !ReferenceEquals(node.Definition, door.Room) ||
            !ReferenceEquals(model.FindNode(door.Room), node))
        {
            return "This door no longer resolves to a registered room in the current graph. Refresh the graph.";
        }

        if (string.IsNullOrWhiteSpace(node.RoomId))
            return "The room has no registered Room ID.";

        int roomIdMatches = 0;
        int sceneGuidMatches = 0;
        foreach (RoomGraphNode candidate in model.Nodes)
        {
            if (string.Equals(candidate.RoomId, node.RoomId, StringComparison.Ordinal))
                roomIdMatches++;

            if (!string.IsNullOrWhiteSpace(node.Definition.SceneGuid) &&
                string.Equals(candidate.Definition.SceneGuid, node.Definition.SceneGuid, StringComparison.Ordinal))
            {
                sceneGuidMatches++;
            }
        }

        if (roomIdMatches != 1)
            return $"Room ID '{node.RoomId}' is not unique in RoomRegistry.";

        if (string.IsNullOrWhiteSpace(node.Definition.SceneGuid) || sceneGuidMatches != 1)
            return $"The scene GUID for room '{node.DisplayName}' is missing or not unique in RoomRegistry.";

        if (!node.SceneAvailable)
            return "The room's Scene GUID does not resolve to a scene.";

        if (string.IsNullOrWhiteSpace(door.StableDoorId))
            return "The door has no stable doorId. IDs are not repaired automatically.";

        int sameIdCount = 0;
        foreach (RoomGraphDoor candidate in node.Doors)
        {
            if (string.Equals(candidate.StableDoorId, door.StableDoorId, StringComparison.Ordinal))
                sameIdCount++;
        }

        if (sameIdCount != 1 || door.IsDuplicateId)
            return $"Door ID '{door.StableDoorId}' is not unique in room '{node.DisplayName}'.";

        if (door.Connection != null)
            return $"The door has a scene backlink to '{door.Connection.name}'.";

        if (door.ReferencedByTopology)
        {
            string[] names = new string[door.ReferencingEdges.Count];
            for (int i = 0; i < door.ReferencingEdges.Count; i++)
                names[i] = door.ReferencingEdges[i].AssetName;

            return "The door is already listed by connection asset(s): " + string.Join(", ", names) + ".";
        }

        if (otherEndpoint != null && RoomConnectionCreationWorkflow.AreSameEndpoint(door, otherEndpoint))
            return "The same door cannot be used for both endpoints.";

        if (otherEndpoint != null &&
            (ReferenceEquals(door.Room, otherEndpoint.Room) ||
             string.Equals(door.Room.RoomId, otherEndpoint.Room.RoomId, StringComparison.Ordinal)))
        {
            return "Self-connections are not supported. Choose a door in a different room.";
        }

        if (otherEndpoint != null)
        {
            RoomGraphNode otherNode = model.FindNode(otherEndpoint.Room);
            if (otherNode == null ||
                string.Equals(node.Definition.SceneGuid, otherNode.Definition.SceneGuid, StringComparison.Ordinal))
            {
                return "Both endpoints must be in different registered scenes.";
            }
        }

        return null;
    }

    public static RoomGraphDoor ResolveCurrentDoor(
        RoomConnectivityGraphModel model,
        RoomGraphDoor selected,
        out RoomGraphNode node,
        out string error)
    {
        node = null;
        error = null;

        if (model == null || selected == null || selected.Room == null)
        {
            error = "A selected endpoint is missing. Select it again.";
            return null;
        }

        node = model.FindNode(selected.Room);
        if (node == null)
        {
            error = $"Room '{selected.Room.name}' is no longer registered. Refresh and choose another endpoint.";
            return null;
        }

        RoomGraphDoor match = null;
        int matches = 0;
        foreach (RoomGraphDoor door in node.Doors)
        {
            if (!string.Equals(door.StableDoorId, selected.StableDoorId, StringComparison.Ordinal))
                continue;

            match = door;
            matches++;
        }

        if (matches != 1)
        {
            error = matches == 0
                ? $"Door '{selected.StableDoorId}' is no longer present in room '{node.DisplayName}'. Refresh and choose another endpoint."
                : $"Door ID '{selected.StableDoorId}' is now duplicated in room '{node.DisplayName}'.";
            return null;
        }

        error = GetIneligibilityReason(model, node, match);
        return error == null ? match : null;
    }
}
