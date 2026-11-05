using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RoomGraphEdgeGeometry
{
    public RoomGraphEdgeGeometry(RoomGraphEdge edge)
    {
        Edge = edge;
    }

    public RoomGraphEdge Edge { get; }
    public Vector2 Start { get; internal set; }
    public Vector2 Control { get; internal set; }
    public Vector2 End { get; internal set; }
    public Vector2 Midpoint { get; internal set; }
    public bool ArrowAtStart { get; internal set; }
    public bool ArrowAtEnd { get; internal set; }

    public Vector2 Evaluate(float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * Start +
               2f * inverse * t * Control +
               t * t * End;
    }
}

/// <summary>
/// Deterministic visual layout. It never reads or writes scene transforms,
/// coordinates or any connectivity data beyond the supplied graph model.
/// </summary>
public static class RoomConnectivityGraphLayout
{
    public const float NodeWidth = 230f;
    public const float NodeHeight = 150f;
    public const float HorizontalGap = 110f;
    public const float VerticalGap = 100f;
    public const float Margin = 60f;
    public const float MultiEdgeSpacing = 48f;

    public static RoomConnectivityGraphLayoutResult Compute(RoomConnectivityGraphModel model)
    {
        return Compute(model, null);
    }

    /// <summary>
    /// Deterministic layout with optional manual overrides. Nodes with a
    /// saved position keyed by <see cref="RoomGraphNode.NodeKey"/> use that
    /// position; every other node falls back to its deterministic grid cell.
    /// Reading the layout never modifies the supplied position data.
    /// </summary>
    public static RoomConnectivityGraphLayoutResult Compute(
        RoomConnectivityGraphModel model,
        IReadOnlyDictionary<string, Vector2> savedPositions)
    {
        RoomConnectivityGraphLayoutResult result = new RoomConnectivityGraphLayoutResult();

        if (model == null || model.Nodes.Count == 0)
        {
            result.ContentSize = new Vector2(800f, 600f);
            return result;
        }

        List<RoomGraphNode> sorted = SortNodesForLayout(model);

        float maxX = 0f;
        float maxY = 0f;

        for (int i = 0; i < sorted.Count; i++)
        {
            Vector2 position = GetGridPosition(i, sorted.Count);

            if (savedPositions != null &&
                !string.IsNullOrEmpty(sorted[i].NodeKey) &&
                savedPositions.TryGetValue(sorted[i].NodeKey, out Vector2 saved) &&
                IsFinite(saved))
            {
                // Saved positions are written non-negative by the editor. A
                // corrupted or hand-edited negative position is clamped for
                // display without modifying the asset.
                position = new Vector2(Mathf.Max(0f, saved.x), Mathf.Max(0f, saved.y));
            }

            Rect rect = new Rect(position.x, position.y, NodeWidth, NodeHeight);
            result.NodeRects[sorted[i]] = rect;

            maxX = Mathf.Max(maxX, rect.xMax);
            maxY = Mathf.Max(maxY, rect.yMax);
        }

        int contentWidth = Mathf.CeilToInt(maxX + Margin);
        int contentHeight = Mathf.CeilToInt(maxY + Margin);

        result.ContentSize = new Vector2(
            Mathf.Max(800f, contentWidth),
            Mathf.Max(600f, contentHeight));

        Dictionary<string, List<RoomGraphEdge>> groups =
            new Dictionary<string, List<RoomGraphEdge>>(StringComparer.Ordinal);

        foreach (RoomGraphEdge edge in model.Edges)
        {
            if (edge.SourceNode == null || edge.TargetNode == null ||
                ReferenceEquals(edge.SourceNode, edge.TargetNode))
            {
                continue;
            }

            if (!result.NodeRects.ContainsKey(edge.SourceNode) ||
                !result.NodeRects.ContainsKey(edge.TargetNode))
            {
                continue;
            }

            string key = PairKey(edge.SourceNode, edge.TargetNode);

            if (!groups.TryGetValue(key, out List<RoomGraphEdge> list))
            {
                list = new List<RoomGraphEdge>();
                groups.Add(key, list);
            }

            list.Add(edge);
        }

        foreach (KeyValuePair<string, List<RoomGraphEdge>> pair in groups)
        {
            pair.Value.Sort((a, b) => string.CompareOrdinal(a.AssetPath, b.AssetPath));

            int count = pair.Value.Count;

            for (int i = 0; i < count; i++)
            {
                RoomGraphEdge edge = pair.Value[i];
                Rect sourceRect = result.NodeRects[edge.SourceNode];
                Rect targetRect = result.NodeRects[edge.TargetNode];

                Vector2 sourceCenter = sourceRect.center;
                Vector2 targetCenter = targetRect.center;

                RoomGraphEdgeGeometry geometry = new RoomGraphEdgeGeometry(edge)
                {
                    Start = BoundaryPoint(sourceRect, targetCenter),
                    End = BoundaryPoint(targetRect, sourceCenter)
                };

                Vector2 direction = targetCenter - sourceCenter;

                if (direction.sqrMagnitude < 0.0001f)
                    direction = Vector2.right;

                Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
                float offset = (i - (count - 1) * 0.5f) * MultiEdgeSpacing;

                geometry.Control = (geometry.Start + geometry.End) * 0.5f + normal * offset;
                geometry.Midpoint = geometry.Evaluate(0.5f);

                if (!edge.HasDirectionError)
                {
                    geometry.ArrowAtEnd = true;
                    geometry.ArrowAtStart = edge.Direction == RoomConnectionDirection.Bidirectional;
                }

                result.EdgeGeometries[edge.Connection] = geometry;
            }
        }

        return result;
    }

