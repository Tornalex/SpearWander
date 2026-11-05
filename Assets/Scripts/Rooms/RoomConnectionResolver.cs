using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum RoomConnectionResolutionStatus
{
    Success = 0,
    DestinationValidationPending = 1,
    MissingConnection = 2,
    InvalidSourceRoom = 3,
    InvalidSourceDoorId = 4,
    InvalidConnection = 5,
    WrongDirection = 6,
    InvalidDestinationRoom = 7
}

/// <summary>
/// Explicit result for a RoomTransition-to-RoomConnection resolution request.
/// A route is only Success after the destination scene's door backlink has
/// also been checked.
/// </summary>
public sealed class RoomConnectionResolutionResult
{
    internal RoomConnectionResolutionResult(
        RoomConnectionResolutionStatus status,
        string message,
        RoomConnection connection = null,
        RoomDefinition sourceRoom = null,
        string sourceDoorId = null,
        RoomDefinition destinationRoom = null,
        string destinationDoorId = null,
        string destinationScenePath = null)
    {
        Status = status;
        Message = message;
        Connection = connection;
        SourceRoom = sourceRoom;
        SourceDoorId = sourceDoorId;
        DestinationRoom = destinationRoom;
        DestinationDoorId = destinationDoorId;
        DestinationScenePath = destinationScenePath;
    }

    public RoomConnectionResolutionStatus Status { get; }
    public string Message { get; }
    public RoomConnection Connection { get; }
    public RoomDefinition SourceRoom { get; }
    public string SourceDoorId { get; }
    public RoomDefinition DestinationRoom { get; }
    public string DestinationDoorId { get; }
    public string DestinationScenePath { get; }
    public bool IsSuccess => Status == RoomConnectionResolutionStatus.Success;
}

/// <summary>
/// Resolves a door's assigned connection into a stable destination. This class
/// does not load scenes; destination backlink verification is completed once
/// the caller has the destination scene's RoomTransition components.
/// </summary>
public static class RoomConnectionResolver
{
    public static RoomConnectionResolutionResult Resolve(
        RoomTransition sourceDoor,
        RuntimeRoomCatalog catalog)
    {
        string scenePath = sourceDoor != null && sourceDoor.gameObject != null
            ? sourceDoor.gameObject.scene.path
            : null;
        return Resolve(sourceDoor, catalog, scenePath);
    }

