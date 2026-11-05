using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum DoorIdStatus
{
    Valid = 0,
    Missing = 1,
    Duplicate = 2
}

public sealed class DoorIdAnalysisEntry
{
    public DoorIdAnalysisEntry(
        RoomDefinition room,
        string scenePath,
        string gameObjectName,
        string stableDoorId,
        string legacyDoorId,
        string targetSceneName,
        string targetDoorId,
        RoomConnection connection,
        DoorIdStatus status)
    {
        Room = room;
        ScenePath = scenePath;
        GameObjectName = gameObjectName;
        StableDoorId = stableDoorId;
        LegacyDoorId = legacyDoorId;
        TargetSceneName = targetSceneName;
        TargetDoorId = targetDoorId;
        Connection = connection;
        Status = status;
    }

    public RoomDefinition Room { get; }
    public string RoomId => Room != null ? Room.RoomId : null;
    public string ScenePath { get; }
    public string GameObjectName { get; }
    public string StableDoorId { get; }
    public string LegacyDoorId { get; }
    public string TargetSceneName { get; }
    public string TargetDoorId { get; }
    public RoomConnection Connection { get; }
    public DoorIdStatus Status { get; }
}

public sealed class DoorIdAnalysisReport
{
    private readonly List<DoorIdAnalysisEntry> _entries = new List<DoorIdAnalysisEntry>();
    private readonly List<string> _messages = new List<string>();
    private bool _isComplete = true;

    public IReadOnlyList<DoorIdAnalysisEntry> Entries => _entries;
    public IReadOnlyList<string> Messages => _messages;
    public bool IsComplete => _isComplete;

    public int ValidCount { get; private set; }
    public int MissingCount { get; private set; }
    public int DuplicateCount { get; private set; }
    public bool HasIssues => MissingCount > 0 || DuplicateCount > 0;

    public void AddEntry(DoorIdAnalysisEntry entry)
    {
        _entries.Add(entry);

        switch (entry.Status)
        {
            case DoorIdStatus.Missing:
                MissingCount++;
                break;
            case DoorIdStatus.Duplicate:
                DuplicateCount++;
                break;
            default:
                ValidCount++;
                break;
        }
    }

    public void AddMessage(string message)
    {
        if (!string.IsNullOrEmpty(message))
            _messages.Add(message);
    }

    public void MarkIncomplete(string message)
    {
        _isComplete = false;
        AddMessage(message);
    }
}

/// <summary>
/// Read-only analysis of stable door IDs plus an explicit assignment pass for missing IDs.
/// IDs are only ever generated for doors whose stable doorId is empty. Existing IDs,
/// including duplicates, are never regenerated or overwritten.
/// </summary>
public static class DoorIdMigrationService
{
    [MenuItem("Tools/Project Spear/Rooms/Migrate/Assign Missing Door IDs…")]
    public static void AssignMissingDoorIdsMenu()
    {
        bool success = AssignMissingDoorIds(out string message, out int assigned);

        if (!success)
            Debug.LogError($"[DoorIdMigration] {message}");
        else if (assigned > 0)
            Debug.Log($"[DoorIdMigration] {message}");
        else
            Debug.Log($"[DoorIdMigration] {message}");

        EditorUtility.DisplayDialog("Door ID Migration", message, "OK");
    }

    /// <summary>
    /// Reads doors from every registered scene. Never modifies or saves anything.
    /// </summary>
    public static DoorIdAnalysisReport Analyze()
    {
        DoorIdAnalysisReport report = new DoorIdAnalysisReport();

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry == null)
        {
            report.MarkIncomplete($"RoomRegistry is missing at '{RoomRegistrationService.RegistryPath}'.");
            return report;
        }

        Scene originalActiveScene = SceneManager.GetActiveScene();
        bool hasOriginalActiveScene = IsSceneLoadedInEditor(originalActiveScene);

        if (!hasOriginalActiveScene)
            report.AddMessage("No valid loaded active scene was available to restore after room analysis.");

