using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class RuntimeRoomCatalogEntry
{
    [SerializeField] private RoomDefinition roomDefinition;
    [SerializeField] private string scenePath;

    public RoomDefinition RoomDefinition => roomDefinition;
    public string RoomId => roomDefinition != null ? roomDefinition.RoomId : string.Empty;
    public string ScenePath => scenePath;

    public RuntimeRoomCatalogEntry()
    {
    }

    public RuntimeRoomCatalogEntry(RoomDefinition definition, string path)
    {
        roomDefinition = definition;
        scenePath = path;
    }
}

/// <summary>
/// Runtime lookup from stable RoomId to its RoomDefinition and exact build scene path.
/// The generated asset lives under Resources so it is included in player builds.
/// </summary>
[CreateAssetMenu(fileName = "RuntimeRoomCatalog", menuName = "Project Spear/Rooms/Runtime Room Catalog")]
public sealed class RuntimeRoomCatalog : ScriptableObject
{
    public const string ResourcesLoadPath = "RoomConnectivity/RuntimeRoomCatalog";
    public const string AssetPath = "Assets/Resources/RoomConnectivity/RuntimeRoomCatalog.asset";

    [SerializeField] private List<RuntimeRoomCatalogEntry> entries = new List<RuntimeRoomCatalogEntry>();

    [NonSerialized] private Dictionary<string, RuntimeRoomCatalogEntry> _entriesByRoomId;
    [NonSerialized] private Dictionary<string, RuntimeRoomCatalogEntry> _entriesByScenePath;
    [NonSerialized] private bool _indexBuilt;
    [NonSerialized] private string _validationError;

    public IReadOnlyList<RuntimeRoomCatalogEntry> Entries => entries;
    public bool IsValid
    {
        get
        {
            EnsureIndex();
            return string.IsNullOrEmpty(_validationError);
        }
    }
    public string ValidationError
    {
        get
        {
            EnsureIndex();
            return _validationError;
        }
    }

    /// <summary>Loads the generated catalog included through the Resources folder.</summary>
    public static RuntimeRoomCatalog LoadDefault()
    {
        RuntimeRoomCatalog catalog = Resources.Load<RuntimeRoomCatalog>(ResourcesLoadPath);
        if (catalog == null)
        {
            throw new InvalidOperationException(
                $"RuntimeRoomCatalog was not found at Resources/{ResourcesLoadPath}. " +
                "Generate the catalog before building the player.");
        }

        if (!catalog.IsValid)
        {
            throw new InvalidOperationException(
                $"RuntimeRoomCatalog '{catalog.name}' is invalid: {catalog.ValidationError}");
        }

        return catalog;
    }

    public bool TryGetRoom(
        string roomId,
        out RoomDefinition definition,
        out string scenePath)
    {
        EnsureIndex();
        definition = null;
        scenePath = null;

        if (!string.IsNullOrEmpty(_validationError) || string.IsNullOrWhiteSpace(roomId))
            return false;

        if (!_entriesByRoomId.TryGetValue(roomId, out RuntimeRoomCatalogEntry entry))
            return false;

        definition = entry.RoomDefinition;
        scenePath = entry.ScenePath;
        return true;
    }

    /// <summary>
    /// Resolves an exact scene asset path to its registered RoomDefinition.
    /// Scene names and partial paths are not used for identity.
    /// </summary>
    public bool TryGetRoomByScenePath(string scenePath, out RoomDefinition definition)
    {
        EnsureIndex();
        definition = null;

        if (!string.IsNullOrEmpty(_validationError) || string.IsNullOrWhiteSpace(scenePath))
            return false;

        if (!_entriesByScenePath.TryGetValue(scenePath, out RuntimeRoomCatalogEntry entry))
            return false;

        definition = entry.RoomDefinition;
        return true;
    }

    public RoomDefinition GetRoomDefinition(string roomId)
    {
        return GetEntry(roomId).RoomDefinition;
    }

    public string GetScenePath(string roomId)
    {
        return GetEntry(roomId).ScenePath;
    }

    private RuntimeRoomCatalogEntry GetEntry(string roomId)
    {
        EnsureIndex();

        if (!string.IsNullOrEmpty(_validationError))
        {
            throw new InvalidOperationException(
                $"RuntimeRoomCatalog '{name}' is invalid: {_validationError}");
        }

        if (string.IsNullOrWhiteSpace(roomId) ||
            !_entriesByRoomId.TryGetValue(roomId, out RuntimeRoomCatalogEntry entry))
        {
            throw new KeyNotFoundException(
                $"RuntimeRoomCatalog '{name}' has no room with RoomId '{roomId ?? "<null>"}'.");
        }

        return entry;
    }

    private void EnsureIndex()
    {
        if (_indexBuilt)
            return;

        _entriesByRoomId = new Dictionary<string, RuntimeRoomCatalogEntry>(StringComparer.Ordinal);
        _entriesByScenePath = new Dictionary<string, RuntimeRoomCatalogEntry>(StringComparer.Ordinal);
        _validationError = null;

        if (entries == null || entries.Count == 0)
        {
            _validationError = "The catalog has no entries.";
            _indexBuilt = true;
            return;
        }

        HashSet<string> scenePaths = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < entries.Count; i++)
        {
            RuntimeRoomCatalogEntry entry = entries[i];
            if (entry == null)
            {
                SetValidationError($"Entry {i} is missing.");
                continue;
            }

            if (entry.RoomDefinition == null)
            {
                SetValidationError($"Entry {i} has no RoomDefinition reference.");
                continue;
            }

            string roomId = entry.RoomDefinition.RoomId;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                SetValidationError($"Entry {i} has an empty RoomId.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                SetValidationError($"Room '{roomId}' has an empty scene path.");
                continue;
            }

            if (!scenePaths.Add(entry.ScenePath))
            {
                SetValidationError($"Scene path '{entry.ScenePath}' is assigned more than once.");
            }
            else
            {
                _entriesByScenePath.Add(entry.ScenePath, entry);
            }

            if (_entriesByRoomId.ContainsKey(roomId))
            {
                SetValidationError($"RoomId '{roomId}' is assigned more than once.");
                continue;
            }

            _entriesByRoomId.Add(roomId, entry);
        }

        _indexBuilt = true;
    }

    private void SetValidationError(string error)
    {
        if (string.IsNullOrEmpty(_validationError))
            _validationError = error;
    }

    private void InvalidateIndex()
    {
        _entriesByRoomId = null;
        _entriesByScenePath = null;
        _validationError = null;
        _indexBuilt = false;
    }

    private void OnEnable()
    {
        InvalidateIndex();
    }

    private void OnValidate()
    {
        InvalidateIndex();
    }

#if UNITY_EDITOR
    public void SetGeneratedEntries(IList<RuntimeRoomCatalogEntry> generatedEntries)
    {
        entries = generatedEntries != null
            ? new List<RuntimeRoomCatalogEntry>(generatedEntries)
            : new List<RuntimeRoomCatalogEntry>();
        InvalidateIndex();
    }
#endif
}
