using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable layout entry. The node key is the only identity used to match
/// a stored position to a graph node. It never contains a display name, scene
/// name, array index or graph-model instance identity.
/// </summary>
[Serializable]
public sealed class RoomGraphLayoutEntry
{
    [SerializeField] private string nodeKey;
    [SerializeField] private float x;
    [SerializeField] private float y;

    public RoomGraphLayoutEntry()
    {
    }

    public RoomGraphLayoutEntry(string nodeKey, Vector2 position)
    {
        this.nodeKey = nodeKey;
        x = position.x;
        y = position.y;
    }

    public string NodeKey => nodeKey;
    public Vector2 Position => new Vector2(x, y);

    public void SetPosition(Vector2 position)
    {
        x = position.x;
        y = position.y;
    }
}

/// <summary>
/// Editor-only, Git-tracked storage for manual Room Connectivity Graph node
/// positions. This asset is intentionally independent of RoomDefinition,
/// RoomRegistry and RoomConnection assets. Positions are matched exclusively
/// through <see cref="RoomGraphNode.NodeKey"/>.
///
/// This type lives in the Editor assembly and must never be used by runtime
/// gameplay code.
/// </summary>
public sealed class RoomConnectivityGraphLayoutAsset : ScriptableObject
{
    [SerializeField] private List<RoomGraphLayoutEntry> entries = new List<RoomGraphLayoutEntry>();

    public IReadOnlyList<RoomGraphLayoutEntry> Entries => entries;

    public int EntryCount => entries != null ? entries.Count : 0;

    /// <summary>
    /// Mutable access for the editor-only layout service. The list is kept in a
    /// deterministic order whenever it is rewritten.
    /// </summary>
    internal List<RoomGraphLayoutEntry> MutableEntries
    {
        get
        {
            if (entries == null)
                entries = new List<RoomGraphLayoutEntry>();

            return entries;
        }
    }

    public void ClearEntries()
    {
        MutableEntries.Clear();
    }

    /// <summary>
    /// Replaces every stored position with the supplied deterministic set.
    /// Keys are sorted so the serialized asset stays diff-friendly in Git.
    /// </summary>
    public void ReplaceAll(IReadOnlyDictionary<string, Vector2> positions)
    {
        List<RoomGraphLayoutEntry> next = new List<RoomGraphLayoutEntry>();

        if (positions != null)
        {
            List<string> keys = new List<string>(positions.Keys);
            keys.Sort(StringComparer.Ordinal);

            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                next.Add(new RoomGraphLayoutEntry(key, positions[key]));
            }
        }

        List<RoomGraphLayoutEntry> current = MutableEntries;
        current.Clear();
        current.AddRange(next);
    }
}
