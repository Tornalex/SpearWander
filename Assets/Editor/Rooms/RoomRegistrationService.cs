using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomRegistrationService
{
    public const string RegistryFolder = "Assets/Data/Rooms";
    public const string RegistryPath = RegistryFolder + "/RoomRegistry.asset";
    public const string DefinitionsFolder = RegistryFolder + "/Definitions";
    private const string MasterSceneName = "Master";

    [MenuItem("Tools/Project Spear/Rooms/Register Open Scene…")]
    public static void RegisterOpenSceneMenu()
    {
        bool registered = RegisterOpenScene(out string message);

        if (registered)
            Debug.Log(message);
        else
            Debug.LogWarning(message);

        EditorUtility.DisplayDialog("Room Registration", message, "OK");
    }

    public static bool RegisterOpenScene(out string message)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            message = "Room registration is not available in Play Mode.";
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            message = "There is no valid open scene to register.";
            return false;
        }

        if (string.IsNullOrEmpty(scene.path))
        {
            message = "Save the open scene before registering it.";
            return false;
        }

        if (scene.isDirty)
        {
            message = $"Scene '{scene.name}' has unsaved changes. Save it before registering.";
            return false;
        }

        if (scene.name == MasterSceneName)
        {
            message = "The Master scene cannot be registered as a room.";
            return false;
        }

        string sceneGuid = AssetDatabase.AssetPathToGUID(scene.path);
        if (string.IsNullOrEmpty(sceneGuid))
        {
            message = $"Could not resolve the scene GUID for '{scene.path}'.";
            return false;
        }

        if (!TryGetRegistry(out RoomRegistry registry, out message))
            return false;

        foreach (RoomDefinition existing in registry.Rooms)
        {
            if (existing == null)
                continue;

            if (string.Equals(existing.SceneGuid, sceneGuid, StringComparison.Ordinal))
            {
                message = $"Scene '{scene.name}' is already registered as room '{existing.RoomId}'.";
                return false;
            }

            if (string.Equals(existing.SceneName, scene.name, StringComparison.Ordinal))
            {
                message = $"Another registered scene already uses the name '{scene.name}'.";
                return false;
            }
        }

        EnsureFolder(DefinitionsFolder);

        RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
        definition.AssignIdentity(
            "room_" + Guid.NewGuid().ToString("N"),
            scene.name,
            scene.name,
            sceneGuid);

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{DefinitionsFolder}/{SanitizeFileName(scene.name)}.asset");

        AssetDatabase.CreateAsset(definition, assetPath);

        if (!registry.Add(definition))
        {
            AssetDatabase.DeleteAsset(assetPath);
            message = $"Could not add room '{scene.name}' to the registry.";
            return false;
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        message = $"Registered room '{scene.name}' with ID '{definition.RoomId}' at '{assetPath}'.";
        return true;
    }

    public static bool TryGetRegistry(out RoomRegistry registry, out string message)
    {
        registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RegistryPath);

        if (registry != null)
        {
            message = null;
            return true;
        }

        EnsureFolder(RegistryFolder);

        registry = ScriptableObject.CreateInstance<RoomRegistry>();
        AssetDatabase.CreateAsset(registry, RegistryPath);
        AssetDatabase.SaveAssets();

        message = null;
        return true;
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
}