        try
        {
            foreach (RoomDefinition room in registry.Rooms)
            {
                if (room == null)
                {
                    report.MarkIncomplete("RoomRegistry contains a missing RoomDefinition reference.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(room.RoomId))
                {
                    report.MarkIncomplete($"RoomDefinition '{room.name}' has no room ID and was skipped.");
                    continue;
                }

                if (!TryOpenRoomScene(
                        room,
                        out Scene scene,
                        out bool openedByAnalysis,
                        out string sceneMessage))
                {
                    report.MarkIncomplete(sceneMessage);
                    continue;
                }

                try
                {
                    List<RoomTransition> doors = CollectDoors(scene);
                    Dictionary<string, int> counts = CountDoorIds(doors);

                    foreach (RoomTransition door in doors)
                    {
                        string stableId = door.DoorId;
                        DoorIdStatus status;

                        if (string.IsNullOrWhiteSpace(stableId))
                            status = DoorIdStatus.Missing;
                        else if (counts[stableId] > 1)
                            status = DoorIdStatus.Duplicate;
                        else
                            status = DoorIdStatus.Valid;

                        report.AddEntry(new DoorIdAnalysisEntry(
                            room,
                            scene.path,
                            door.gameObject.name,
                            stableId,
                            door.DoorID,
                            door.TargetSceneName,
                            door.TargetDoorID,
                            door.Connection,
                            status));
                    }
                }
                catch (Exception exception)
                {
                    report.MarkIncomplete(
                        $"Could not analyze room '{room.RoomId}' ('{room.name}') in '{scene.path}': " +
                        exception.Message);
                }
                finally
                {
                    if (!CloseTemporaryScene(scene, openedByAnalysis, out string closeMessage))
                        report.MarkIncomplete(closeMessage);
                }
            }
        }
        finally
        {
            if (!RestoreActiveScene(originalActiveScene, hasOriginalActiveScene, out string restoreMessage))
            {
                report.MarkIncomplete(restoreMessage);
            }
        }

        return report;
    }

    /// <summary>
    /// Generates a stable ID for every door whose doorId is empty and saves the scene.
    /// Duplicate IDs and already valid IDs are preserved untouched.
    /// </summary>
    public static bool AssignMissingDoorIds(out string message, out int assignedCount)
    {
        assignedCount = 0;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Door ID assignment is not available in Play Mode.";
            return false;
        }

        if (!TryLoadRegistry(out RoomRegistry registry, out string registryMessage))
        {
            message = registryMessage;
            return false;
        }

        int sceneCount = 0;
        List<string> problems = new List<string>();
        Scene originalActiveScene = SceneManager.GetActiveScene();
        bool hasOriginalActiveScene = IsSceneLoadedInEditor(originalActiveScene);

        try
        {
            foreach (RoomDefinition room in registry.Rooms)
            {
                if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
                    continue;

                string scenePath = AssetDatabase.GUIDToAssetPath(room.SceneGuid);
                if (string.IsNullOrEmpty(scenePath))
                {
                    problems.Add(
                        $"Could not resolve a scene path for room '{room.RoomId}' ('{room.name}') " +
                        $"(GUID '{room.SceneGuid}'); AssetDatabase returned an empty path.");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    problems.Add(
                        $"Room '{room.RoomId}' ('{room.name}') resolves to '{scenePath}' " +
                        $"(GUID '{room.SceneGuid}'), but Unity does not recognize it as a scene asset.");
                    continue;
                }

                Scene scene = FindLoadedRoomScene(scenePath, room.SceneGuid);
                bool alreadyLoaded = scene.IsValid() && scene.isLoaded;

                if (alreadyLoaded && scene.isDirty)
                {
                    problems.Add($"Scene '{scene.name}' has unsaved changes. Save it before assigning door IDs.");
                    continue;
                }

                bool openedByOperation = false;

                if (!alreadyLoaded)
                {
                    try
                    {
                        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                        openedByOperation = true;
                    }
                    catch (Exception exception)
                    {
                        Scene partiallyOpenedScene = FindLoadedRoomScene(scenePath, room.SceneGuid);
                        if (partiallyOpenedScene.IsValid() && partiallyOpenedScene.isLoaded)
                        {
                            if (!CloseTemporaryScene(
                                    partiallyOpenedScene,
                                    true,
                                    out string closeMessage))
                            {
                                problems.Add(closeMessage);
                            }
                        }

                        problems.Add(
                            $"Could not open scene for room '{room.RoomId}' ('{room.name}') at '{scenePath}' " +
                            $"(GUID '{room.SceneGuid}'): {exception.Message}");
                        continue;
                    }
                }

                try
                {
                    List<RoomTransition> doors = CollectDoors(scene);
                    HashSet<string> usedIds = new HashSet<string>(StringComparer.Ordinal);

                    foreach (RoomTransition door in doors)
                    {
                        if (!string.IsNullOrWhiteSpace(door.DoorId))
                            usedIds.Add(door.DoorId);
                    }

                    List<AssignedId> assignments = new List<AssignedId>();

                    foreach (RoomTransition door in doors)
                    {
                        if (!string.IsNullOrWhiteSpace(door.DoorId))
                            continue;

                        string newId = GenerateDoorId(usedIds);
                        usedIds.Add(newId);

                        assignments.Add(new AssignedId(door, door.DoorId, newId));
                        SetDoorId(door, newId);
                        assignedCount++;
                    }

                    if (assignments.Count == 0)
                        continue;

                    sceneCount++;
                    EditorSceneManager.MarkSceneDirty(scene);

                    if (EditorSceneManager.SaveScene(scene))
                        continue;

                    // Saving failed: revert the in-memory changes so no partial data is left behind.
                    foreach (AssignedId assignment in assignments)
                        SetDoorId(assignment.Door, assignment.OriginalDoorId);

                    assignedCount -= assignments.Count;
                    problems.Add($"Could not save scene '{scene.name}' after assigning door IDs. No IDs were changed on disk.");

                    if (!openedByOperation)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene))
                            Debug.LogError($"[DoorIdMigration] Could not restore scene '{scene.name}' after a failed save.");
                    }
                }
                finally
                {
                    if (!CloseTemporaryScene(scene, openedByOperation, out string closeMessage))
                        problems.Add(closeMessage);
                }
            }
        }
        catch (Exception exception)
        {
            problems.Add("Door ID assignment failed: " + exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            if (!RestoreActiveScene(originalActiveScene, hasOriginalActiveScene, out string restoreMessage))
                problems.Add(restoreMessage);
        }

        message =
            $"Assigned {assignedCount} missing door ID(s) across {sceneCount} scene(s). " +
            $"{problems.Count} problem(s).";

        if (problems.Count > 0)
        {
            foreach (string problem in problems)
                message += "\n- " + problem;

            return false;
        }

        return true;
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

    private static bool TryOpenRoomScene(
        RoomDefinition room,
        out Scene scene,
        out bool openedByAnalysis,
        out string message)
    {
        scene = default;
        openedByAnalysis = false;

        string scenePath = AssetDatabase.GUIDToAssetPath(room.SceneGuid);
        if (string.IsNullOrEmpty(scenePath))
        {
            message =
                $"Could not resolve a scene path for room '{room.RoomId}' ('{room.name}') " +
                $"(GUID '{room.SceneGuid}'); AssetDatabase returned an empty path.";
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            message =
                $"Room '{room.RoomId}' ('{room.name}') resolves to '{scenePath}' (GUID '{room.SceneGuid}'), " +
                "but Unity does not currently recognize that path as a scene asset. The room was skipped; " +
                "verify the scene and its .meta file, then re-register the room if the scene was moved or renamed.";
            return false;
        }

        scene = FindLoadedRoomScene(scenePath, room.SceneGuid);
        if (scene.IsValid() && scene.isLoaded)
        {
            message = null;
            return true;
        }

        try
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            openedByAnalysis = true;
            message = null;
            return true;
        }
        catch (Exception exception)
        {
            Scene partiallyOpenedScene = FindLoadedRoomScene(scenePath, room.SceneGuid);
            string cleanupDetail = string.Empty;
            if (partiallyOpenedScene.IsValid() && partiallyOpenedScene.isLoaded &&
                !CloseTemporaryScene(partiallyOpenedScene, true, out cleanupDetail))
            {
                cleanupDetail = " " + cleanupDetail;
            }

            message =
                $"Could not open scene for room '{room.RoomId}' ('{room.name}') at '{scenePath}' " +
                $"(GUID '{room.SceneGuid}'): {exception.Message} The room was skipped; remaining registered " +
                "rooms will still be analyzed. Verify the scene is present and imported, then re-register the " +
                $"room if its scene reference is outdated.{cleanupDetail}";
            return false;
        }
    }

    private static Scene FindLoadedRoomScene(string scenePath, string sceneGuid)
    {
        Scene pathMatch = SceneManager.GetSceneByPath(scenePath);
        if (pathMatch.IsValid() && pathMatch.isLoaded)
            return pathMatch;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidate = SceneManager.GetSceneAt(i);
            if (!candidate.IsValid() || !candidate.isLoaded || string.IsNullOrEmpty(candidate.path))
                continue;

            if (string.Equals(
                    AssetDatabase.AssetPathToGUID(candidate.path),
                    sceneGuid,
                    StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return pathMatch;
    }

    private static List<RoomTransition> CollectDoors(Scene scene)
    {
        List<RoomTransition> doors = new List<RoomTransition>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RoomTransition door in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
                doors.Add(door);
        }

        return doors;
    }

    private static Dictionary<string, int> CountDoorIds(List<RoomTransition> doors)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (RoomTransition door in doors)
        {
            string id = door.DoorId;

            if (string.IsNullOrWhiteSpace(id))
                continue;

            counts.TryGetValue(id, out int count);
            counts[id] = count + 1;
        }

        return counts;
    }

    private static void SetDoorId(RoomTransition door, string doorId)
    {
        SerializedObject serializedDoor = new SerializedObject(door);
        SerializedProperty property = serializedDoor.FindProperty("doorId");

        if (property == null)
            return;

        property.stringValue = doorId;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();

        if (PrefabUtility.IsPartOfPrefabInstance(door))
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);

        EditorUtility.SetDirty(door);
    }

    private static string GenerateDoorId(HashSet<string> usedIds)
    {
        string candidate;

        do
        {
            candidate = "door_" + Guid.NewGuid().ToString("N");
        }
        while (usedIds.Contains(candidate));

        return candidate;
    }

    private static bool CloseTemporaryScene(Scene scene, bool openedByOperation, out string message)
    {
        message = null;
        if (!openedByOperation || !IsSceneLoadedInEditor(scene))
            return true;

        try
        {
            if (EditorSceneManager.CloseScene(scene, removeScene: true))
                return true;

            message =
                $"Could not close temporary room scene '{GetSceneDescription(scene)}' after migration processing.";
            Debug.LogError("[DoorIdMigration] " + message);
            return false;
        }
        catch (Exception exception)
        {
            message =
                $"Could not close temporary room scene '{GetSceneDescription(scene)}' after migration processing: " +
                exception.Message;
            Debug.LogError("[DoorIdMigration] " + message);
            return false;
        }
    }

    private static bool RestoreActiveScene(Scene originalActiveScene, bool hadOriginalActiveScene, out string message)
    {
        message = null;
        if (!hadOriginalActiveScene)
            return true;

        if (!IsSceneLoadedInEditor(originalActiveScene))
        {
            message =
                $"The previously active scene '{GetSceneDescription(originalActiveScene)}' is no longer loaded; " +
                "it could not be restored.";
            Debug.LogError("[DoorIdMigration] " + message);
            return false;
        }

        Scene currentActiveScene = SceneManager.GetActiveScene();
        if (IsSceneLoadedInEditor(currentActiveScene) && currentActiveScene == originalActiveScene)
            return true;

        try
        {
            if (SceneManager.SetActiveScene(originalActiveScene))
                return true;

            message =
                $"Unity could not restore the previously active scene '{GetSceneDescription(originalActiveScene)}'.";
            Debug.LogError("[DoorIdMigration] " + message);
            return false;
        }
        catch (Exception exception)
        {
            message =
                $"Unity could not restore the previously active scene '{GetSceneDescription(originalActiveScene)}': " +
                exception.Message;
            Debug.LogError("[DoorIdMigration] " + message);
            return false;
        }
    }

    private static bool IsSceneLoadedInEditor(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.IsValid() && loadedScene.isLoaded && loadedScene == scene)
                return true;
        }

        return false;
    }

    private static string GetSceneDescription(Scene scene)
    {
        return string.IsNullOrEmpty(scene.path)
            ? $"{(string.IsNullOrEmpty(scene.name) ? "<unnamed>" : scene.name)} (unsaved scene)"
            : scene.path;
    }

    private readonly struct AssignedId
    {
        public AssignedId(RoomTransition door, string originalDoorId, string newDoorId)
        {
            Door = door;
            OriginalDoorId = originalDoorId;
            NewDoorId = newDoorId;
        }

        public RoomTransition Door { get; }
        public string OriginalDoorId { get; }
        public string NewDoorId { get; }
    }

}
