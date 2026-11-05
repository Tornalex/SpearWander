using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomConnectionService
{
    public const string ConnectionsFolder = RoomRegistrationService.RegistryFolder + "/Connections";
    public const string CreateUndoName = "Create Room Connection";

    // Optional test seam for deterministic save-failure/rollback coverage.
    // Production callers leave this null and use EditorSceneManager.SaveScene.
    internal static Func<Scene, bool> SaveDoorSceneOverrideForTests;

    public static bool CreateConnection(
        RoomDefinition roomA,
        string doorAId,
        RoomDefinition roomB,
        string doorBId,
        RoomConnectionDirection direction,
        string connectionAssetName,
        out string message)
    {
        return CreateConnection(
            roomA,
            doorAId,
            roomB,
            doorBId,
            direction,
            connectionAssetName,
            out _,
            out message);
    }

    public static bool CreateConnection(
        RoomDefinition roomA,
        string doorAId,
        RoomDefinition roomB,
        string doorBId,
        RoomConnectionDirection direction,
        string connectionAssetName,
        out RoomConnection createdConnection,
        out string message)
    {
        createdConnection = null;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Room connections cannot be created in Play Mode.";
            return false;
        }

        if (roomA == null || roomB == null)
        {
            message = "Both endpoints must reference a RoomDefinition.";
            return false;
        }

        if (roomA == roomB || string.Equals(roomA.RoomId, roomB.RoomId, StringComparison.Ordinal))
        {
            message = "Self-connections are not supported in the MVP. Select two different rooms.";
            return false;
        }

        if (string.Equals(roomA.SceneGuid, roomB.SceneGuid, StringComparison.Ordinal))
        {
            message = "Both endpoints resolve to the same scene. Select doors from different registered scenes.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(roomA.RoomId) || string.IsNullOrWhiteSpace(roomB.RoomId))
        {
            message = "Both rooms must have a registered room ID.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(doorAId) || string.IsNullOrWhiteSpace(doorBId))
        {
            message = "Both endpoints must have a non-empty stable doorId.";
            return false;
        }

        if (direction != RoomConnectionDirection.Bidirectional && direction != RoomConnectionDirection.OneWay)
        {
            message = $"Invalid connection direction value ({(int)direction}).";
            return false;
        }

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry == null)
        {
            message = "RoomRegistry is missing. Register rooms before creating connections.";
            return false;
        }

        if (registry.Rooms == null)
        {
            message = "RoomRegistry has no room list.";
            return false;
        }

        if (!ContainsDefinition(registry, roomA) || !ContainsDefinition(registry, roomB))
        {
            message = "Both endpoint rooms must be present in RoomRegistry.";
            return false;
        }

        if (!ValidateRegisteredRoomIdentity(registry, roomA, out message) ||
            !ValidateRegisteredRoomIdentity(registry, roomB, out message))
        {
            return false;
        }

        List<DoorHandle> handles = new List<DoorHandle>();
        Scene originalActiveScene = SceneManager.GetActiveScene();
        RoomConnection connection = null;
        DoorHandle handleA = null;
        DoorHandle handleB = null;
        bool created = false;
        int undoGroup = -1;

        try
        {
            if (!TryGetDoor(roomA, doorAId, handles, out handleA, out message))
                return false;

            if (!TryGetDoor(roomB, doorBId, handles, out handleB, out message))
                return false;

            if (handleA.Door == handleB.Door)
            {
                message = "Endpoint A and endpoint B resolve to the same door.";
                return false;
            }

            if (handleA.Door.Connection != null)
            {
                message = $"Door '{doorAId}' in room '{roomA.RoomId}' is already connected to '{handleA.Door.Connection.name}'.";
                return false;
            }

            if (handleB.Door.Connection != null)
            {
                message = $"Door '{doorBId}' in room '{roomB.RoomId}' is already connected to '{handleB.Door.Connection.name}'.";
                return false;
            }

            if (TryFindConnectionUsingEndpoint(roomA.RoomId, doorAId, out RoomConnection existingA))
            {
                message = $"Door '{doorAId}' in room '{roomA.RoomId}' is already listed by connection '{existingA.name}'.";
                return false;
            }

            if (TryFindConnectionUsingEndpoint(roomB.RoomId, doorBId, out RoomConnection existingB))
            {
                message = $"Door '{doorBId}' in room '{roomB.RoomId}' is already listed by connection '{existingB.name}'.";
                return false;
            }

            EnsureFolder(ConnectionsFolder);

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(CreateUndoName);
            Undo.RecordObject(handleA.Door, CreateUndoName);
            Undo.RecordObject(handleB.Door, CreateUndoName);

            connection = ScriptableObject.CreateInstance<RoomConnection>();
            connection.Assign(roomA.RoomId, doorAId, roomB.RoomId, doorBId, direction);

            string finalName = string.IsNullOrWhiteSpace(connectionAssetName)
                ? $"Conn_{roomA.RoomId}_{doorAId}_to_{roomB.RoomId}_{doorBId}"
                : connectionAssetName;

            finalName = SanitizeFileName(finalName);
            if (finalName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                finalName = finalName.Substring(0, finalName.Length - ".asset".Length);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ConnectionsFolder}/{finalName}.asset");
            AssetDatabase.CreateAsset(connection, assetPath);
            Undo.RegisterCreatedObjectUndo(connection, CreateUndoName);

            if (!SetBacklink(handleA.Door, connection) || !SetBacklink(handleB.Door, connection))
            {
                RollbackAndDiscardUndo(connection, handleA, handleB, undoGroup);
                undoGroup = -1;
                message = "Could not assign the connection backlink to both doors.";
                return false;
            }

            if (!SaveDoorScene(handleA) || !SaveDoorScene(handleB))
            {
                RollbackAndDiscardUndo(connection, handleA, handleB, undoGroup);
                undoGroup = -1;
                message = "Could not save one of the scenes that contain the connected doors.";
                return false;
            }

            AssetDatabase.SaveAssets();
            Undo.FlushUndoRecordObjects();
            RoomValidationReport report = RoomConnectionValidator.Validate();
            message =
                $"Created connection '{connection.name}' ({direction}) between " +
                $"'{roomA.RoomId}/{doorAId}' and '{roomB.RoomId}/{doorBId}'. " +
                $"Validation: {report.ErrorCount} error(s), {report.WarningCount} warning(s).";

            Undo.CollapseUndoOperations(undoGroup);
            created = true;
            createdConnection = connection;

            return true;
        }
        catch (Exception exception)
        {
            if (connection != null && !created)
            {
                RollbackAndDiscardUndo(connection, handleA, handleB, undoGroup);
                undoGroup = -1;
            }

            createdConnection = null;
            message = "Connection creation failed: " + exception.Message;
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            CloseOpenedScenes(handles, originalActiveScene);
        }
    }

    private static bool TryGetDoor(
        RoomDefinition room,
        string doorId,
        List<DoorHandle> handles,
        out DoorHandle handle,
        out string message)
    {
        handle = null;

        string scenePath = AssetDatabase.GUIDToAssetPath(room.SceneGuid);
        if (string.IsNullOrEmpty(scenePath))
        {
            message = $"Could not resolve the scene path for room '{room.RoomId}'.";
            return false;
        }

        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool alreadyLoaded = scene.IsValid() && scene.isLoaded;

        if (alreadyLoaded && scene.isDirty)
        {
            message = $"Scene '{scene.name}' has unsaved changes. Save it before creating a connection.";
            return false;
        }

        bool openedByOperation = false;

        if (!alreadyLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            openedByOperation = true;
        }

        handle = new DoorHandle(scene, openedByOperation);
        handles.Add(handle);

        RoomTransition match = null;
        int matches = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RoomTransition transition in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
            {
                if (!string.Equals(transition.DoorId, doorId, StringComparison.Ordinal))
                    continue;

                match = transition;
                matches++;
            }
        }

        if (matches == 0)
        {
            message = $"No door with stable doorId '{doorId}' was found in room '{room.RoomId}'.";
            return false;
        }

        if (matches > 1)
        {
            message = $"Door ID '{doorId}' is duplicated in room '{room.RoomId}'.";
            return false;
        }

        handle.Door = match;
        message = null;
        return true;
    }

    private static bool SetBacklink(RoomTransition door, RoomConnection connection)
    {
        SerializedObject serializedDoor = new SerializedObject(door);
        SerializedProperty connectionProperty = serializedDoor.FindProperty("connection");

        if (connectionProperty == null)
            return false;

        connectionProperty.objectReferenceValue = connection;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();

        if (PrefabUtility.IsPartOfPrefabInstance(door))
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);

        return true;
    }

    private static void ClearBacklink(RoomTransition door)
    {
        if (door == null)
            return;

        SerializedObject serializedDoor = new SerializedObject(door);
        SerializedProperty connectionProperty = serializedDoor.FindProperty("connection");

        if (connectionProperty == null)
            return;

        connectionProperty.objectReferenceValue = null;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();

        if (PrefabUtility.IsPartOfPrefabInstance(door))
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);
    }

    private static bool SaveDoorScene(DoorHandle handle)
    {
        if (handle == null || handle.Door == null)
            return false;

        EditorSceneManager.MarkSceneDirty(handle.Scene);
        if (SaveDoorSceneOverrideForTests != null)
            return SaveDoorSceneOverrideForTests(handle.Scene);

        return EditorSceneManager.SaveScene(handle.Scene);
    }

    private static void Rollback(RoomConnection connection, DoorHandle handleA, DoorHandle handleB)
    {
        if (handleA != null && handleA.Door != null)
            ClearAndRestoreScene(handleA);

        if (handleB != null && handleB.Door != null)
            ClearAndRestoreScene(handleB);

        if (connection != null)
        {
            try
            {
                string path = AssetDatabase.GetAssetPath(connection);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.DeleteAsset(path);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RoomConnectionService] Could not delete connection asset during rollback: {exception.Message}");
            }
        }
    }

    private static void ClearAndRestoreScene(DoorHandle handle)
    {
        try
        {
            ClearBacklink(handle.Door);

            if (!handle.Scene.IsValid() || !handle.Scene.isLoaded)
                return;

            // The operation may already have saved this scene before another
            // endpoint failed. Persist the cleared backlink in either case.
            EditorSceneManager.MarkSceneDirty(handle.Scene);
            if (!EditorSceneManager.SaveScene(handle.Scene))
                Debug.LogError($"[RoomConnectionService] Could not restore scene '{handle.Scene.name}' after rollback.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RoomConnectionService] Rollback of scene '{handle.Scene.name}' failed: {exception.Message}");
        }
    }

    private static void RollbackAndDiscardUndo(
        RoomConnection connection,
        DoorHandle handleA,
        DoorHandle handleB,
        int undoGroup)
    {
        Rollback(connection, handleA, handleB);

        if (undoGroup < 0)
            return;

        try
        {
            Undo.RevertAllDownToGroup(undoGroup);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RoomConnectionService] Could not discard failed connection Undo records: {exception.Message}");
        }
    }

    private static void CloseOpenedScenes(List<DoorHandle> handles, Scene originalActiveScene)
    {
        if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            SceneManager.SetActiveScene(originalActiveScene);

        foreach (DoorHandle handle in handles)
        {
            if (handle.OpenedByOperation && handle.Scene.IsValid() && handle.Scene.isLoaded)
                EditorSceneManager.CloseScene(handle.Scene, removeScene: true);
        }
    }

    private static bool ContainsDefinition(RoomRegistry registry, RoomDefinition definition)
    {
        foreach (RoomDefinition room in registry.Rooms)
        {
            if (room == definition)
                return true;
        }

        return false;
    }

    private static bool ValidateRegisteredRoomIdentity(
        RoomRegistry registry,
        RoomDefinition definition,
        out string message)
    {
        int roomIdMatches = 0;
        int sceneGuidMatches = 0;

        foreach (RoomDefinition registered in registry.Rooms)
        {
            if (registered == null)
                continue;

            if (string.Equals(registered.RoomId, definition.RoomId, StringComparison.Ordinal))
                roomIdMatches++;

            if (string.Equals(registered.SceneGuid, definition.SceneGuid, StringComparison.Ordinal))
                sceneGuidMatches++;
        }

        if (roomIdMatches != 1)
        {
            message = $"Room ID '{definition.RoomId}' is not unique in RoomRegistry.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.SceneGuid) || sceneGuidMatches != 1)
        {
            message = $"The scene GUID for room '{definition.RoomId}' is missing or is not unique in RoomRegistry.";
            return false;
        }

        message = null;
        return true;
    }

    private static bool TryFindConnectionUsingEndpoint(string roomId, string doorId, out RoomConnection found)
    {
        found = null;

        string[] guids = AssetDatabase.FindAssets("t:RoomConnection");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            RoomConnection connection = AssetDatabase.LoadAssetAtPath<RoomConnection>(path);
            if (connection == null)
                continue;

            if (EndpointMatches(connection.EndpointARoomId, connection.EndpointADoorId, roomId, doorId) ||
                EndpointMatches(connection.EndpointBRoomId, connection.EndpointBDoorId, roomId, doorId))
            {
                found = connection;
                return true;
            }
        }

        return false;
    }

    private static bool EndpointMatches(string candidateRoomId, string candidateDoorId, string roomId, string doorId)
    {
        return string.Equals(candidateRoomId, roomId, StringComparison.Ordinal) &&
               string.Equals(candidateDoorId, doorId, StringComparison.Ordinal);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent))
            return;

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }

    private static string SanitizeFileName(string fileName)
    {
        StringBuilder builder = new StringBuilder(fileName.Length);

        foreach (char character in fileName)
        {
            builder.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0
                ? '_'
                : character);
        }

        return builder.ToString();
    }

    private sealed class DoorHandle
    {
        public DoorHandle(Scene scene, bool openedByOperation)
        {
            Scene = scene;
            OpenedByOperation = openedByOperation;
        }

        public Scene Scene { get; }
        public bool OpenedByOperation { get; }
        public RoomTransition Door { get; set; }
    }
}