    public static Vector2 Evaluate(RoomGraphEdgeGeometry geometry, float t)
    {
        return geometry.Evaluate(t);
    }

    /// <summary>
    /// Stable node ordering used by the automatic grid. The ordering is based
    /// only on the node's own display data and stable key, never on model
    /// instance identity or list order.
    /// </summary>
    public static List<RoomGraphNode> SortNodesForLayout(
        RoomConnectivityGraphModel model)
    {
        List<RoomGraphNode> sorted = new List<RoomGraphNode>();

        if (model == null)
            return sorted;

        sorted.AddRange(model.Nodes);
        sorted.Sort(CompareNodes);
        return sorted;
    }

    /// <summary>
    /// Deterministic grid cell for a node index, using the same spacing as the
    /// original automatic layout.
    /// </summary>
    public static Vector2 GetGridPosition(int index, int count)
    {
        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))));
        int column = Mathf.Max(0, index) % columns;
        int row = Mathf.Max(0, index) / columns;

        return new Vector2(
            Margin + column * (NodeWidth + HorizontalGap),
            Margin + row * (NodeHeight + VerticalGap));
    }

    private static bool IsFinite(Vector2 position)
    {
        return !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
               !float.IsNaN(position.y) && !float.IsInfinity(position.y);
    }

    private static int CompareNodes(RoomGraphNode a, RoomGraphNode b)
    {
        int display = string.CompareOrdinal(a.DisplayName, b.DisplayName);

        if (display != 0)
            return display;

        int roomId = string.CompareOrdinal(a.RoomId, b.RoomId);

        if (roomId != 0)
            return roomId;

        return string.CompareOrdinal(a.NodeKey, b.NodeKey);
    }

    private static string PairKey(RoomGraphNode a, RoomGraphNode b)
    {
        string first = a.NodeKey;
        string second = b.NodeKey;

        return string.CompareOrdinal(first, second) <= 0
            ? first + "\u001e" + second
            : second + "\u001e" + first;
    }

    private static Vector2 BoundaryPoint(Rect rect, Vector2 toward)
    {
        Vector2 center = rect.center;
        Vector2 direction = toward - center;

        if (direction.sqrMagnitude < 0.0001f)
            return center;

        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;

        float scaleX = Mathf.Abs(direction.x) < 0.0001f
            ? float.PositiveInfinity
            : halfWidth / Mathf.Abs(direction.x);

        float scaleY = Mathf.Abs(direction.y) < 0.0001f
            ? float.PositiveInfinity
            : halfHeight / Mathf.Abs(direction.y);

        float scale = Mathf.Min(scaleX, scaleY);
        return center + direction * scale;
    }
}

public sealed class RoomConnectivityGraphLayoutResult
{
    public Dictionary<RoomGraphNode, Rect> NodeRects { get; } =
        new Dictionary<RoomGraphNode, Rect>();

    public Dictionary<RoomConnection, RoomGraphEdgeGeometry> EdgeGeometries { get; } =
        new Dictionary<RoomConnection, RoomGraphEdgeGeometry>();

    public Vector2 ContentSize { get; internal set; }
}
