using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class RoomConnectivityGraphWindow : EditorWindow
{
    private const float EdgeHitThreshold = 12f;

    private RoomConnectivityGraphModel _model;
    private RoomConnectivityGraphLayoutResult _layout;

    private const int MaxSearchResultsShown = 150;

    private RoomConnectivityGraphLayoutAsset _layoutAsset;
    private readonly Dictionary<string, Vector2> _layoutPositions =
        new Dictionary<string, Vector2>(StringComparer.Ordinal);
    private Label _layoutDiagnosticLabel;

    private RoomGraphNode _dragNode;
    private VisualElement _dragElement;
    private int _dragPointerId = -1;
    private bool _dragStarted;
    private bool _dragFinishing;
    private Vector2 _dragAccumulatedPanelDelta;
    private Vector2 _dragStartPosition;
    private Vector2 _dragPosition;
    private bool _dragHadSavedPosition;

    private RoomGraphNode _selectedNode;
    private RoomGraphEdge _selectedEdge;
    private RoomGraphDoor _selectedDoor;
    private bool _showUnresolvedPanel;

    private VisualElement _viewport;
    private VisualElement _content;
    private VisualElement _edgeLayer;
    private ScrollView _sidePanel;
    private Label _summaryLabel;
    private Label _zoomLabel;
    private Label _visibilityLabel;
    private Button _unresolvedButton;

    private TextField _searchField;
    private VisualElement _searchResultsContainer;
    private ScrollView _searchResultsList;
    private Label _searchResultsSummary;
    private Toggle _allRoomsToggle;
    private Toggle _connectedToggle;
    private Toggle _unconnectedDoorsToggle;
    private Toggle _errorsToggle;
    private Toggle _warningsToggle;

    private RoomGraphRoomFilter _filters = RoomGraphRoomFilter.AllRooms;
    private string _searchQuery = string.Empty;
    private bool _syncingFilterToggles;
    private readonly List<RoomGraphSearchResult> _searchResults = new List<RoomGraphSearchResult>();
    private readonly HashSet<RoomGraphNode> _searchRevealedNodes = new HashSet<RoomGraphNode>();
    private HashSet<RoomGraphNode> _visibleNodes = new HashSet<RoomGraphNode>();

    private readonly Dictionary<RoomGraphNode, VisualElement> _nodeElements =
        new Dictionary<RoomGraphNode, VisualElement>();
    private readonly Dictionary<RoomGraphEdge, VisualElement> _edgeLabelElements =
        new Dictionary<RoomGraphEdge, VisualElement>();

    private Vector2 _pan = new Vector2(20f, 20f);
    private float _zoom = 1f;
    private bool _panning;
    private int _panPointerId = -1;
    private bool _initialFitDone;
    private int _fitAttempts;

    [MenuItem("Tools/Project Spear/Rooms/Open Room Connectivity Graph")]
    public static void Open()
    {
        RoomConnectivityGraphWindow window = GetWindow<RoomConnectivityGraphWindow>();
        window.titleContent = new GUIContent("Room Connectivity");
        window.minSize = new Vector2(940f, 560f);
        window.Show();
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1;
        root.style.flexDirection = FlexDirection.Column;
        root.style.backgroundColor = new Color(0.13f, 0.13f, 0.14f);

        BuildToolbar(root);
        BuildBody(root);

        ShowEmptyState();
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }

    private void ShowEmptyState()
    {
        _model = null;
        _layout = null;
        _selectedNode = null;
        _selectedEdge = null;
        _selectedDoor = null;
        _showUnresolvedPanel = false;
        _initialFitDone = false;
        _fitAttempts = 0;
        _nodeElements.Clear();
        _edgeLabelElements.Clear();
        _searchResults.Clear();
        _searchRevealedNodes.Clear();
        _visibleNodes.Clear();
        _searchQuery = string.Empty;
        _layoutPositions.Clear();
        ResetDragState();

        if (_searchField != null)
            _searchField.SetValueWithoutNotify(string.Empty);

        HideSearchResults();
        SyncFilterToggles();

        if (_summaryLabel != null)
            _summaryLabel.text = "Not loaded — click Refresh";

        UpdateLayoutDiagnostics();
        UpdateToolbar();
        UpdateDetails();
    }

    private void BuildToolbar(VisualElement root)
    {
        VisualElement toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.alignItems = Align.Center;
        toolbar.style.paddingLeft = 8;
        toolbar.style.paddingRight = 8;
        toolbar.style.paddingTop = 6;
        toolbar.style.paddingBottom = 6;
        toolbar.style.borderBottomWidth = 1;
        toolbar.style.borderBottomColor = new Color(0f, 0f, 0f, 0.5f);
        toolbar.style.backgroundColor = new Color(0.17f, 0.17f, 0.18f);

        toolbar.Add(MakeButton("Refresh", RefreshModel));
        toolbar.Add(MakeButton("Fit Graph", FitGraph));

        Button autoLayoutButton = MakeButton("Auto Layout", AutoLayoutGraph);
        autoLayoutButton.tooltip =
            "Recalculate deterministic positions for every room and save them as explicit layout entries. " +
            "Single Undo step. Never runs automatically.";
        toolbar.Add(autoLayoutButton);

        Button resetLayoutButton = MakeButton("Reset Layout", ResetLayoutGraph);
        resetLayoutButton.tooltip =
            "Clear all saved manual positions and return to the deterministic automatic layout. " +
            "Single Undo step.";
        toolbar.Add(resetLayoutButton);

        toolbar.Add(MakeButton("Validate", SelectValidationIssues));
        _unresolvedButton = MakeButton("Unresolved (0)", ShowUnresolvedPanel);
        toolbar.Add(_unresolvedButton);

        _layoutDiagnosticLabel = new Label();
        _layoutDiagnosticLabel.style.marginLeft = 10;
        _layoutDiagnosticLabel.style.marginRight = 8;
        _layoutDiagnosticLabel.style.fontSize = 10;
        _layoutDiagnosticLabel.style.flexShrink = 0;
        _layoutDiagnosticLabel.style.color = new Color(0.68f, 0.72f, 0.76f);
        toolbar.Add(_layoutDiagnosticLabel);

        _summaryLabel = new Label();
        _summaryLabel.style.marginLeft = 12;
        _summaryLabel.style.flexGrow = 1;
        _summaryLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
        toolbar.Add(_summaryLabel);

        _zoomLabel = new Label("100%");
        _zoomLabel.style.width = 52;
        _zoomLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        _zoomLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
        toolbar.Add(_zoomLabel);

        root.Add(toolbar);

        BuildNavigationBar(root);
        BuildSearchResults(root);
    }

    private void BuildNavigationBar(VisualElement root)
    {
        VisualElement navigation = new VisualElement();
        navigation.style.flexDirection = FlexDirection.Row;
        navigation.style.alignItems = Align.Center;
        navigation.style.flexWrap = Wrap.Wrap;
        navigation.style.paddingLeft = 8;
        navigation.style.paddingRight = 8;
        navigation.style.paddingTop = 4;
        navigation.style.paddingBottom = 4;
        navigation.style.borderBottomWidth = 1;
        navigation.style.borderBottomColor = new Color(0f, 0f, 0f, 0.5f);
        navigation.style.backgroundColor = new Color(0.155f, 0.155f, 0.165f);

        Label searchLabel = new Label("Search");
        searchLabel.style.marginRight = 6;
        searchLabel.style.color = new Color(0.82f, 0.82f, 0.84f);
        navigation.Add(searchLabel);

        _searchField = new TextField();
        _searchField.style.flexGrow = 1;
        _searchField.style.minWidth = 180;
        _searchField.style.marginRight = 6;
        _searchField.tooltip =
            "Search rooms (display name, scene, room ID), doors (door ID, GameObject name, legacy door ID) " +
            "and connections (asset name or path). Case-insensitive partial matches.";
        _searchField.RegisterValueChangedCallback(evt => OnSearchChanged(evt.newValue));
        _searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);
        navigation.Add(_searchField);

        navigation.Add(MakeButton("Clear", ClearSearch));

        _visibilityLabel = new Label();
        _visibilityLabel.style.marginLeft = 8;
        _visibilityLabel.style.marginRight = 8;
        _visibilityLabel.style.color = new Color(0.72f, 0.74f, 0.78f);
        navigation.Add(_visibilityLabel);

        _allRoomsToggle = MakeFilterToggle("All Rooms", RoomGraphRoomFilter.AllRooms, navigation);
        _connectedToggle = MakeFilterToggle("Connected", RoomGraphRoomFilter.ConnectedRooms, navigation);
        _unconnectedDoorsToggle = MakeFilterToggle(
            "Unconnected Doors",
            RoomGraphRoomFilter.RoomsWithUnconnectedDoors,
            navigation);
        _errorsToggle = MakeFilterToggle("Errors", RoomGraphRoomFilter.RoomsWithErrors, navigation);
        _warningsToggle = MakeFilterToggle("Warnings", RoomGraphRoomFilter.RoomsWithWarnings, navigation);

        root.Add(navigation);
        SyncFilterToggles();
    }

    private Toggle MakeFilterToggle(
        string label,
        RoomGraphRoomFilter filter,
        VisualElement parent)
    {
        Toggle toggle = new Toggle(label);
        toggle.style.marginRight = 10;
        toggle.style.marginTop = 0;
        toggle.style.marginBottom = 0;
        toggle.style.fontSize = 11;
        toggle.tooltip = FilterTooltip(filter);
        toggle.RegisterValueChangedCallback(evt => OnFilterToggleChanged(filter, evt.newValue));
        parent.Add(toggle);
        return toggle;
    }

    private static string FilterTooltip(RoomGraphRoomFilter filter)
    {
        switch (filter)
        {
            case RoomGraphRoomFilter.ConnectedRooms:
                return "Show rooms with at least one door referenced by a valid topology connection " +
                       "and no invalid incident connection. Scene backlinks are not used to infer connectivity.";

            case RoomGraphRoomFilter.RoomsWithUnconnectedDoors:
                return "Show rooms containing at least one door that is not referenced by any resolved " +
                       "RoomConnection endpoint. Scene backlinks are ignored.";

            case RoomGraphRoomFilter.RoomsWithErrors:
                return "Show rooms whose effective status is Invalid, including errors on incident connections.";

            case RoomGraphRoomFilter.RoomsWithWarnings:
                return "Show rooms with warnings, including warnings on incident connections. " +
                       "Warnings combine as a union with the other active filters.";

            default:
                return "Show every room. Selecting a specific filter clears All Rooms; " +
                       "clearing every specific filter restores it.";
        }
    }

    private void BuildSearchResults(VisualElement root)
    {
        _searchResultsContainer = new VisualElement();
        _searchResultsContainer.style.display = DisplayStyle.None;
        _searchResultsContainer.style.maxHeight = 210;
        _searchResultsContainer.style.borderBottomWidth = 1;
        _searchResultsContainer.style.borderBottomColor = new Color(0f, 0f, 0f, 0.5f);
        _searchResultsContainer.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f);

        _searchResultsSummary = new Label();
        _searchResultsSummary.style.fontSize = 10;
        _searchResultsSummary.style.paddingLeft = 10;
        _searchResultsSummary.style.paddingTop = 4;
        _searchResultsSummary.style.paddingBottom = 4;
        _searchResultsSummary.style.color = new Color(0.78f, 0.8f, 0.82f);
        _searchResultsContainer.Add(_searchResultsSummary);

        _searchResultsList = new ScrollView(ScrollViewMode.Vertical);
        _searchResultsList.style.maxHeight = 180;
        _searchResultsContainer.Add(_searchResultsList);

        root.Add(_searchResultsContainer);
    }

    private void BuildBody(VisualElement root)
    {
        VisualElement body = new VisualElement();
        body.style.flexDirection = FlexDirection.Row;
        body.style.flexGrow = 1;

        _viewport = new VisualElement();
        _viewport.style.flexGrow = 1;
        _viewport.style.position = Position.Relative;
        _viewport.style.overflow = Overflow.Hidden;
        _viewport.style.backgroundColor = new Color(0.10f, 0.105f, 0.115f);
        _viewport.pickingMode = PickingMode.Position;

        _content = new VisualElement();
        _content.style.position = Position.Absolute;
        _content.style.left = 0;
        _content.style.top = 0;
        _content.style.transformOrigin =
            new TransformOrigin(Length.Percent(0), Length.Percent(0), 0f);

        _edgeLayer = new VisualElement();
        _edgeLayer.style.position = Position.Absolute;
        _edgeLayer.style.left = 0;
        _edgeLayer.style.top = 0;
        _edgeLayer.pickingMode = PickingMode.Position;
        _edgeLayer.generateVisualContent += DrawEdges;
        _edgeLayer.RegisterCallback<PointerDownEvent>(OnEdgeLayerPointerDown);
        _content.Add(_edgeLayer);

        _viewport.Add(_content);
        _viewport.RegisterCallback<PointerDownEvent>(OnViewportPointerDown);
        _viewport.RegisterCallback<PointerMoveEvent>(OnViewportPointerMove);
        _viewport.RegisterCallback<PointerUpEvent>(OnViewportPointerUp);
        _viewport.RegisterCallback<WheelEvent>(OnWheel);

        body.Add(_viewport);

        _sidePanel = new ScrollView(ScrollViewMode.Vertical);
        _sidePanel.style.width = 390;
        _sidePanel.style.borderLeftWidth = 1;
        _sidePanel.style.borderLeftColor = new Color(0f, 0f, 0f, 0.5f);
        _sidePanel.style.backgroundColor = new Color(0.15f, 0.15f, 0.16f);
        _sidePanel.style.paddingLeft = 10;
        _sidePanel.style.paddingRight = 10;
        _sidePanel.style.paddingTop = 8;
        _sidePanel.style.paddingBottom = 8;

        body.Add(_sidePanel);
        root.Add(body);
    }

    private void RefreshModel()
    {
        // Commit an in-progress drag before replacing the model so a manual
        // Refresh cannot leave an unpersisted intermediate position behind.
        if (_dragNode != null)
            FinishNodeDrag();

        string selectedNodeKey = _selectedNode != null ? _selectedNode.NodeKey : null;
        string selectedEdgePath = _selectedEdge != null ? _selectedEdge.AssetPath : null;
        string selectedDoorId = _selectedDoor != null ? _selectedDoor.StableDoorId : null;
        string selectedDoorNodeKey = _selectedDoor != null && _selectedNode != null
            ? _selectedNode.NodeKey
            : null;
        string selectedDoorObjectName = _selectedDoor != null ? _selectedDoor.GameObjectName : null;

        _model = RoomConnectivityGraphService.Build();
        RefreshLayoutData();
        _searchRevealedNodes.Clear();
        ResetDragState();

        RebuildGraphElements();

        _selectedNode = RestoreNodeSelection(_model, selectedNodeKey);
        _selectedEdge = string.IsNullOrEmpty(selectedEdgePath) ? null : _model.FindEdgeByPath(selectedEdgePath);
        _selectedDoor = RestoreDoorSelection(
            _model,
            _selectedNode,
            selectedDoorNodeKey,
            selectedDoorId,
            selectedDoorObjectName);

        RefreshSearchResults();
        ApplyVisibility();

        UpdateToolbar();
        UpdateSelectionVisuals();
        UpdateDetails();

        _content.style.width = _layout.ContentSize.x;
        _content.style.height = _layout.ContentSize.y;
        _edgeLayer.style.width = _layout.ContentSize.x;
        _edgeLayer.style.height = _layout.ContentSize.y;
        ApplyTransform();

        if (!_initialFitDone)
        {
            _initialFitDone = true;
            _viewport.schedule.Execute(FitGraph);
        }
    }

    private static RoomGraphDoor RestoreDoorSelection(
        RoomConnectivityGraphModel model,
        RoomGraphNode node,
        string nodeKey,
        string stableDoorId,
        string gameObjectName)
    {
        if (model == null || node == null || string.IsNullOrEmpty(nodeKey))
            return null;

        if (!string.Equals(node.NodeKey, nodeKey, StringComparison.Ordinal))
            return null;

        foreach (RoomGraphDoor door in node.Doors)
        {
            bool idMatches =
                !string.IsNullOrWhiteSpace(stableDoorId) &&
                string.Equals(door.StableDoorId, stableDoorId, StringComparison.Ordinal);

            bool nameMatches =
                string.IsNullOrWhiteSpace(stableDoorId) &&
                string.Equals(door.GameObjectName, gameObjectName, StringComparison.Ordinal);

            if (idMatches || nameMatches)
                return door;
        }

        return null;
    }

    private void RebuildGraphElements()
    {
        foreach (VisualElement label in _edgeLabelElements.Values)
            label.RemoveFromHierarchy();

        foreach (KeyValuePair<RoomGraphNode, VisualElement> pair in _nodeElements)
            pair.Value.RemoveFromHierarchy();

        _edgeLabelElements.Clear();
        _nodeElements.Clear();

        _content.style.width = _layout.ContentSize.x;
        _content.style.height = _layout.ContentSize.y;
        _edgeLayer.style.width = _layout.ContentSize.x;
        _edgeLayer.style.height = _layout.ContentSize.y;

        foreach (RoomGraphEdge edge in _model.Edges)
        {
            if (!_layout.EdgeGeometries.TryGetValue(edge.Connection, out RoomGraphEdgeGeometry geometry))
                continue;

            Label label = BuildEdgeLabel(edge, geometry);
            _edgeLabelElements[edge] = label;
            _content.Add(label);
        }

        foreach (RoomGraphNode node in _model.Nodes)
        {
            if (!_layout.NodeRects.TryGetValue(node, out Rect rect))
                continue;

            VisualElement element = BuildNodeElement(node, rect);
            _nodeElements.Add(node, element);
            _content.Add(element);
        }

        _edgeLayer.MarkDirtyRepaint();
    }

    private VisualElement BuildNodeElement(RoomGraphNode node, Rect rect)
    {
        VisualElement element = new VisualElement();
        element.style.position = Position.Absolute;
        element.style.left = rect.x;
        element.style.top = rect.y;
        element.style.width = rect.width;
        element.style.height = rect.height;
        element.style.paddingLeft = 10;
        element.style.paddingRight = 10;
        element.style.paddingTop = 8;
        element.style.paddingBottom = 8;
        element.style.borderTopLeftRadius = 7;
        element.style.borderTopRightRadius = 7;
        element.style.borderBottomLeftRadius = 7;
        element.style.borderBottomRightRadius = 7;
        element.style.borderLeftWidth = 2;
        element.style.borderRightWidth = 2;
        element.style.borderTopWidth = 2;
        element.style.borderBottomWidth = 2;
        element.style.backgroundColor = new Color(0.21f, 0.22f, 0.24f);

        Color statusColor = StatusColor(GetNodeStatus(_model, node));

        Label title = new Label(node.DisplayName);
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 14;
        title.style.color = new Color(0.95f, 0.95f, 0.95f);
        title.style.whiteSpace = WhiteSpace.Normal;
        element.Add(title);

        Label scene = new Label($"Scene: {node.SceneName}");
        scene.style.fontSize = 11;
        scene.style.color = new Color(0.7f, 0.72f, 0.75f);
        scene.style.whiteSpace = WhiteSpace.Normal;
        element.Add(scene);

        Label topology = new Label(
            $"Doors {node.DoorCount}   Topology {node.ReferencedDoorCount}   Unref {node.UnreferencedDoorCount}");
        topology.style.fontSize = 10;
        topology.style.marginTop = 6;
        topology.style.color = new Color(0.82f, 0.84f, 0.86f);
        topology.style.whiteSpace = WhiteSpace.Normal;
        element.Add(topology);

        Label backlinks = new Label(
            $"Backlink ok {node.BacklinkConsistentDoorCount}   " +
            $"miss {node.BacklinkMissingDoorCount}   " +
            $"mis {node.BacklinkMismatchedDoorCount}   " +
            $"orphan {node.BacklinkOrphanedDoorCount}");
        backlinks.style.fontSize = 10;
        backlinks.style.color = new Color(0.78f, 0.8f, 0.82f);
        backlinks.style.whiteSpace = WhiteSpace.Normal;
        element.Add(backlinks);

        Label integrity = new Label(
            $"Unconnected {node.FullyUnconnectedDoorCount}   " +
            $"Invalid ID {node.InvalidIdentityDoorCount}   " +
            $"Invalid conn {node.InvalidConnectionDoorCount}");
        integrity.style.fontSize = 10;
        integrity.style.color = new Color(0.78f, 0.8f, 0.82f);
        integrity.style.whiteSpace = WhiteSpace.Normal;
        element.Add(integrity);

        Label status = new Label(GetNodeStatusLabel(GetNodeStatus(_model, node)));
        status.style.fontSize = 11;
        status.style.unityFontStyleAndWeight = FontStyle.Bold;
        status.style.color = statusColor;
        status.style.marginTop = 6;
        element.Add(status);

        element.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            SelectNode(node);
            BeginNodeDrag(node, element, evt);
            evt.StopPropagation();
        });

        element.RegisterCallback<PointerMoveEvent>(OnNodePointerMove);
        element.RegisterCallback<PointerUpEvent>(OnNodePointerUp);
        element.RegisterCallback<PointerCaptureOutEvent>(OnNodePointerCaptureOut);

        element.tooltip = $"{node.DisplayName}\n{node.RoomId}\n{node.SceneName}";
        return element;
    }

    private Label BuildEdgeLabel(RoomGraphEdge edge, RoomGraphEdgeGeometry geometry)
    {
        string text = $"{Shorten(edge.EndpointADoorId)} {edge.DirectionArrow} {Shorten(edge.EndpointBDoorId)}";

        if (!edge.IsValid)
            text = "⚠ " + text;

        Label label = new Label(text);
        label.style.position = Position.Absolute;
        label.style.left = geometry.Midpoint.x - 58f;
        label.style.top = geometry.Midpoint.y - 11f;
        label.style.minWidth = 46f;
        label.style.maxWidth = 170f;
        label.style.paddingLeft = 5;
        label.style.paddingRight = 5;
        label.style.paddingTop = 2;
        label.style.paddingBottom = 2;
        label.style.borderTopLeftRadius = 4;
        label.style.borderTopRightRadius = 4;
        label.style.borderBottomLeftRadius = 4;
        label.style.borderBottomRightRadius = 4;
        label.style.fontSize = 10;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f, 0.92f);
        label.style.color = edge.IsValid
            ? new Color(0.65f, 0.9f, 0.72f)
            : new Color(1f, 0.55f, 0.5f);
        label.tooltip =
            $"{edge.AssetName}\n{edge.EndpointALabel} {edge.DirectionArrow} {edge.EndpointBLabel}\n" +
            $"{edge.DirectionLabel}\n{edge.StatusLabel}";

        label.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            SelectEdge(edge);
            evt.StopPropagation();
        });

        return label;
    }

    private void DrawEdges(MeshGenerationContext context)
    {
        if (_layout == null)
            return;

        Painter2D painter = context.painter2D;

        foreach (KeyValuePair<RoomConnection, RoomGraphEdgeGeometry> pair in _layout.EdgeGeometries)
        {
            RoomGraphEdgeGeometry geometry = pair.Value;
            RoomGraphEdge edge = geometry.Edge;

            if (!RoomConnectivityGraphNavigation.IsEdgeVisible(edge, _visibleNodes))
                continue;

            bool selected = _selectedEdge != null && ReferenceEquals(_selectedEdge, edge);

            Color color = selected
                ? new Color(1f, 0.85f, 0.35f)
                : edge.IsValid
                    ? new Color(0.35f, 0.75f, 0.62f)
                    : new Color(0.9f, 0.34f, 0.3f);

            painter.lineWidth = selected ? 3.5f : 2f;
            painter.strokeColor = color;

            const int steps = 28;
            Vector2 previous = geometry.Start;

            for (int i = 1; i <= steps; i++)
            {
                Vector2 current = geometry.Evaluate(i / (float)steps);
                painter.BeginPath();
                painter.MoveTo(previous);
                painter.LineTo(current);
                painter.Stroke();
                previous = current;
            }

            if (geometry.ArrowAtEnd)
                DrawArrow(painter, geometry.End, geometry.Control, color);

            if (geometry.ArrowAtStart)
                DrawArrow(painter, geometry.Start, geometry.Control, color);
        }
    }

    private static void DrawArrow(Painter2D painter, Vector2 tip, Vector2 control, Color color)
    {
        Vector2 direction = tip - control;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();
        Vector2 back = tip - direction * 12f;
        Vector2 normal = new Vector2(-direction.y, direction.x) * 5f;

        painter.strokeColor = color;
        painter.BeginPath();
        painter.MoveTo(back + normal);
        painter.LineTo(tip);
        painter.LineTo(back - normal);
        painter.Stroke();
    }

    private void OnEdgeLayerPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || _layout == null)
            return;

        RoomGraphEdge hit = FindEdgeAt(evt.localPosition);

        if (hit == null)
            return;

        SelectEdge(hit);
        evt.StopPropagation();
    }

    private RoomGraphEdge FindEdgeAt(Vector2 contentPosition)
    {
        RoomGraphEdge best = null;
        float bestDistance = float.MaxValue;

        foreach (KeyValuePair<RoomConnection, RoomGraphEdgeGeometry> pair in _layout.EdgeGeometries)
        {
            RoomGraphEdgeGeometry geometry = pair.Value;

            if (!RoomConnectivityGraphNavigation.IsEdgeVisible(geometry.Edge, _visibleNodes))
                continue;

            Vector2 previous = geometry.Start;

            for (int i = 1; i <= 24; i++)
            {
                Vector2 current = geometry.Evaluate(i / 24f);
                float distance = DistanceToSegment(contentPosition, previous, current);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = geometry.Edge;
                }

                previous = current;
            }
        }

        return bestDistance <= EdgeHitThreshold ? best : null;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared < 0.0001f)
            return Vector2.Distance(point, start);

        float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        Vector2 projection = start + segment * t;
        return Vector2.Distance(point, projection);
    }

    private void OnViewportPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 0)
            return;

        ClearSelection();
        _panning = true;
        _panPointerId = evt.pointerId;
        _viewport.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnViewportPointerMove(PointerMoveEvent evt)
    {
        if (!_panning || !_viewport.HasPointerCapture(_panPointerId))
            return;

        _pan += new Vector2(evt.deltaPosition.x, evt.deltaPosition.y);
        ApplyTransform();
        evt.StopPropagation();
    }

    private void OnViewportPointerUp(PointerUpEvent evt)
    {
        if (!_panning)
            return;

        _panning = false;

        if (_viewport.HasPointerCapture(_panPointerId))
            _viewport.ReleasePointer(_panPointerId);

        _panPointerId = -1;
        evt.StopPropagation();
    }

    private void OnWheel(WheelEvent evt)
    {
        float factor = evt.delta.y > 0 ? 0.9f : 1.1f;
        _zoom = Mathf.Clamp(_zoom * factor, 0.3f, 2.5f);
        ApplyTransform();
        evt.StopPropagation();
    }

    private void ApplyTransform()
    {
        if (_content == null)
            return;

        _content.style.translate = new Translate(_pan.x, _pan.y);
        _content.style.scale = new Scale(new Vector2(_zoom, _zoom));

        if (_zoomLabel != null)
            _zoomLabel.text = $"{Mathf.RoundToInt(_zoom * 100f)}%";
    }

    private void FitGraph()
    {
        if (_layout == null || _viewport == null)
        {
            _fitAttempts = 0;
            return;
        }

        float viewWidth = _viewport.layout.width;
        float viewHeight = _viewport.layout.height;

        if (viewWidth <= 1f || viewHeight <= 1f)
        {
            // The window may not be laid out yet. Retry a bounded number of frames
            // instead of rescheduling forever while the panel is hidden or detached.
            if (_fitAttempts++ < 30 && _viewport.panel != null)
                _viewport.schedule.Execute(FitGraph);

            return;
        }

        _fitAttempts = 0;

        float contentWidth = _layout.ContentSize.x;
        float contentHeight = _layout.ContentSize.y;

        float fit = Mathf.Min(
            (viewWidth - 40f) / contentWidth,
            (viewHeight - 40f) / contentHeight);

        _zoom = Mathf.Clamp(fit, 0.3f, 1.35f);
        _pan = new Vector2(
            (viewWidth - contentWidth * _zoom) * 0.5f,
            (viewHeight - contentHeight * _zoom) * 0.5f);

        ApplyTransform();
    }

    private void BeginNodeDrag(
        RoomGraphNode node,
        VisualElement element,
        PointerDownEvent evt)
    {
        if (_dragNode != null || node == null || element == null || _layout == null)
            return;

        _dragNode = node;
        _dragElement = element;
        _dragPointerId = evt.pointerId;
        _dragStarted = false;
        _dragFinishing = false;
        _dragAccumulatedPanelDelta = Vector2.zero;

        _dragStartPosition = _layout.NodeRects.TryGetValue(node, out Rect rect)
            ? new Vector2(rect.x, rect.y)
            : Vector2.zero;

        _dragPosition = _dragStartPosition;
        _dragHadSavedPosition = _layoutPositions.ContainsKey(node.NodeKey);

        element.CapturePointer(evt.pointerId);
    }

    private void OnNodePointerMove(PointerMoveEvent evt)
    {
        if (_dragNode == null || evt.pointerId != _dragPointerId)
            return;

        Vector2 panelDelta;
        Vector2 pointerDelta = new Vector2(
            evt.deltaPosition.x,
            evt.deltaPosition.y);

        if (!_dragStarted)
        {
            _dragAccumulatedPanelDelta += pointerDelta;

            if (!RoomConnectivityGraphLayoutService.ExceedsDragThreshold(
                    _dragAccumulatedPanelDelta))
            {
                evt.StopPropagation();
                return;
            }

            _layoutAsset = RoomConnectivityGraphLayoutService.Ensure();

            if (_layoutAsset == null)
            {
                ResetDragState();
                return;
            }

            RoomConnectivityGraphLayoutService.RecordUndo(
                _layoutAsset,
                RoomConnectivityGraphLayoutService.MoveUndoName);

            _dragStarted = true;
            panelDelta = _dragAccumulatedPanelDelta;
            _dragAccumulatedPanelDelta = Vector2.zero;
        }
        else
        {
            panelDelta = pointerDelta;
        }

        Vector2 graphDelta = RoomConnectivityGraphLayoutService.PanelDeltaToGraphDelta(
            panelDelta,
            _zoom);

        Vector2 next = RoomConnectivityGraphLayoutService.ClampToGraph(
            _dragPosition + graphDelta);

        if (next == _dragPosition)
        {
            evt.StopPropagation();
            return;
        }

        _dragPosition = next;

        if (_layoutAsset != null &&
            RoomConnectivityGraphLayoutService.SetPosition(
                _layoutAsset,
                _dragNode.NodeKey,
                _dragPosition))
        {
            _layoutPositions[_dragNode.NodeKey] = _dragPosition;
            _layout = RoomConnectivityGraphLayout.Compute(_model, _layoutPositions);
            ApplyLayoutGeometry();
        }

        evt.StopPropagation();
    }

    private void OnNodePointerUp(PointerUpEvent evt)
    {
        if (_dragNode == null || evt.pointerId != _dragPointerId)
            return;

        FinishNodeDrag();
        evt.StopPropagation();
    }

    private void OnNodePointerCaptureOut(PointerCaptureOutEvent evt)
    {
        if (_dragFinishing || _dragNode == null)
            return;

        FinishNodeDrag();
    }

    private void FinishNodeDrag()
    {
        if (_dragFinishing)
            return;

        _dragFinishing = true;

        try
        {
            VisualElement element = _dragElement;
            int pointerId = _dragPointerId;

            if (element != null && pointerId >= 0 && element.HasPointerCapture(pointerId))
                element.ReleasePointer(pointerId);

            if (_dragStarted && _layoutAsset != null)
            {
                bool changed = !_dragHadSavedPosition ||
                    Vector2.Distance(_dragStartPosition, _dragPosition) > 0.001f;

                // One drag = one Undo operation recorded at drag start. The
                // asset is written only once, when the drag ends and the
                // position actually changed.
                if (changed)
                    RoomConnectivityGraphLayoutService.Persist(_layoutAsset);

                UpdateLayoutDiagnostics();
            }

            if (element != null)
                element.style.opacity = 1f;
        }
        finally
        {
            ResetDragState();
            _dragFinishing = false;
        }
    }

    private void ResetDragState()
    {
        VisualElement element = _dragElement;
        int pointerId = _dragPointerId;

        // Clear the drag identity before releasing capture so a synchronous
        // PointerCaptureOutEvent cannot re-enter the drag.
        _dragNode = null;
        _dragElement = null;
        _dragPointerId = -1;
        _dragStarted = false;
        _dragAccumulatedPanelDelta = Vector2.zero;
        _dragStartPosition = Vector2.zero;
        _dragPosition = Vector2.zero;
        _dragHadSavedPosition = false;

        if (element == null)
            return;

        element.style.opacity = 1f;

        if (pointerId >= 0 && element.HasPointerCapture(pointerId))
            element.ReleasePointer(pointerId);
    }

    private void RefreshLayoutData()
    {
        _layoutAsset = RoomConnectivityGraphLayoutService.Load();
        _layoutPositions.Clear();

        foreach (KeyValuePair<string, Vector2> pair in
                 RoomConnectivityGraphLayoutService.BuildPositions(_layoutAsset))
        {
            _layoutPositions[pair.Key] = pair.Value;
        }

        _layout = RoomConnectivityGraphLayout.Compute(_model, _layoutPositions);
        UpdateLayoutDiagnostics();
    }

    private void ApplyLayoutGeometry()
    {
        if (_layout == null || _content == null || _edgeLayer == null)
            return;

        _content.style.width = _layout.ContentSize.x;
        _content.style.height = _layout.ContentSize.y;
        _edgeLayer.style.width = _layout.ContentSize.x;
        _edgeLayer.style.height = _layout.ContentSize.y;

        foreach (KeyValuePair<RoomGraphNode, VisualElement> pair in _nodeElements)
        {
            if (!_layout.NodeRects.TryGetValue(pair.Key, out Rect rect))
                continue;

            pair.Value.style.left = rect.x;
            pair.Value.style.top = rect.y;
        }

        foreach (KeyValuePair<RoomGraphEdge, VisualElement> pair in _edgeLabelElements)
        {
            if (pair.Key == null ||
                !_layout.EdgeGeometries.TryGetValue(
                    pair.Key.Connection,
                    out RoomGraphEdgeGeometry geometry))
            {
                continue;
            }

            pair.Value.style.left = geometry.Midpoint.x - 58f;
            pair.Value.style.top = geometry.Midpoint.y - 11f;
        }

        _edgeLayer.MarkDirtyRepaint();
    }

    private void UpdateLayoutDiagnostics()
    {
        if (_layoutDiagnosticLabel == null)
            return;

        if (_model == null)
        {
            _layoutDiagnosticLabel.text = string.Empty;
            _layoutDiagnosticLabel.tooltip = null;
            return;
        }

        List<string> currentKeys = GetCurrentNodeKeys();

        if (_layoutAsset == null)
        {
            _layoutDiagnosticLabel.text = "Layout: automatic";
            _layoutDiagnosticLabel.tooltip =
                "No saved layout asset is loaded. Positions use the deterministic automatic grid.";
            _layoutDiagnosticLabel.style.color = new Color(0.68f, 0.72f, 0.76f);
            return;
        }

        int applied = 0;

        foreach (string key in currentKeys)
        {
            if (_layoutPositions.ContainsKey(key))
                applied++;
        }

        List<string> stale =
            RoomConnectivityGraphLayoutService.FindStaleKeys(_layoutAsset, currentKeys);
        List<string> duplicates =
            RoomConnectivityGraphLayoutService.FindDuplicateKeys(_layoutAsset);
        int invalid =
            RoomConnectivityGraphLayoutService.CountInvalidEntries(_layoutAsset);

        string text = applied == 0
            ? "Layout: automatic"
            : $"Layout: {applied} saved";

        if (stale.Count > 0)
            text += $" · {stale.Count} stale";

        if (duplicates.Count > 0)
            text += $" · {duplicates.Count} duplicate";

        if (invalid > 0)
            text += $" · {invalid} invalid";

        _layoutDiagnosticLabel.text = text;

        List<string> tooltip = new List<string>
        {
            "Positions are matched only by NodeKey (RoomDefinition asset GUID).",
            "Stale entries are never reassigned and are not deleted automatically."
        };

        if (stale.Count > 0)
        {
            tooltip.Add("Stale NodeKeys:");

            int shown = Mathf.Min(stale.Count, 12);

            for (int i = 0; i < shown; i++)
                tooltip.Add("  " + stale[i]);

            if (stale.Count > shown)
                tooltip.Add($"  …and {stale.Count - shown} more");
        }

        if (duplicates.Count > 0)
            tooltip.Add("Duplicate entries: " + string.Join(", ", duplicates));

        _layoutDiagnosticLabel.tooltip = string.Join("\n", tooltip);
        _layoutDiagnosticLabel.style.color =
            stale.Count > 0 || duplicates.Count > 0 || invalid > 0
                ? new Color(0.95f, 0.75f, 0.35f)
                : new Color(0.68f, 0.72f, 0.76f);
    }

    private List<string> GetCurrentNodeKeys()
    {
        List<string> keys = new List<string>();

        if (_model == null)
            return keys;

        foreach (RoomGraphNode node in _model.Nodes)
        {
            if (node != null && !string.IsNullOrEmpty(node.NodeKey))
                keys.Add(node.NodeKey);
        }

        return keys;
    }

    private void AutoLayoutGraph()
    {
        if (_model == null || _model.Nodes.Count == 0)
            return;

        RoomConnectivityGraphLayoutAsset asset = RoomConnectivityGraphLayoutService.Ensure();

        if (asset == null)
        {
            Debug.LogWarning("[RoomGraph] Could not create the layout asset.");
            return;
        }

        _layoutAsset = asset;

        if (!RoomConnectivityGraphLayoutService.AutoLayout(asset, _model))
            return;

        RefreshLayoutData();
        ApplyLayoutGeometry();
    }

    private void ResetLayoutGraph()
    {
        if (_model == null)
            return;

        if (_layoutAsset == null)
            _layoutAsset = RoomConnectivityGraphLayoutService.Load();

        if (!RoomConnectivityGraphLayoutService.ResetLayout(_layoutAsset))
            return;

        RefreshLayoutData();
        ApplyLayoutGeometry();
    }

    private void OnUndoRedoPerformed()
    {
        if (_model == null || _layoutAsset == null || _layout == null)
            return;

        Dictionary<string, Vector2> current =
            RoomConnectivityGraphLayoutService.BuildPositions(_layoutAsset);

        if (RoomConnectivityGraphLayoutService.PositionsEqual(current, _layoutPositions))
            return;

        _layoutPositions.Clear();

        foreach (KeyValuePair<string, Vector2> pair in current)
            _layoutPositions[pair.Key] = pair.Value;

        _layout = RoomConnectivityGraphLayout.Compute(_model, _layoutPositions);
        ApplyLayoutGeometry();
        UpdateLayoutDiagnostics();

        // Undo/Redo changed the layout asset in memory. Persist only that
        // dedicated asset so the restored state survives a domain reload.
        RoomConnectivityGraphLayoutService.Persist(_layoutAsset);
    }

    private void SelectNode(RoomGraphNode node)
    {
        _showUnresolvedPanel = false;
        _selectedNode = node;
        _selectedEdge = null;
        _selectedDoor = null;
        UpdateSelectionVisuals();
        UpdateDetails();
    }

    private void SelectDoor(RoomGraphNode node, RoomGraphDoor door)
    {
        _showUnresolvedPanel = false;
        _selectedNode = node;
        _selectedEdge = null;
        _selectedDoor = door;
        UpdateSelectionVisuals();
        UpdateDetails();
    }

    private void SelectEdge(RoomGraphEdge edge)
    {
        _showUnresolvedPanel = false;
        _selectedEdge = edge;
        _selectedNode = null;
        _selectedDoor = null;
        UpdateSelectionVisuals();
        UpdateDetails();
    }

    private void ShowUnresolvedPanel()
    {
        _showUnresolvedPanel = true;
        _selectedNode = null;
        _selectedEdge = null;
        _selectedDoor = null;
        UpdateSelectionVisuals();
        UpdateDetails();
    }

    private void ClearSelection()
    {
        if (_selectedNode == null && _selectedEdge == null && !_showUnresolvedPanel)
            return;

        _showUnresolvedPanel = false;
        _selectedNode = null;
        _selectedEdge = null;
        _selectedDoor = null;
        UpdateSelectionVisuals();
        UpdateDetails();
    }

    private void ApplyVisibility()
    {
        if (_model == null)
        {
            _visibleNodes = new HashSet<RoomGraphNode>();
        }
        else
        {
            _visibleNodes = RoomConnectivityGraphNavigation.ComputeVisibleRooms(
                _model,
                _filters,
                _searchRevealedNodes);
        }

        foreach (KeyValuePair<RoomGraphNode, VisualElement> pair in _nodeElements)
        {
            pair.Value.style.display = _visibleNodes.Contains(pair.Key)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        foreach (KeyValuePair<RoomGraphEdge, VisualElement> pair in _edgeLabelElements)
        {
            pair.Value.style.display = RoomConnectivityGraphNavigation.IsEdgeVisible(
                pair.Key,
                _visibleNodes)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (_visibilityLabel != null)
        {
            _visibilityLabel.text = _model == null
                ? string.Empty
                : $"Visible {_visibleNodes.Count}/{_model.Nodes.Count}";
        }

        if (_edgeLayer != null)
            _edgeLayer.MarkDirtyRepaint();
    }

    private void OnFilterToggleChanged(RoomGraphRoomFilter filter, bool value)
    {
        if (_syncingFilterToggles)
            return;

        RoomGraphRoomFilter next = _filters;

        if (filter == RoomGraphRoomFilter.AllRooms)
        {
            next = value
                ? RoomGraphRoomFilter.AllRooms
                : RoomGraphRoomFilter.None;
        }
        else if (value)
        {
            next &= ~RoomGraphRoomFilter.AllRooms;
            next |= filter;
        }
        else
        {
            next &= ~filter;
        }

        _filters = RoomConnectivityGraphNavigation.Normalize(next);
        SyncFilterToggles();
        ApplyVisibility();
        UpdateToolbar();
    }

    private void SyncFilterToggles()
    {
        _syncingFilterToggles = true;

        try
        {
            bool allRooms = !RoomConnectivityGraphNavigation.HasActiveSpecificFilters(_filters);

            if (_allRoomsToggle != null)
                _allRoomsToggle.SetValueWithoutNotify(allRooms);

            if (_connectedToggle != null)
                _connectedToggle.SetValueWithoutNotify(
                    (_filters & RoomGraphRoomFilter.ConnectedRooms) != 0);

            if (_unconnectedDoorsToggle != null)
                _unconnectedDoorsToggle.SetValueWithoutNotify(
                    (_filters & RoomGraphRoomFilter.RoomsWithUnconnectedDoors) != 0);

            if (_errorsToggle != null)
                _errorsToggle.SetValueWithoutNotify(
                    (_filters & RoomGraphRoomFilter.RoomsWithErrors) != 0);

            if (_warningsToggle != null)
                _warningsToggle.SetValueWithoutNotify(
                    (_filters & RoomGraphRoomFilter.RoomsWithWarnings) != 0);
        }
        finally
        {
            _syncingFilterToggles = false;
        }
    }

    private void OnSearchKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            if (_searchResults.Count > 0)
                ActivateSearchResult(_searchResults[0]);

            evt.StopPropagation();
            return;
        }

        if (evt.keyCode == KeyCode.Escape)
        {
            ClearSearch();
            evt.StopPropagation();
        }
    }

    private void OnSearchChanged(string value)
    {
        _searchQuery = value ?? string.Empty;
        _searchRevealedNodes.Clear();
        RefreshSearchResults();
        ApplyVisibility();
        UpdateToolbar();
    }

    private void ClearSearch()
    {
        _searchQuery = string.Empty;

        if (_searchField != null)
            _searchField.SetValueWithoutNotify(string.Empty);

        _searchRevealedNodes.Clear();
        RefreshSearchResults();
        ApplyVisibility();
        UpdateToolbar();
    }

    private void RefreshSearchResults()
    {
        _searchResults.Clear();

        if (_model != null && !string.IsNullOrWhiteSpace(_searchQuery))
            _searchResults.AddRange(RoomConnectivityGraphNavigation.Search(_model, _searchQuery));

        if (_searchResultsList == null || _searchResultsContainer == null)
            return;

        _searchResultsList.Clear();

        if (string.IsNullOrWhiteSpace(_searchQuery) || _model == null)
        {
            HideSearchResults();
            return;
        }

        _searchResultsContainer.style.display = DisplayStyle.Flex;

        int shown = Mathf.Min(_searchResults.Count, MaxSearchResultsShown);

        _searchResultsSummary.text = _searchResults.Count == 0
            ? $"No results for '{_searchQuery.Trim()}'."
            : _searchResults.Count == shown
                ? $"{_searchResults.Count} result(s) for '{_searchQuery.Trim()}'."
                : $"{_searchResults.Count} result(s) for '{_searchQuery.Trim()}' (showing first {shown}).";

        for (int i = 0; i < shown; i++)
            _searchResultsList.Add(BuildSearchResultRow(_searchResults[i]));
    }

    private void HideSearchResults()
    {
        if (_searchResultsContainer != null)
            _searchResultsContainer.style.display = DisplayStyle.None;

        if (_searchResultsList != null)
            _searchResultsList.Clear();

        if (_searchResultsSummary != null)
            _searchResultsSummary.text = string.Empty;
    }

    private VisualElement BuildSearchResultRow(RoomGraphSearchResult result)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Column;
        row.style.paddingLeft = 10;
        row.style.paddingRight = 8;
        row.style.paddingTop = 3;
        row.style.paddingBottom = 3;
        row.style.borderLeftWidth = 3;
        row.style.borderLeftColor = SearchResultColor(result.Kind);
        row.style.borderBottomWidth = 1;
        row.style.borderBottomColor = new Color(0f, 0f, 0f, 0.2f);
        row.tooltip = string.IsNullOrEmpty(result.Subtitle)
            ? result.KindLabel
            : $"{result.KindLabel}: {result.Subtitle}";

        Label title = new Label($"{result.KindLabel}: {result.Title}");
        title.style.fontSize = 11;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = new Color(0.9f, 0.9f, 0.92f);
        title.style.whiteSpace = WhiteSpace.Normal;
        row.Add(title);

        if (!string.IsNullOrEmpty(result.Subtitle))
        {
            Label subtitle = new Label(result.Subtitle);
            subtitle.style.fontSize = 10;
            subtitle.style.color = new Color(0.72f, 0.74f, 0.78f);
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            row.Add(subtitle);
        }

        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            ActivateSearchResult(result);
            evt.StopPropagation();
        });

        row.RegisterCallback<PointerEnterEvent>(_ =>
            row.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f));
        row.RegisterCallback<PointerLeaveEvent>(_ =>
            row.style.backgroundColor = Color.clear);

        return row;
    }

    private static Color SearchResultColor(RoomGraphSearchResultKind kind)
    {
        switch (kind)
        {
            case RoomGraphSearchResultKind.Door:
                return new Color(0.5f, 0.72f, 0.92f);
            case RoomGraphSearchResultKind.Connection:
                return new Color(0.55f, 0.85f, 0.65f);
            default:
                return new Color(0.9f, 0.78f, 0.42f);
        }
    }

    private void ActivateSearchResult(RoomGraphSearchResult result)
    {
        if (result == null || _model == null)
            return;

        _searchRevealedNodes.Clear();

        switch (result.Kind)
        {
            case RoomGraphSearchResultKind.Door:
                if (result.Room != null)
                    _searchRevealedNodes.Add(result.Room);

                ApplyVisibility();
                SelectDoor(result.Room, result.Door);
                FocusNode(result.Room);
                break;

            case RoomGraphSearchResultKind.Connection:
                if (result.Connection != null)
                {
                    if (result.Connection.SourceNode != null)
                        _searchRevealedNodes.Add(result.Connection.SourceNode);

                    if (result.Connection.TargetNode != null)
                        _searchRevealedNodes.Add(result.Connection.TargetNode);
                }

                ApplyVisibility();
                SelectEdge(result.Connection);
                FocusEdge(result.Connection);
                break;

            default:
                if (result.Room != null)
                    _searchRevealedNodes.Add(result.Room);

                ApplyVisibility();
                SelectNode(result.Room);
                FocusNode(result.Room);
                break;
        }

        UpdateToolbar();
    }

    private void FocusNode(RoomGraphNode node)
    {
        if (node == null || _layout == null)
            return;

        if (_layout.NodeRects.TryGetValue(node, out Rect rect))
            FocusContentPoint(rect.center, 1);
    }

    private void FocusEdge(RoomGraphEdge edge)
    {
        if (edge == null || _layout == null || edge.Connection == null)
            return;

        if (_layout.EdgeGeometries.TryGetValue(edge.Connection, out RoomGraphEdgeGeometry geometry))
            FocusContentPoint(geometry.Midpoint, 1);
    }

    private void FocusContentPoint(Vector2 contentPoint, int retries)
    {
        if (_viewport == null)
            return;

        float viewWidth = _viewport.layout.width;
        float viewHeight = _viewport.layout.height;

        if (viewWidth <= 1f || viewHeight <= 1f)
        {
            if (retries > 0 && _viewport.panel != null)
                _viewport.schedule.Execute(() => FocusContentPoint(contentPoint, retries - 1));

            return;
        }

        _pan = new Vector2(
            viewWidth * 0.5f - contentPoint.x * _zoom,
            viewHeight * 0.5f - contentPoint.y * _zoom);

        ApplyTransform();
    }

    private void UpdateSelectionVisuals()
    {
        foreach (KeyValuePair<RoomGraphNode, VisualElement> pair in _nodeElements)
        {
            bool selected = _selectedNode != null && ReferenceEquals(_selectedNode, pair.Key);
            Color border = selected
                ? new Color(1f, 0.85f, 0.35f)
                : StatusColor(GetNodeStatus(_model, pair.Key));

            pair.Value.style.borderLeftColor = border;
            pair.Value.style.borderRightColor = border;
            pair.Value.style.borderTopColor = border;
            pair.Value.style.borderBottomColor = border;
        }

        if (_edgeLayer != null)
            _edgeLayer.MarkDirtyRepaint();
    }

    private void UpdateToolbar()
    {
        if (_unresolvedButton != null)
        {
            int unresolvedCount = GetUnresolvedEdges(_model, _layout).Count;
            _unresolvedButton.text = $"Unresolved ({unresolvedCount})";
            _unresolvedButton.SetEnabled(_model != null);
        }

        if (_summaryLabel == null)
            return;

        if (_model == null)
        {
            _summaryLabel.text = "Not loaded — click Refresh";
            return;
        }

        _summaryLabel.text =
            $"Rooms {_visibleNodes.Count}/{_model.Nodes.Count}   Connections {_model.Edges.Count}   " +
            $"Errors {_model.ErrorCount}   Warnings {_model.WarningCount}";
    }

    private void UpdateDetails()
    {
        if (_sidePanel == null)
            return;

        _sidePanel.Clear();

        if (_model == null)
        {
            BuildEmptyDetails();
            return;
        }

        if (_showUnresolvedPanel)
        {
            BuildUnresolvedDetails();
            return;
        }

        if (_selectedEdge != null)
        {
            BuildEdgeDetails(_selectedEdge);
            return;
        }

        if (_selectedNode != null)
        {
            BuildNodeDetails(_selectedNode);
            return;
        }

        BuildSummaryDetails();
    }

    private void BuildEmptyDetails()
    {
        _sidePanel.Add(SectionTitle("Room Connectivity Graph"));
        _sidePanel.Add(InfoLabel(
            "The graph has not been loaded. Click Refresh to scan the registered rooms and build the connectivity graph."));
        _sidePanel.Add(InfoLabel(
            "Refreshing opens registered room scenes additively and closes them again. " +
            "It does not modify or save any scene or asset."));
    }

    private void BuildSummaryDetails()
    {
        _sidePanel.Add(SectionTitle("Room Connectivity Graph"));

        if (!_model.RegistryAvailable)
        {
            _sidePanel.Add(HelpBox(
                $"RoomRegistry is missing at '{RoomRegistrationService.RegistryPath}'.",
                HelpBoxMessageType.Error));
        }

        _sidePanel.Add(InfoLabel(
            $"{_model.Nodes.Count} room node(s), {_model.Edges.Count} connection edge(s).\n" +
            $"{_model.ErrorCount} error(s), {_model.WarningCount} warning(s)."));

        _sidePanel.Add(InfoLabel(
            "Select a room node or a connection label to inspect it. " +
            "Refresh rebuilds the graph from RoomDefinitions, discovered doors and RoomConnection assets."));

        int referencedDoors = 0;
        int unreferencedDoors = 0;
        int consistentBacklinks = 0;
        int missingBacklinks = 0;
        int mismatchedBacklinks = 0;
        int orphanedBacklinks = 0;
        int fullyUnconnectedDoors = 0;
        int identityIssues = 0;
        int invalidConnectionDoors = 0;

        foreach (RoomGraphNode node in _model.Nodes)
        {
            referencedDoors += node.ReferencedDoorCount;
            unreferencedDoors += node.UnreferencedDoorCount;
            consistentBacklinks += node.BacklinkConsistentDoorCount;
            missingBacklinks += node.BacklinkMissingDoorCount;
            mismatchedBacklinks += node.BacklinkMismatchedDoorCount;
            orphanedBacklinks += node.BacklinkOrphanedDoorCount;
            fullyUnconnectedDoors += node.FullyUnconnectedDoorCount;
            identityIssues += node.InvalidIdentityDoorCount;
            invalidConnectionDoors += node.InvalidConnectionDoorCount;
        }

        _sidePanel.Add(InfoLabel(
            $"Topology: {referencedDoors} referenced door(s), {unreferencedDoors} unreferenced."));
        _sidePanel.Add(InfoLabel(
            $"Backlinks: {consistentBacklinks} consistent, {missingBacklinks} missing, " +
            $"{mismatchedBacklinks} mismatched, {orphanedBacklinks} orphaned."));
        _sidePanel.Add(InfoLabel(
            $"Integrity: {fullyUnconnectedDoors} fully unconnected door(s), " +
            $"{identityIssues} identity issue(s), {invalidConnectionDoors} door(s) in invalid connections."));

        BuildUnresolvedSection();

        if (_model.GlobalIssues.Count > 0)
        {
            _sidePanel.Add(SectionTitle($"Project issues ({_model.GlobalIssues.Count})"));

            foreach (RoomGraphIssue issue in _model.GlobalIssues)
                AddIssue(issue);
        }

        _sidePanel.Add(MakeButton("Open Connection Creator…", RoomConnectionCreatorWindow.Open));
    }

    private void BuildNodeDetails(RoomGraphNode node)
    {
        RoomGraphStatus status = GetNodeStatus(_model, node);

        _sidePanel.Add(SectionTitle(node.DisplayName));
        _sidePanel.Add(StatusLabel(GetNodeStatusLabel(status), status));
        _sidePanel.Add(InfoLabel($"Room ID: {node.RoomId}"));
        _sidePanel.Add(InfoLabel($"Scene: {node.SceneName}"));
        _sidePanel.Add(InfoLabel($"Doors: {node.DoorCount}"));
        _sidePanel.Add(InfoLabel(
            $"Topology: {node.ReferencedDoorCount} referenced, {node.UnreferencedDoorCount} unreferenced."));
        _sidePanel.Add(InfoLabel(
            $"Backlinks: {node.BacklinkConsistentDoorCount} consistent, " +
            $"{node.BacklinkMissingDoorCount} missing, " +
            $"{node.BacklinkMismatchedDoorCount} mismatched, " +
            $"{node.BacklinkOrphanedDoorCount} orphaned."));
        _sidePanel.Add(InfoLabel(
            $"Integrity: {node.FullyUnconnectedDoorCount} fully unconnected, " +
            $"{node.InvalidIdentityDoorCount} identity issue(s), " +
            $"{node.InvalidConnectionDoorCount} door(s) in invalid connections."));

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.flexWrap = Wrap.Wrap;
        actions.style.marginTop = 6;
        actions.style.marginBottom = 8;

        Button openScene = MakeButton("Open Scene", () => OpenRoomScene(node));
        openScene.SetEnabled(node.SceneAvailable);
        actions.Add(openScene);

        Button pingDefinition = MakeButton("Ping Definition", () => PingObject(node.Definition));
        pingDefinition.SetEnabled(node.Definition != null);
        actions.Add(pingDefinition);

        _sidePanel.Add(actions);

        BuildSelectedDoorBlock(node);

        _sidePanel.Add(SectionTitle($"Doors ({node.Doors.Count})"));

        if (node.Doors.Count == 0)
            _sidePanel.Add(InfoLabel("No RoomTransition components were discovered in this scene."));

        foreach (RoomGraphDoor door in node.Doors)
            AddDoorRow(node, door);

        List<RoomGraphEdge> invalidIncident = GetInvalidIncidentEdges(node);

        if (invalidIncident.Count > 0)
        {
            _sidePanel.Add(SectionTitle($"Invalid incident connections ({invalidIncident.Count})"));

            foreach (RoomGraphEdge edge in invalidIncident)
            {
                RoomGraphEdge captured = edge;
                _sidePanel.Add(MakeButton(
                    $"{edge.AssetName}: {edge.StatusLabel}",
                    () => SelectEdge(captured)));
            }
        }

        if (node.Issues.Count > 0)
        {
            _sidePanel.Add(SectionTitle($"Room issues ({node.Issues.Count})"));

            foreach (RoomGraphIssue issue in node.Issues)
                AddIssue(issue);
        }
    }

    private void BuildSelectedDoorBlock(RoomGraphNode node)
    {
        if (_selectedDoor == null || node == null)
            return;

        if (!ReferenceEquals(_selectedDoor.Room, node.Definition))
            return;

        _sidePanel.Add(SectionTitle("Selected door"));

        string stable = string.IsNullOrWhiteSpace(_selectedDoor.StableDoorId)
            ? "<missing doorId>"
            : _selectedDoor.StableDoorId;

        Label title = new Label($"{stable}  —  {_selectedDoor.GameObjectName}");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 11;
        title.style.whiteSpace = WhiteSpace.Normal;
        _sidePanel.Add(title);

        foreach (string line in GetDoorDiagnosticLines(_selectedDoor))
            _sidePanel.Add(InfoLabel(line));

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.flexWrap = Wrap.Wrap;
        actions.style.marginTop = 3;
        actions.style.marginBottom = 6;
        AddDoorActions(actions, node, _selectedDoor);
        _sidePanel.Add(actions);
    }

    private void AddDoorRow(RoomGraphNode node, RoomGraphDoor door)
    {
        VisualElement row = new VisualElement();
        row.style.borderTopWidth = 1;
        row.style.borderTopColor = new Color(0f, 0f, 0f, 0.35f);
        row.style.paddingTop = 6;
        row.style.paddingBottom = 6;

        bool selected = _selectedDoor != null && ReferenceEquals(_selectedDoor, door);

        if (selected)
        {
            row.style.borderLeftWidth = 3;
            row.style.borderLeftColor = new Color(1f, 0.85f, 0.35f);
            row.style.paddingLeft = 6;
            row.style.backgroundColor = new Color(1f, 0.85f, 0.35f, 0.08f);
        }

        string stable = string.IsNullOrWhiteSpace(door.StableDoorId) ? "<missing doorId>" : door.StableDoorId;
        Label title = new Label($"{stable}  —  {door.GameObjectName}");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 11;
        title.style.whiteSpace = WhiteSpace.Normal;
        row.Add(title);

        foreach (string line in GetDoorDiagnosticLines(door))
        {
            Label diagnostic = new Label(line);
            diagnostic.style.fontSize = 10;
            diagnostic.style.color = DoorDiagnosticColor(line);
            diagnostic.style.whiteSpace = WhiteSpace.Normal;
            row.Add(diagnostic);
        }

        if (door.ReferencedByTopology)
        {
            List<string> names = new List<string>();

            foreach (RoomGraphEdge edge in door.ReferencingEdges)
                names.Add(edge.AssetName);

            Label references = new Label("References: " + string.Join(", ", names));
            references.style.fontSize = 10;
            references.style.color = new Color(0.72f, 0.82f, 0.9f);
            references.style.whiteSpace = WhiteSpace.Normal;
            row.Add(references);
        }

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.flexWrap = Wrap.Wrap;
        actions.style.marginTop = 3;
        AddDoorActions(actions, node, door);
        row.Add(actions);

        _sidePanel.Add(row);
    }

    private void AddDoorActions(VisualElement actions, RoomGraphNode node, RoomGraphDoor door)
    {
        if (actions == null || node == null || door == null)
            return;

        Button locate = MakeButton("Locate Door", () => LocateDoor(door));
        locate.SetEnabled(node.SceneAvailable);
        actions.Add(locate);

        if (door.Connection != null)
        {
            Button backlink = MakeButton("Ping Backlink", () => PingObject(door.Connection));
            backlink.tooltip = door.Connection.name;
            actions.Add(backlink);
        }

        foreach (RoomGraphEdge edge in door.ReferencingEdges)
        {
            RoomGraphEdge captured = edge;
            Button select = MakeButton($"Select {Shorten(edge.AssetName)}", () => SelectEdge(captured));
            select.tooltip = edge.AssetName;
            actions.Add(select);
        }
    }

    private void BuildEdgeDetails(RoomGraphEdge edge)
    {
        _sidePanel.Add(SectionTitle(edge.AssetName));
        _sidePanel.Add(StatusLabel(edge.StatusLabel,
            edge.IsValid ? RoomGraphStatus.Valid : RoomGraphStatus.Invalid));
        _sidePanel.Add(InfoLabel($"Direction: {edge.DirectionLabel}"));
        _sidePanel.Add(InfoLabel($"Endpoint A: {edge.EndpointALabel}"));
        _sidePanel.Add(InfoLabel($"Endpoint B: {edge.EndpointBLabel}"));
        _sidePanel.Add(InfoLabel($"Canvas: {GetCanvasStatus(edge)}"));

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.flexWrap = Wrap.Wrap;
        actions.style.marginTop = 6;
        actions.style.marginBottom = 8;

        Button pingAsset = MakeButton("Ping Connection Asset", () => PingObject(edge.Connection));
        pingAsset.SetEnabled(edge.Connection != null);
        actions.Add(pingAsset);

        if (edge.SourceNode != null)
        {
            Button openSource = MakeButton("Open Source Scene", () => OpenRoomScene(edge.SourceNode));
            openSource.SetEnabled(edge.SourceNode.SceneAvailable);
            actions.Add(openSource);
        }

        if (edge.TargetNode != null)
        {
            Button openTarget = MakeButton("Open Target Scene", () => OpenRoomScene(edge.TargetNode));
            openTarget.SetEnabled(edge.TargetNode.SceneAvailable);
            actions.Add(openTarget);
        }

        _sidePanel.Add(actions);

        if (edge.EndpointADoor != null)
        {
            RoomGraphDoor captured = edge.EndpointADoor;
            _sidePanel.Add(MakeButton("Locate Door A", () => LocateDoor(captured)));
        }

        if (edge.EndpointBDoor != null)
        {
            RoomGraphDoor captured = edge.EndpointBDoor;
            _sidePanel.Add(MakeButton("Locate Door B", () => LocateDoor(captured)));
        }

        if (edge.Issues.Count > 0)
        {
            _sidePanel.Add(SectionTitle($"Connection issues ({edge.Issues.Count})"));

            foreach (RoomGraphIssue issue in edge.Issues)
                AddIssue(issue);
        }
        else
        {
            _sidePanel.Add(InfoLabel("No validation issues were reported for this connection."));
        }

        _sidePanel.Add(MakeButton("Open Connection Creator…", RoomConnectionCreatorWindow.Open));
    }

    private void BuildUnresolvedDetails()
    {
        List<RoomGraphEdge> unresolved = GetUnresolvedEdges(_model, _layout);

        _sidePanel.Add(SectionTitle($"Unresolved Connections ({unresolved.Count})"));
        _sidePanel.Add(InfoLabel(
            "These connections are unresolved or cannot be fully represented as normal canvas edges. " +
            "Self-connections and connections with unresolved room or door endpoints are listed here."));

        if (unresolved.Count == 0)
        {
            _sidePanel.Add(InfoLabel("No unresolved connections were found."));
            return;
        }

        foreach (RoomGraphEdge edge in unresolved)
            AddUnresolvedEntry(_sidePanel, edge);

        _sidePanel.Add(MakeButton("Open Connection Creator…", RoomConnectionCreatorWindow.Open));
    }

    private void BuildUnresolvedSection()
    {
        List<RoomGraphEdge> unresolved = GetUnresolvedEdges(_model, _layout);

        _sidePanel.Add(SectionTitle($"Unresolved connections ({unresolved.Count})"));

        if (unresolved.Count == 0)
        {
            _sidePanel.Add(InfoLabel("All connections are represented on the canvas."));
            return;
        }

        _sidePanel.Add(InfoLabel(
            "These connections are unresolved or not fully represented on the canvas. " +
            "Select one to inspect its raw endpoints and issues."));

        foreach (RoomGraphEdge edge in unresolved)
            AddUnresolvedEntry(_sidePanel, edge);
    }

    private void AddUnresolvedEntry(VisualElement parent, RoomGraphEdge edge)
    {
        VisualElement container = new VisualElement();
        container.style.marginBottom = 8;
        container.style.paddingLeft = 6;
        container.style.paddingRight = 6;
        container.style.paddingTop = 5;
        container.style.paddingBottom = 5;
        container.style.borderLeftWidth = 3;
        container.style.borderLeftColor = new Color(0.9f, 0.62f, 0.3f);
        container.style.backgroundColor = new Color(0f, 0f, 0f, 0.16f);

        Label title = new Label(edge.AssetName);
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.fontSize = 11;
        title.style.whiteSpace = WhiteSpace.Normal;
        container.Add(title);

        container.Add(UnresolvedInfo($"Path: {RawId(edge.AssetPath)}"));
        container.Add(UnresolvedInfo($"Direction: {edge.DirectionLabel}"));
        container.Add(UnresolvedInfo($"Not drawn: {GetUnresolvedReason(edge, _model)}"));
        container.Add(UnresolvedInfo($"A raw: room '{RawId(edge.EndpointARoomId)}' / door '{RawId(edge.EndpointADoorId)}'"));
        container.Add(UnresolvedInfo($"B raw: room '{RawId(edge.EndpointBRoomId)}' / door '{RawId(edge.EndpointBDoorId)}'"));
        container.Add(UnresolvedInfo($"A resolution: {GetEndpointResolution(edge, true)}"));
        container.Add(UnresolvedInfo($"B resolution: {GetEndpointResolution(edge, false)}"));

        string codes = GetIssueCodes(edge);
        container.Add(UnresolvedInfo($"Issue codes: {(string.IsNullOrEmpty(codes) ? "none" : codes)}"));

        foreach (RoomGraphIssue issue in edge.Issues)
            AddIssue(container, issue);

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.flexWrap = Wrap.Wrap;
        actions.style.marginTop = 4;

        RoomGraphEdge captured = edge;
        actions.Add(MakeButton("Select", () => SelectEdge(captured)));

        Button pingAsset = MakeButton("Ping Asset", () => PingObject(captured.Connection));
        pingAsset.SetEnabled(edge.Connection != null);
        actions.Add(pingAsset);

        if (edge.SourceNode != null)
        {
            RoomGraphNode source = edge.SourceNode;
            Button openSource = MakeButton("Open Source Scene", () => OpenRoomScene(source));
            openSource.SetEnabled(source.SceneAvailable);
            actions.Add(openSource);
        }

        if (edge.TargetNode != null)
        {
            RoomGraphNode target = edge.TargetNode;
            Button openTarget = MakeButton("Open Target Scene", () => OpenRoomScene(target));
            openTarget.SetEnabled(target.SceneAvailable);
            actions.Add(openTarget);
        }

        container.Add(actions);
        parent.Add(container);
    }

    private static Label UnresolvedInfo(string text)
    {
        Label label = new Label(text);
        label.style.fontSize = 10;
        label.style.color = new Color(0.8f, 0.82f, 0.84f);
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private string GetCanvasStatus(RoomGraphEdge edge)
    {
        if (edge == null)
            return "Unknown";

        bool drawn = IsEdgeDrawn(edge, _layout);

        if (drawn && edge.EndpointsResolved && !edge.IsSelfConnection)
            return "Drawn";

        string reason = GetUnresolvedReason(edge, _model);
        return drawn
            ? "Drawn with unresolved endpoint — " + reason
            : "Not drawn — " + reason;
    }

    private List<RoomGraphEdge> GetInvalidIncidentEdges(RoomGraphNode node)
    {
        List<RoomGraphEdge> result = new List<RoomGraphEdge>();

        if (_model == null || node == null)
            return result;

        foreach (RoomGraphEdge edge in _model.Edges)
        {
            if (edge.HasErrors && IsEdgeIncidentToNode(edge, node))
                result.Add(edge);
        }

        return result;
    }

    internal static RoomGraphNode RestoreNodeSelection(
        RoomConnectivityGraphModel model,
        string nodeKey)
    {
        return model != null ? model.FindNodeByKey(nodeKey) : null;
    }

    internal static RoomGraphStatus GetNodeStatus(
        RoomConnectivityGraphModel model,
        RoomGraphNode node)
    {
        return RoomConnectivityGraphNavigation.GetRoomStatus(model, node);
    }

    private static string GetNodeStatusLabel(RoomGraphStatus status)
    {
        switch (status)
        {
            case RoomGraphStatus.Invalid:
                return "Invalid";
            case RoomGraphStatus.Warning:
                return "Warnings";
            default:
                return "Valid";
        }
    }

    internal static bool IsEdgeIncidentToNode(RoomGraphEdge edge, RoomGraphNode node)
    {
        return RoomConnectivityGraphNavigation.IsEdgeIncidentToNode(edge, node);
    }

    internal static bool IsEdgeDrawn(
        RoomGraphEdge edge,
        RoomConnectivityGraphLayoutResult layout)
    {
        if (edge == null || edge.Connection == null || layout == null)
            return false;

        return layout.EdgeGeometries.ContainsKey(edge.Connection);
    }

    internal static List<RoomGraphEdge> GetUnresolvedEdges(
        RoomConnectivityGraphModel model,
        RoomConnectivityGraphLayoutResult layout)
    {
        List<RoomGraphEdge> result = new List<RoomGraphEdge>();

        if (model == null)
            return result;

        foreach (RoomGraphEdge edge in model.Edges)
        {
            if (edge.IsSelfConnection || !edge.EndpointsResolved || !IsEdgeDrawn(edge, layout))
                result.Add(edge);
        }

        return result;
    }

    internal static string GetUnresolvedReason(
        RoomGraphEdge edge,
        RoomConnectivityGraphModel model)
    {
        if (edge == null)
            return "Unknown connection.";

        if (edge.IsSelfConnection && edge.EndpointsResolved)
            return "Self-connection (both endpoints resolve to the same room).";

        List<string> reasons = new List<string>();

        if (edge.IsSelfConnection)
            reasons.Add("Self-connection (both endpoints resolve to the same room)");

        AddEndpointReason(reasons, edge, model, true);
        AddEndpointReason(reasons, edge, model, false);

        if (reasons.Count == 0)
            return "Not drawn by the current layout.";

        return string.Join("; ", reasons) + ".";
    }

    private static void AddEndpointReason(
        List<string> reasons,
        RoomGraphEdge edge,
        RoomConnectivityGraphModel model,
        bool endpointA)
    {
        string label = endpointA ? "A" : "B";
        string roomId = endpointA ? edge.EndpointARoomId : edge.EndpointBRoomId;
        string doorId = endpointA ? edge.EndpointADoorId : edge.EndpointBDoorId;
        RoomGraphNode node = endpointA ? edge.SourceNode : edge.TargetNode;
        RoomGraphDoor door = endpointA ? edge.EndpointADoor : edge.EndpointBDoor;

        if (node == null)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                reasons.Add($"Endpoint {label} has a missing room ID");
                return;
            }

            int matches = CountNodesByRoomId(model, roomId);

            if (matches == 0)
                reasons.Add($"Endpoint {label} references unknown room ID '{roomId}'");
            else
                reasons.Add($"Endpoint {label} references ambiguous room ID '{roomId}' ({matches} RoomDefinitions)");

            return;
        }

        if (door != null)
            return;

        if (string.IsNullOrWhiteSpace(doorId))
        {
            reasons.Add($"Endpoint {label} has a missing door ID");
            return;
        }

        int doorMatches = CountDoors(node, doorId);

        if (doorMatches == 0)
            reasons.Add($"Endpoint {label} references missing door '{doorId}' in room '{roomId}'");
        else
            reasons.Add($"Endpoint {label} references ambiguous door ID '{doorId}' ({doorMatches} doors) in room '{roomId}'");
    }

    private static int CountNodesByRoomId(
        RoomConnectivityGraphModel model,
        string roomId)
    {
        if (model == null || string.IsNullOrEmpty(roomId))
            return 0;

        int count = 0;

        foreach (RoomGraphNode node in model.Nodes)
        {
            if (string.Equals(node.RoomId, roomId, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    private static int CountDoors(RoomGraphNode node, string doorId)
    {
        if (node == null || string.IsNullOrEmpty(doorId))
            return 0;

        int count = 0;

        foreach (RoomGraphDoor door in node.Doors)
        {
            if (string.Equals(door.StableDoorId, doorId, StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    internal static string GetEndpointResolution(RoomGraphEdge edge, bool endpointA)
    {
        RoomGraphNode node = endpointA ? edge.SourceNode : edge.TargetNode;
        RoomGraphDoor door = endpointA ? edge.EndpointADoor : edge.EndpointBDoor;
        string roomId = endpointA ? edge.EndpointARoomId : edge.EndpointBRoomId;
        string doorId = endpointA ? edge.EndpointADoorId : edge.EndpointBDoorId;

        string room = node != null
            ? $"resolved room '{node.DisplayName}'"
            : $"unresolved room '{RawId(roomId)}'";

        string doorText = door != null
            ? $"resolved door '{RawId(door.StableDoorId)}'"
            : $"unresolved door '{RawId(doorId)}'";

        return $"{room}, {doorText}";
    }

    internal static string GetIssueCodes(RoomGraphEdge edge)
    {
        if (edge == null)
            return string.Empty;

        List<string> codes = new List<string>();

        foreach (RoomGraphIssue issue in edge.Issues)
        {
            if (!codes.Contains(issue.RuleId))
                codes.Add(issue.RuleId);
        }

        return string.Join(", ", codes);
    }

    private static string RawId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<missing>" : value;
    }

    internal static List<string> GetDoorDiagnosticLines(RoomGraphDoor door)
    {
        List<string> lines = new List<string>();

        if (door == null)
            return lines;

        lines.Add($"Topology: {door.ReferenceCount} reference(s)");

        if (door.HasMultipleReferences)
            lines.Add($"Multiple topology references: {door.ReferenceCount}");

        if (door.InvolvedInInvalidConnection)
            lines.Add($"Invalid topology references: {door.InvalidReferenceCount}");

        if (door.HasBacklink)
            lines.Add($"Backlink: {door.Connection.name}");
        else
            lines.Add("Backlink: missing");

        if (door.BacklinkMatchesTopology)
        {
            lines.Add("Backlink consistency: consistent");
        }
        else if (door.HasBacklink)
        {
            List<string> states = new List<string>();

            if (door.BacklinkMismatched)
                states.Add("mismatched");

            if (door.BacklinkOrphaned)
                states.Add("orphaned");

            lines.Add("Backlink consistency: " + string.Join(" + ", states));
        }
        else if (door.ReferencedByTopology)
        {
            lines.Add("Backlink consistency: missing");
        }
        else
        {
            lines.Add("Backlink consistency: not applicable");
        }

        if (door.IsMissingId)
            lines.Add("Identity: missing doorId");

        if (door.IsDuplicateId)
            lines.Add("Identity: duplicate doorId");

        if (door.IsFullyUnconnected)
            lines.Add("Fully unconnected");

        return lines;
    }

    private static Color DoorDiagnosticColor(string line)
    {
        if (line.StartsWith("Identity:", StringComparison.Ordinal) ||
            line.StartsWith("Invalid topology references:", StringComparison.Ordinal))
        {
            return new Color(0.95f, 0.45f, 0.42f);
        }

        if (line.StartsWith("Backlink consistency:", StringComparison.Ordinal))
        {
            if (line.Contains("consistent"))
                return new Color(0.5f, 0.85f, 0.62f);

            if (line.Contains("not applicable"))
                return new Color(0.78f, 0.8f, 0.82f);

            return new Color(0.95f, 0.45f, 0.42f);
        }

        if (line.StartsWith("Backlink:", StringComparison.Ordinal))
        {
            return line.Contains("missing")
                ? new Color(0.9f, 0.78f, 0.4f)
                : new Color(0.78f, 0.8f, 0.82f);
        }

        if (line.StartsWith("Fully unconnected", StringComparison.Ordinal) ||
            line.StartsWith("Multiple topology references", StringComparison.Ordinal))
        {
            return new Color(0.9f, 0.78f, 0.4f);
        }

        return new Color(0.78f, 0.8f, 0.82f);
    }

    private void AddIssue(RoomGraphIssue issue)
    {
        AddIssue(_sidePanel, issue);
    }

    private void AddIssue(VisualElement parent, RoomGraphIssue issue)
    {
        if (parent == null || issue == null)
            return;

        VisualElement container = new VisualElement();
        container.style.marginBottom = 6;
        container.style.paddingLeft = 6;
        container.style.paddingRight = 6;
        container.style.paddingTop = 4;
        container.style.paddingBottom = 4;
        container.style.borderLeftWidth = 3;
        container.style.borderLeftColor = issue.IsError
            ? new Color(0.85f, 0.3f, 0.28f)
            : new Color(0.9f, 0.75f, 0.3f);
        container.style.backgroundColor = new Color(0f, 0f, 0f, 0.18f);

        Label label = new Label($"{issue.RuleId}: {issue.Message}");
        label.style.fontSize = 10;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.color = new Color(0.88f, 0.88f, 0.88f);
        container.Add(label);

        if (_model != null && issue.Context is RoomConnection connection)
        {
            RoomGraphEdge edge = _model.FindEdge(connection);

            if (edge != null)
            {
                RoomGraphEdge captured = edge;
                container.Add(MakeButton("Select Connection", () => SelectEdge(captured)));
            }
        }
        else if (_model != null && issue.Context is RoomDefinition definition)
        {
            RoomGraphNode node = _model.FindNode(definition);

            if (node != null)
            {
                RoomGraphNode captured = node;
                container.Add(MakeButton("Select Room", () => SelectNode(captured)));
            }
        }

        parent.Add(container);
    }

    private void SelectValidationIssues()
    {
        RefreshModel();

        _sidePanel.Add(InfoLabel(
            "Validation refreshed. Select an invalid node or connection to inspect its rule IDs."));
    }

    private void OpenRoomScene(RoomGraphNode node)
    {
        if (node == null || !node.SceneAvailable)
        {
            Debug.LogWarning("[RoomGraph] The selected room has no resolvable scene path.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(node.ScenePath, OpenSceneMode.Single);
    }

    private void LocateDoor(RoomGraphDoor door)
    {
        if (door == null || string.IsNullOrEmpty(door.ScenePath))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(door.ScenePath, OpenSceneMode.Single);
        RoomTransition match = null;
        int matches = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RoomTransition transition in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
            {
                bool identityMatches =
                    !string.IsNullOrWhiteSpace(door.StableDoorId) &&
                    string.Equals(transition.DoorId, door.StableDoorId, StringComparison.Ordinal);

                bool nameFallback =
                    string.IsNullOrWhiteSpace(door.StableDoorId) &&
                    string.Equals(transition.gameObject.name, door.GameObjectName, StringComparison.Ordinal);

                if (!identityMatches && !nameFallback)
                    continue;

                match = transition;
                matches++;
            }
        }

        if (match == null)
        {
            Debug.LogWarning($"[RoomGraph] Could not locate door '{door.StableDoorId}' in '{door.ScenePath}'.");
            return;
        }

        if (matches > 1)
        {
            Debug.LogWarning(
                $"[RoomGraph] Door '{door.StableDoorId}' is duplicated in '{door.ScenePath}'. Selecting the first match.");
        }

        Selection.activeGameObject = match.gameObject;
        EditorGUIUtility.PingObject(match.gameObject);

        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();
    }

    private static void PingObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
    }

    private static Button MakeButton(string text, Action action)
    {
        Button button = new Button(action) { text = text };
        button.style.marginRight = 4;
        button.style.marginBottom = 4;
        button.style.height = 22;
        return button;
    }

    private static Label SectionTitle(string text)
    {
        Label label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 13;
        label.style.marginTop = 10;
        label.style.marginBottom = 5;
        label.style.color = new Color(0.92f, 0.92f, 0.92f);
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static Label InfoLabel(string text)
    {
        Label label = new Label(text);
        label.style.fontSize = 11;
        label.style.marginBottom = 4;
        label.style.color = new Color(0.78f, 0.8f, 0.82f);
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static Label StatusLabel(string text, RoomGraphStatus status)
    {
        Label label = new Label(text);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 11;
        label.style.marginBottom = 6;
        label.style.color = StatusColor(status);
        return label;
    }

    private static VisualElement HelpBox(string text, HelpBoxMessageType type)
    {
        HelpBox box = new HelpBox(text, type);
        box.style.marginBottom = 6;
        return box;
    }

    private static Color StatusColor(RoomGraphStatus status)
    {
        switch (status)
        {
            case RoomGraphStatus.Invalid:
                return new Color(0.95f, 0.45f, 0.42f);
            case RoomGraphStatus.Warning:
                return new Color(0.92f, 0.78f, 0.38f);
            default:
                return new Color(0.5f, 0.85f, 0.62f);
        }
    }

    private static string Shorten(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<none>";

        return value.Length <= 14 ? value : value.Substring(0, 13) + "…";
    }
}
