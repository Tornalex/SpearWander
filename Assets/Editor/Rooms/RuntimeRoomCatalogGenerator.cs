using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class RuntimeRoomCatalogValidationIssue
{
    public RuntimeRoomCatalogValidationIssue(string code, string message, UnityEngine.Object context = null)
    {
        Code = code;
        Message = message;
        Context = context;
    }

    public string Code { get; }
    public string Message { get; }
    public UnityEngine.Object Context { get; }
}

public sealed class RuntimeRoomCatalogValidationReport
{
    private readonly List<RuntimeRoomCatalogValidationIssue> _issues =
        new List<RuntimeRoomCatalogValidationIssue>();

    public IReadOnlyList<RuntimeRoomCatalogValidationIssue> Issues => _issues;
    public int ErrorCount => _issues.Count;
    public bool IsValid => ErrorCount == 0;
    public int RoomCount { get; internal set; }

    internal void AddError(string code, string message, UnityEngine.Object context = null)
    {
        _issues.Add(new RuntimeRoomCatalogValidationIssue(code, message, context));
    }

    public string FormatSummary()
    {
        return $"Runtime Room Catalog: {RoomCount} room(s), {ErrorCount} error(s).";
    }
}

/// <summary>
/// Generates the Resources-backed runtime catalog exclusively from RoomRegistry.
/// Validation is read-only; asset creation or updating occurs only from the
/// explicit Generate/Regenerate menu command.
/// </summary>
public static class RuntimeRoomCatalogGenerator
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string CatalogFolder = "Assets/Resources/RoomConnectivity";

    [MenuItem("Tools/Project Spear/Rooms/Generate Runtime Room Catalog")]
    public static void GenerateMenu()
    {
        RuntimeRoomCatalogValidationReport report = GenerateOrUpdate();
        LogReport(report, "Generate Runtime Room Catalog");

        if (report.IsValid)
        {
            EditorUtility.DisplayDialog(
                "Runtime Room Catalog",
                report.FormatSummary() + $"\nGenerated: {RuntimeRoomCatalog.AssetPath}",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Runtime Room Catalog",
                report.FormatSummary() + "\nSee the Console for details. The catalog was not updated.",
                "OK");
        }
    }

    [MenuItem("Tools/Project Spear/Rooms/Validate Runtime Room Catalog")]
    public static void ValidateMenu()
    {
        RuntimeRoomCatalogValidationReport report = ValidateForBuild();
        LogReport(report, "Validate Runtime Room Catalog");
    }

    /// <summary>
    /// Deterministic, read-only validation and generation preview from the
    /// project's RoomRegistry. No catalog or source asset is modified.
    /// </summary>
    public static RuntimeRoomCatalogValidationReport ValidateSource(
        out List<RuntimeRoomCatalogEntry> generatedEntries)
    {
        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        return ValidateSource(
            registry,
            out generatedEntries,
            AssetDatabase.GUIDToAssetPath,
            AssetDatabase.AssetPathToGUID,
            path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null,
            definition => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(definition)),
            IsEnabledInClassicBuildSettings);
    }

    /// <summary>
    /// Generates or updates only RuntimeRoomCatalog.AssetPath. Registry and
    /// RoomDefinition assets are read-only inputs. Invalid source data leaves
    /// the existing catalog untouched.
    /// </summary>
    public static RuntimeRoomCatalogValidationReport GenerateOrUpdate()
    {
        RuntimeRoomCatalogValidationReport report = ValidateSource(out List<RuntimeRoomCatalogEntry> entries);
        if (!report.IsValid)
            return report;

        EnsureFolder(ResourcesFolder);
        EnsureFolder(CatalogFolder);

        RuntimeRoomCatalog catalog = AssetDatabase.LoadAssetAtPath<RuntimeRoomCatalog>(RuntimeRoomCatalog.AssetPath);
        if (catalog == null && File.Exists(RuntimeRoomCatalog.AssetPath))
        {
            report.AddError(
                "RCAT001",
                $"An asset exists at '{RuntimeRoomCatalog.AssetPath}', but it is not a RuntimeRoomCatalog.");
            return report;
        }

        bool isNewAsset = catalog == null;
        if (isNewAsset)
            catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();

        try
        {
            catalog.SetGeneratedEntries(entries);

            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(catalog, RuntimeRoomCatalog.AssetPath);
            }
            else
            {
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
        }
        catch (Exception exception)
        {
            if (isNewAsset && catalog != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(catalog)))
                UnityEngine.Object.DestroyImmediate(catalog);

            report.AddError("RCAT002", "Could not write the runtime catalog: " + exception.Message);
            return report;
        }

        report.RoomCount = entries.Count;
        return report;
    }

    /// <summary>
    /// Read-only validation suitable for a future pre-build hook. It compares
    /// the generated asset to the current RoomRegistry without changing either.
    /// </summary>
    public static RuntimeRoomCatalogValidationReport ValidateForBuild()
    {
        RuntimeRoomCatalogValidationReport report = ValidateSource(out List<RuntimeRoomCatalogEntry> expected);
        if (!report.IsValid)
            return report;

        RuntimeRoomCatalog catalog = AssetDatabase.LoadAssetAtPath<RuntimeRoomCatalog>(RuntimeRoomCatalog.AssetPath);
        if (catalog == null)
        {
            report.AddError(
                "RCAT003",
                $"Generated catalog is missing at '{RuntimeRoomCatalog.AssetPath}'. Run Generate Runtime Room Catalog.");
            return report;
        }

        if (!catalog.IsValid)
        {
            report.AddError("RCAT004", $"Generated catalog is structurally invalid: {catalog.ValidationError}", catalog);
            return report;
        }

        IReadOnlyList<RuntimeRoomCatalogEntry> actual = catalog.Entries;
        if (actual == null || actual.Count != expected.Count)
        {
            report.AddError(
                "RCAT005",
                $"Catalog entry count does not match RoomRegistry ({(actual != null ? actual.Count : 0)} vs {expected.Count}).",
                catalog);
            return report;
        }

        for (int i = 0; i < expected.Count; i++)
        {
            RuntimeRoomCatalogEntry expectedEntry = expected[i];
            RuntimeRoomCatalogEntry actualEntry = actual[i];

            if (actualEntry == null ||
                actualEntry.RoomDefinition != expectedEntry.RoomDefinition ||
                !string.Equals(actualEntry.RoomId, expectedEntry.RoomId, StringComparison.Ordinal) ||
                !string.Equals(actualEntry.ScenePath, expectedEntry.ScenePath, StringComparison.Ordinal))
            {
                report.AddError(
                    "RCAT006",
                    $"Catalog entry {i} is stale or differs from RoomRegistry. Expected " +
                    $"'{expectedEntry.RoomId}' at '{expectedEntry.ScenePath}'.",
                    catalog);
            }
        }

        report.RoomCount = expected.Count;
        return report;
    }

    internal static RuntimeRoomCatalogValidationReport ValidateSource(
        RoomRegistry registry,
        out List<RuntimeRoomCatalogEntry> generatedEntries,
        Func<string, string> guidToPath,
        Func<string, string> pathToGuid,
        Func<string, bool> isSceneAsset,
        Func<RoomDefinition, bool> isPersistentDefinition,
        Func<string, bool> isIncludedInBuild)
    {
        RuntimeRoomCatalogValidationReport report = new RuntimeRoomCatalogValidationReport();
        generatedEntries = new List<RuntimeRoomCatalogEntry>();

        if (registry == null)
        {
            report.AddError(
                "RCAT007",
                $"RoomRegistry is missing at '{RoomRegistrationService.RegistryPath}'.");
            return report;
        }

        IReadOnlyList<RoomDefinition> rooms = registry.Rooms;
        if (rooms == null)
        {
            report.AddError("RCAT008", "RoomRegistry has no room list.", registry);
            return report;
        }

        if (rooms.Count == 0)
        {
            report.AddError("RCAT009", "RoomRegistry contains no registered rooms.", registry);
            return report;
        }

        HashSet<string> roomIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> sceneGuids = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> scenePaths = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomDefinition definition = rooms[i];
            if (definition == null)
            {
                report.AddError("RCAT010", $"RoomRegistry entry {i} is a missing RoomDefinition reference.", registry);
                continue;
            }

            string roomId = definition.RoomId;
            bool roomIdValid = !string.IsNullOrWhiteSpace(roomId);
            if (!roomIdValid)
            {
                report.AddError("RCAT011", $"RoomDefinition '{definition.name}' has an empty RoomId.", definition);
            }
            else if (!roomIds.Add(roomId))
            {
                report.AddError("RCAT012", $"RoomId '{roomId}' is assigned more than once.", definition);
                roomIdValid = false;
            }

            if (isPersistentDefinition == null || !isPersistentDefinition(definition))
            {
                report.AddError(
                    "RCAT013",
                    $"RoomDefinition '{definition.name}' is not a persistent asset and cannot be included in the catalog.",
                    definition);
            }

            string sceneGuid = definition.SceneGuid;
            if (string.IsNullOrWhiteSpace(sceneGuid))
            {
                report.AddError("RCAT014", $"Room '{(roomIdValid ? roomId : definition.name)}' has no scene GUID.", definition);
                continue;
            }

            if (!sceneGuids.Add(sceneGuid))
                report.AddError("RCAT015", $"Scene GUID '{sceneGuid}' is assigned to more than one room.", definition);

            string scenePath = guidToPath != null ? guidToPath(sceneGuid) : null;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                report.AddError(
                    "RCAT016",
                    $"Scene GUID '{sceneGuid}' for room '{(roomIdValid ? roomId : definition.name)}' does not resolve to an asset path.",
                    definition);
                continue;
            }

            string resolvedGuid = pathToGuid != null ? pathToGuid(scenePath) : null;
            if (!string.Equals(resolvedGuid, sceneGuid, StringComparison.Ordinal))
            {
                report.AddError(
                    "RCAT017",
                    $"Resolved path '{scenePath}' maps to GUID '{resolvedGuid}', not the registered GUID '{sceneGuid}'.",
                    definition);
                continue;
            }

            if (isSceneAsset == null || !isSceneAsset(scenePath))
            {
                report.AddError("RCAT018", $"Resolved asset '{scenePath}' is not a Unity SceneAsset.", definition);
                continue;
            }

            if (!scenePaths.Add(scenePath))
                report.AddError("RCAT019", $"Scene path '{scenePath}' is assigned to more than one room.", definition);

            if (isIncludedInBuild == null || !isIncludedInBuild(scenePath))
            {
                report.AddError(
                    "RCAT020",
                    $"Registered room '{(roomIdValid ? roomId : definition.name)}' scene '{scenePath}' is not enabled in the checked-in EditorBuildSettings scene list.",
                    definition);
            }

            if (roomIdValid)
                generatedEntries.Add(new RuntimeRoomCatalogEntry(definition, scenePath));
        }

        generatedEntries.Sort((left, right) =>
            string.Compare(left.RoomId, right.RoomId, StringComparison.Ordinal));
        report.RoomCount = generatedEntries.Count;

        if (!report.IsValid)
            generatedEntries.Clear();

        return report;
    }

    private static bool IsEnabledInClassicBuildSettings(string scenePath)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                return true;
        }

        return false;
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

    private static void LogReport(RuntimeRoomCatalogValidationReport report, string operation)
    {
        foreach (RuntimeRoomCatalogValidationIssue issue in report.Issues)
        {
            Debug.LogError($"[{operation}] {issue.Code}: {issue.Message}", issue.Context);
        }

        if (report.IsValid)
            Debug.Log($"[{operation}] {report.FormatSummary()}");
        else
            Debug.LogError($"[{operation}] {report.FormatSummary()}");
    }
}
