using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only persistence and layout helpers for the Room Connectivity Graph.
///
/// The service owns layout-asset loading, validation and persistence. The
/// Graph window owns user interaction and presentation. Positions are matched
/// exclusively by <see cref="RoomGraphNode.NodeKey"/>.
/// </summary>
public static class RoomConnectivityGraphLayoutService
{
    public const string DefaultAssetPath =
        "Assets/Data/Rooms/RoomConnectivityGraphLayout.asset";

    public const string AssetName = "RoomConnectivityGraphLayout";

    public const string MoveUndoName = "Move Room Graph Node";
    public const string AutoLayoutUndoName = "Auto Layout Room Graph";
    public const string ResetLayoutUndoName = "Reset Room Graph Layout";

    /// <summary>Pointer movement in panel pixels before a press becomes a drag.</summary>
    public const float DragThresholdPixels = 3f;

    public static RoomConnectivityGraphLayoutAsset Load()
    {
        return AssetDatabase.LoadAssetAtPath<RoomConnectivityGraphLayoutAsset>(
            DefaultAssetPath);
    }

    /// <summary>
    /// Loads the shared layout asset, creating it only when an explicit layout
    /// write needs it. Reading and displaying the automatic layout never
    /// creates or modifies project assets.
    /// </summary>
    public static RoomConnectivityGraphLayoutAsset Ensure()
    {
        RoomConnectivityGraphLayoutAsset asset = Load();

        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<RoomConnectivityGraphLayoutAsset>();
        asset.name = AssetName;
        AssetDatabase.CreateAsset(asset, DefaultAssetPath);

        if (AssetDatabase.Contains(asset))
            AssetDatabase.SaveAssetIfDirty(asset);

        return asset;
    }

    public static void RecordUndo(
        RoomConnectivityGraphLayoutAsset asset,
        string undoName)
    {
        if (asset != null)
            Undo.RecordObject(asset, undoName);
    }

    /// <summary>
    /// Marks and saves only the dedicated layout asset. Never calls a global
    /// AssetDatabase.SaveAssets().
    /// </summary>
    public static void Persist(RoomConnectivityGraphLayoutAsset asset)
    {
        if (asset == null)
            return;

        EditorUtility.SetDirty(asset);

        if (AssetDatabase.Contains(asset))
            AssetDatabase.SaveAssetIfDirty(asset);
    }

    /// <summary>
    /// Reads the stored positions into a NodeKey-keyed dictionary. The first
    /// entry for a duplicated key wins, making reads deterministic. Reads never
    /// mutate the asset, so stale or duplicate entries are never auto-cleaned.
    /// </summary>
    public static Dictionary<string, Vector2> BuildPositions(
        RoomConnectivityGraphLayoutAsset asset)
    {
        Dictionary<string, Vector2> positions =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);

        if (asset == null || asset.Entries == null)
            return positions;

