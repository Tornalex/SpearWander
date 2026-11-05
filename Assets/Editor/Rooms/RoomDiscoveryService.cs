using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct DiscoveredDoor
{
    public DiscoveredDoor(
        string doorID,
        string stableDoorId,
        string gameObjectName,
        bool isActive,
        string targetSceneName,
        string targetDoorID,
        RoomConnection connection)
    {
        DoorID = doorID;
        StableDoorId = stableDoorId;
        GameObjectName = gameObjectName;
        IsActive = isActive;
        TargetSceneName = targetSceneName;
        TargetDoorID = targetDoorID;
        Connection = connection;
    }

    public string DoorID { get; }
    public string StableDoorId { get; }
    public string GameObjectName { get; }
    public bool IsActive { get; }
    public string TargetSceneName { get; }
    public string TargetDoorID { get; }
    public RoomConnection Connection { get; }
}

internal readonly struct DoorDiscoveryResult
{
    public DoorDiscoveryResult(List<DiscoveredDoor> doors, string message)
    {
        Doors = doors;
        Message = message;
    }

    public List<DiscoveredDoor> Doors { get; }
    public string Message { get; }
}

public static class RoomDiscoveryService
{
    [MenuItem("Tools/Project Spear/Rooms/Discover Doors in Registered Scenes")]
    public static void DiscoverAllMenu()
    {
        if (!RoomRegistrationService.TryGetRegistry(out RoomRegistry registry, out string error))
        {
            Debug.LogWarning($"[RoomDiscovery] {error}");
            return;
        }

        int roomCount = 0;
        int doorCount = 0;

        foreach (RoomDefinition definition in registry.Rooms)
        {
            if (definition == null)
                continue;

            roomCount++;

            List<DiscoveredDoor> doors = DiscoverDoors(definition, out string message);
            if (doors == null)
            {
                Debug.LogWarning($"[RoomDiscovery] {message}");
                continue;
            }

            doorCount += doors.Count;
            Debug.Log($"[RoomDiscovery] Scene '{definition.SceneName}' (room '{definition.RoomId}'): {doors.Count} door(s).");

            foreach (DiscoveredDoor door in doors)
            {
                Debug.Log(
                    $"[RoomDiscovery]   Door '{door.DoorID}' (stable '{door.StableDoorId}', object '{door.GameObjectName}', " +
                    $"{(door.IsActive ? "active" : "inactive")}) -> " +
                    $"'{door.TargetSceneName}' / '{door.TargetDoorID}', " +
                    $"connection '{(door.Connection != null ? door.Connection.name : "none")}'");
            }
        }

        Debug.Log($"[RoomDiscovery] Scanned {roomCount} registered scene(s), found {doorCount} door(s).");
    }

    /// <summary>
    /// Reads RoomTransition components from the registered scene without modifying or saving it.
    /// The scene is opened additively when it is not already loaded, then closed again.
    /// </summary>
    public static List<DiscoveredDoor> DiscoverDoors(RoomDefinition definition, out string message)
    {
        if (definition == null)
        {
            message = "Cannot discover doors for a null RoomDefinition.";
            return null;
        }

        string scenePath = AssetDatabase.GUIDToAssetPath(definition.SceneGuid);
        if (string.IsNullOrEmpty(scenePath))
        {
            message = $"Could not resolve a scene path for room '{definition.RoomId}' (GUID '{definition.SceneGuid}').";
            return null;
        }

        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedByThisPass = !scene.IsValid() || !scene.isLoaded;

        try
        {
            if (openedByThisPass)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            List<DiscoveredDoor> doors = new List<DiscoveredDoor>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RoomTransition transition in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
                {
                    doors.Add(new DiscoveredDoor(
                        transition.DoorID,
                        transition.DoorId,
                        transition.gameObject.name,
                        transition.gameObject.activeInHierarchy,
                        transition.TargetSceneName,
                        transition.TargetDoorID,
                        transition.Connection));
                }
            }

            message = null;
            return doors;
        }
        catch (Exception exception)
        {
            message = $"Could not discover doors in '{scenePath}': {exception.Message}";
            Debug.LogException(exception);
            return null;
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(originalActiveScene);

            if (openedByThisPass && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }
}