    /// <summary>
    /// Resolves the source endpoint using the exact path of its containing scene.
    /// The overload accepts an explicit path for callers that already hold the
    /// scene context (and for isolated tests using transient scene objects).
    /// If the GameObject's scene has a path, it must match this value.
    /// </summary>
    public static RoomConnectionResolutionResult Resolve(
        RoomTransition sourceDoor,
        RuntimeRoomCatalog catalog,
        string sourceScenePath)
    {
        if (sourceDoor == null)
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidSourceDoorId,
                "The source RoomTransition is missing; its stable doorId cannot be read.");
        }

        if (catalog == null)
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidSourceRoom,
                "RuntimeRoomCatalog is missing; the source room cannot be resolved.");
        }

        if (!catalog.IsValid)
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidSourceRoom,
                "RuntimeRoomCatalog is invalid: " + catalog.ValidationError);
        }

        if (sourceDoor.gameObject == null || !sourceDoor.gameObject.scene.IsValid())
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidSourceRoom,
                "The source door is not attached to a valid scene.");
        }

        string actualSourceScenePath = sourceDoor.gameObject.scene.path;
        if (!string.IsNullOrWhiteSpace(actualSourceScenePath) &&
            !string.Equals(actualSourceScenePath, sourceScenePath, StringComparison.Ordinal))
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidSourceRoom,
                $"Source door '{sourceDoor.DoorId}' belongs to '{actualSourceScenePath}', " +
                $"not the supplied scene path '{sourceScenePath}'.");
        }

        if (string.IsNullOrWhiteSpace(sourceScenePath) ||
            !catalog.TryGetRoomByScenePath(sourceScenePath, out RoomDefinition sourceRoom))
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidSourceRoom,
                $"Source scene path '{sourceScenePath ?? "<empty>"}' does not resolve to exactly one registered room.");
        }

        string sourceDoorId = sourceDoor.DoorId;
        if (string.IsNullOrWhiteSpace(sourceDoorId))
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidSourceDoorId,
                $"Source room '{sourceRoom.RoomId}' has a door with no stable doorId.",
                sourceRoom: sourceRoom);
        }

        if (CountDoorsWithId(sourceDoor.gameObject.scene, sourceDoorId) != 1)
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidSourceDoorId,
                $"Stable doorId '{sourceDoorId}' is missing or duplicated in source room '{sourceRoom.RoomId}'.",
                sourceRoom: sourceRoom,
                sourceDoorId: sourceDoorId);
        }

        RoomConnection connection = sourceDoor.Connection;
        if (connection == null)
        {
            return Failure(
                RoomConnectionResolutionStatus.MissingConnection,
                $"Source endpoint '{sourceRoom.RoomId}/{sourceDoorId}' has no RoomConnection assigned.",
                sourceRoom: sourceRoom,
                sourceDoorId: sourceDoorId);
        }

        if (!IsValidDirection(connection.Direction))
        {
            return InvalidConnection(
                connection,
                sourceRoom,
                sourceDoorId,
                $"Connection '{connection.name}' has invalid direction value {(int)connection.Direction}.");
        }

        if (string.IsNullOrWhiteSpace(connection.EndpointARoomId) ||
            string.IsNullOrWhiteSpace(connection.EndpointADoorId) ||
            string.IsNullOrWhiteSpace(connection.EndpointBRoomId) ||
            string.IsNullOrWhiteSpace(connection.EndpointBDoorId))
        {
            return InvalidConnection(
                connection,
                sourceRoom,
                sourceDoorId,
                $"Connection '{connection.name}' has an empty endpoint RoomId or doorId.");
        }

        if (string.Equals(connection.EndpointARoomId, connection.EndpointBRoomId, StringComparison.Ordinal))
        {
            return InvalidConnection(
                connection,
                sourceRoom,
                sourceDoorId,
                $"Connection '{connection.name}' is a self-connection; room self-connections are unsupported.");
        }

        bool matchesEndpointA =
            string.Equals(connection.EndpointARoomId, sourceRoom.RoomId, StringComparison.Ordinal) &&
            string.Equals(connection.EndpointADoorId, sourceDoorId, StringComparison.Ordinal);
        bool matchesEndpointB =
            string.Equals(connection.EndpointBRoomId, sourceRoom.RoomId, StringComparison.Ordinal) &&
            string.Equals(connection.EndpointBDoorId, sourceDoorId, StringComparison.Ordinal);

        if (matchesEndpointA == matchesEndpointB)
        {
            return InvalidConnection(
                connection,
                sourceRoom,
                sourceDoorId,
                $"Connection '{connection.name}' does not identify source endpoint '{sourceRoom.RoomId}/{sourceDoorId}' exactly once.");
        }

        if (connection.Direction == RoomConnectionDirection.OneWay && matchesEndpointB)
        {
            return new RoomConnectionResolutionResult(
                RoomConnectionResolutionStatus.WrongDirection,
                $"OneWay connection '{connection.name}' only permits Endpoint A to initiate; " +
                $"'{sourceRoom.RoomId}/{sourceDoorId}' is Endpoint B.",
                connection,
                sourceRoom,
                sourceDoorId);
        }

        string destinationRoomId = matchesEndpointA
            ? connection.EndpointBRoomId
            : connection.EndpointARoomId;
        string destinationDoorId = matchesEndpointA
            ? connection.EndpointBDoorId
            : connection.EndpointADoorId;

        if (string.IsNullOrWhiteSpace(destinationDoorId) ||
            !catalog.TryGetRoom(destinationRoomId, out RoomDefinition destinationRoom, out string destinationScenePath))
        {
            return new RoomConnectionResolutionResult(
                RoomConnectionResolutionStatus.InvalidDestinationRoom,
                $"Connection '{connection.name}' destination '{destinationRoomId}/{destinationDoorId}' " +
                "does not resolve to a registered room and stable doorId.",
                connection,
                sourceRoom,
                sourceDoorId,
                destinationDoorId: destinationDoorId);
        }

        if (string.Equals(sourceRoom.RoomId, destinationRoom.RoomId, StringComparison.Ordinal))
        {
            return InvalidConnection(
                connection,
                sourceRoom,
                sourceDoorId,
                $"Connection '{connection.name}' resolves both endpoints to room '{sourceRoom.RoomId}'.");
        }

        return new RoomConnectionResolutionResult(
            RoomConnectionResolutionStatus.DestinationValidationPending,
            $"Resolved '{sourceRoom.RoomId}/{sourceDoorId}' to " +
            $"'{destinationRoom.RoomId}/{destinationDoorId}'. Destination door backlink validation is pending.",
            connection,
            sourceRoom,
            sourceDoorId,
            destinationRoom,
            destinationDoorId,
            destinationScenePath);
    }

    /// <summary>
    /// Completes resolution after the caller has loaded the destination scene.
    /// destinationDoors must contain all RoomTransition components from that
    /// exact scene, including inactive objects.
    /// </summary>
    public static RoomConnectionResolutionResult ValidateDestinationBacklink(
        RoomConnectionResolutionResult resolution,
        string loadedDestinationScenePath,
        IEnumerable<RoomTransition> destinationDoors)
    {
        if (resolution == null)
        {
            return Failure(
                RoomConnectionResolutionStatus.InvalidConnection,
                "The pending connection resolution result is missing.");
        }

        if (resolution.Status != RoomConnectionResolutionStatus.DestinationValidationPending)
            return resolution;

        if (!string.Equals(
                resolution.DestinationScenePath,
                loadedDestinationScenePath,
                StringComparison.Ordinal))
        {
            return DestinationFailure(
                resolution,
                $"Loaded destination path '{loadedDestinationScenePath ?? "<empty>"}' does not match " +
                $"catalog path '{resolution.DestinationScenePath}'.");
        }

        if (destinationDoors == null)
        {
            return DestinationFailure(
                resolution,
                $"Destination room '{resolution.DestinationRoom.RoomId}' supplied no RoomTransition components.");
        }

        RoomTransition match = null;
        int matches = 0;

        foreach (RoomTransition candidate in destinationDoors)
        {
            if (candidate == null ||
                !string.Equals(candidate.DoorId, resolution.DestinationDoorId, StringComparison.Ordinal))
            {
                continue;
            }

            string candidateScenePath = candidate.gameObject.scene.path;
            if (!string.IsNullOrWhiteSpace(candidateScenePath) &&
                !string.Equals(candidateScenePath, loadedDestinationScenePath, StringComparison.Ordinal))
            {
                continue;
            }

            match = candidate;
            matches++;
        }

        if (matches != 1)
        {
            return DestinationFailure(
                resolution,
                matches == 0
                    ? $"Destination door '{resolution.DestinationRoom.RoomId}/{resolution.DestinationDoorId}' was not found in '{loadedDestinationScenePath}'."
                    : $"Destination doorId '{resolution.DestinationDoorId}' appears {matches} times in room '{resolution.DestinationRoom.RoomId}'.");
        }

        if (match.Connection != resolution.Connection)
        {
            return DestinationFailure(
                resolution,
                $"Destination door '{resolution.DestinationRoom.RoomId}/{resolution.DestinationDoorId}' " +
                $"does not backlink to connection '{resolution.Connection.name}'.");
        }

        return new RoomConnectionResolutionResult(
            RoomConnectionResolutionStatus.Success,
            $"Resolved and backlink-validated '{resolution.SourceRoom.RoomId}/{resolution.SourceDoorId}' " +
            $"to '{resolution.DestinationRoom.RoomId}/{resolution.DestinationDoorId}'.",
            resolution.Connection,
            resolution.SourceRoom,
            resolution.SourceDoorId,
            resolution.DestinationRoom,
            resolution.DestinationDoorId,
            resolution.DestinationScenePath);
    }

    private static int CountDoorsWithId(Scene scene, string doorId)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RoomTransition door in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
            {
                if (string.Equals(door.DoorId, doorId, StringComparison.Ordinal))
                    count++;
            }
        }

        return count;
    }

    private static bool IsValidDirection(RoomConnectionDirection direction)
    {
        return direction == RoomConnectionDirection.Bidirectional ||
               direction == RoomConnectionDirection.OneWay;
    }

    private static RoomConnectionResolutionResult InvalidConnection(
        RoomConnection connection,
        RoomDefinition sourceRoom,
        string sourceDoorId,
        string detail)
    {
        return new RoomConnectionResolutionResult(
            RoomConnectionResolutionStatus.InvalidConnection,
            detail + $" Source endpoint: '{sourceRoom.RoomId}/{sourceDoorId}'.",
            connection,
            sourceRoom,
            sourceDoorId);
    }

    private static RoomConnectionResolutionResult DestinationFailure(
        RoomConnectionResolutionResult resolution,
        string detail)
    {
        return new RoomConnectionResolutionResult(
            RoomConnectionResolutionStatus.InvalidDestinationRoom,
            detail,
            resolution.Connection,
            resolution.SourceRoom,
            resolution.SourceDoorId,
            resolution.DestinationRoom,
            resolution.DestinationDoorId,
            resolution.DestinationScenePath);
    }

    private static RoomConnectionResolutionResult Failure(
        RoomConnectionResolutionStatus status,
        string message,
        RoomConnection connection = null,
        RoomDefinition sourceRoom = null,
        string sourceDoorId = null)
    {
        return new RoomConnectionResolutionResult(
            status,
            message,
            connection,
            sourceRoom,
            sourceDoorId);
    }
}