        foreach (RoomGraphLayoutEntry entry in asset.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.NodeKey))
                continue;

            if (positions.ContainsKey(entry.NodeKey))
                continue;

            positions.Add(entry.NodeKey, entry.Position);
        }

        return positions;
    }

    public static bool TryGetPosition(
        RoomConnectivityGraphLayoutAsset asset,
        string nodeKey,
        out Vector2 position)
    {
        position = default;

        if (asset == null || string.IsNullOrWhiteSpace(nodeKey) || asset.Entries == null)
            return false;

        foreach (RoomGraphLayoutEntry entry in asset.Entries)
        {
            if (entry == null ||
                !string.Equals(entry.NodeKey, nodeKey, StringComparison.Ordinal))
            {
                continue;
            }

            position = entry.Position;
            return IsFinite(position);
        }

        return false;
    }

    /// <summary>
    /// Writes a single position. Duplicate entries for the same key are
    /// collapsed into the first matching entry. Called during dragging (after
    /// the single Undo snapshot has been recorded) and by explicit commands.
    /// </summary>
    public static bool SetPosition(
        RoomConnectivityGraphLayoutAsset asset,
        string nodeKey,
        Vector2 position)
    {
        if (asset == null || string.IsNullOrWhiteSpace(nodeKey) || !IsFinite(position))
            return false;

        List<RoomGraphLayoutEntry> entries = asset.MutableEntries;
        bool written = false;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            RoomGraphLayoutEntry entry = entries[i];

            if (entry == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            if (!string.Equals(entry.NodeKey, nodeKey, StringComparison.Ordinal))
                continue;

            if (!written)
            {
                entry.SetPosition(position);
                written = true;
            }
            else
            {
                entries.RemoveAt(i);
            }
        }

        if (!written)
            entries.Add(new RoomGraphLayoutEntry(nodeKey, position));

        return true;
    }

    public static List<string> FindStaleKeys(
        RoomConnectivityGraphLayoutAsset asset,
        IEnumerable<string> currentKeys)
    {
        List<string> stale = new List<string>();

        if (asset == null || asset.Entries == null)
            return stale;

        HashSet<string> current = new HashSet<string>(
            currentKeys ?? Array.Empty<string>(),
            StringComparer.Ordinal);

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (RoomGraphLayoutEntry entry in asset.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.NodeKey))
                continue;

            if (current.Contains(entry.NodeKey))
                continue;

            if (seen.Add(entry.NodeKey))
                stale.Add(entry.NodeKey);
        }

        stale.Sort(StringComparer.Ordinal);
        return stale;
    }

    public static List<string> FindDuplicateKeys(
        RoomConnectivityGraphLayoutAsset asset)
    {
        List<string> duplicates = new List<string>();

        if (asset == null || asset.Entries == null)
            return duplicates;

        Dictionary<string, int> counts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (RoomGraphLayoutEntry entry in asset.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.NodeKey))
                continue;

            counts.TryGetValue(entry.NodeKey, out int count);
            counts[entry.NodeKey] = count + 1;
        }

        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (pair.Value > 1)
                duplicates.Add(pair.Key);
        }

        duplicates.Sort(StringComparer.Ordinal);
        return duplicates;
    }

    public static int CountInvalidEntries(RoomConnectivityGraphLayoutAsset asset)
    {
        if (asset == null || asset.Entries == null)
            return 0;

        int invalid = 0;

        foreach (RoomGraphLayoutEntry entry in asset.Entries)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.NodeKey) ||
                !IsFinite(entry.Position))
            {
                invalid++;
            }
        }

        return invalid;
    }

    /// <summary>
    /// Deterministic automatic grid position used whenever a node has no saved
    /// position. Independent of graph-model identity.
    /// </summary>
    public static Dictionary<string, Vector2> ComputeDeterministicPositions(
        RoomConnectivityGraphModel model)
    {
        Dictionary<string, Vector2> positions =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);

        List<RoomGraphNode> nodes = RoomConnectivityGraphLayout.SortNodesForLayout(model);

        for (int i = 0; i < nodes.Count; i++)
        {
            RoomGraphNode node = nodes[i];

            if (node == null || string.IsNullOrWhiteSpace(node.NodeKey))
                continue;

            if (positions.ContainsKey(node.NodeKey))
                continue;

            positions.Add(node.NodeKey, RoomConnectivityGraphLayout.GetGridPosition(i, nodes.Count));
        }

        return positions;
    }

    /// <summary>
    /// Explicit Auto Layout: writes deterministic positions for every current
    /// room as one Undo operation, then saves only the layout asset.
    /// </summary>
    public static bool AutoLayout(
        RoomConnectivityGraphLayoutAsset asset,
        RoomConnectivityGraphModel model)
    {
        if (asset == null || model == null || model.Nodes.Count == 0)
            return false;

        Dictionary<string, Vector2> positions = ComputeDeterministicPositions(model);

        if (positions.Count == 0)
            return false;

        Undo.RecordObject(asset, AutoLayoutUndoName);
        asset.ReplaceAll(positions);
        Persist(asset);
        return true;
    }

    /// <summary>
    /// Explicit Reset Layout: clears every saved override so the deterministic
    /// automatic layout is used again. One Undo operation; saves only the
    /// layout asset.
    /// </summary>
    public static bool ResetLayout(RoomConnectivityGraphLayoutAsset asset)
    {
        if (asset == null || asset.EntryCount == 0)
            return false;

        Undo.RecordObject(asset, ResetLayoutUndoName);
        asset.ClearEntries();
        Persist(asset);
        return true;
    }

    public static Vector2 ClampToGraph(Vector2 position)
    {
        return new Vector2(Mathf.Max(0f, position.x), Mathf.Max(0f, position.y));
    }

    /// <summary>
    /// Converts panel-space pointer movement to graph-space movement. The Graph
    /// content is scaled by the current zoom, so pointer movement must be
    /// divided by that zoom to keep node movement consistent at every zoom
    /// level.
    /// </summary>
    public static Vector2 PanelDeltaToGraphDelta(Vector2 panelDelta, float zoom)
    {
        if (zoom <= 0.0001f)
            return panelDelta;

        return panelDelta / zoom;
    }

    public static bool ExceedsDragThreshold(Vector2 accumulatedPanelDelta)
    {
        return accumulatedPanelDelta.sqrMagnitude >=
               DragThresholdPixels * DragThresholdPixels;
    }

    public static bool PositionsEqual(
        IReadOnlyDictionary<string, Vector2> a,
        IReadOnlyDictionary<string, Vector2> b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a == null || b == null || a.Count != b.Count)
            return false;

        foreach (KeyValuePair<string, Vector2> pair in a)
        {
            if (!b.TryGetValue(pair.Key, out Vector2 other))
                return false;

            if (pair.Value != other)
                return false;
        }

        return true;
    }

    public static bool IsFinite(Vector2 position)
    {
        return IsFinite(position.x) && IsFinite(position.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
