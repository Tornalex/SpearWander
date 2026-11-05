using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Isolated tests for Phase 5.1B manual graph layout.
///
/// The project has no test assembly definition, so these tests compile into the
/// existing editor assembly and are exposed through a menu item. They use
/// transient models and a temporary layout asset that is always deleted.
///
/// Run: Tools/Project Spear/Rooms/Tests/Run Layout &amp; Drag Tests
/// Batch: -executeMethod RoomConnectivityGraphLayoutTests.RunAllBatch
/// </summary>
public static class RoomConnectivityGraphLayoutTests
{
    private const string ReportFileName = "RoomConnectivityGraphLayoutTests.log";
    private const string TempAssetPath = "Assets/__RoomConnectivityGraphLayoutTests.asset";

    private sealed class AssertException : Exception
    {
        public AssertException(string message) : base(message) { }
    }

    private sealed class Fixture : IDisposable
    {
        public RoomConnectivityGraphModel Model;
        public RoomGraphNode Alpha;
        public RoomGraphNode Beta;
        public RoomGraphNode Gamma;
        public RoomGraphEdge Edge;

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        public static Fixture Create()
        {
            Fixture fixture = new Fixture();
            fixture.Build();
            return fixture;
        }

        private void Build()
        {
            Model = new RoomConnectivityGraphModel();

            RoomDefinition alphaDefinition = Definition("room-alpha", "Alpha Hall", "R_Alpha");
            RoomDefinition betaDefinition = Definition("room-beta", "Beta Depths", "R_Beta");

            Alpha = new RoomGraphNode(
                alphaDefinition,
                "Assets/Scenes/Rooms/R_Alpha.unity");
            Beta = new RoomGraphNode(
                betaDefinition,
                "Assets/Scenes/Rooms/R_Beta.unity");

            RoomConnection connection = ScriptableObject.CreateInstance<RoomConnection>();
            connection.name = "RC_Alpha_Beta";
            connection.Assign(
                "room-alpha",
                "A_North",
                "room-beta",
                "B_South",
                RoomConnectionDirection.Bidirectional);
            _created.Add(connection);

            Edge = new RoomGraphEdge(
                connection,
                "Assets/Data/Rooms/Connections/RC_Alpha_Beta.asset")
            {
                SourceNode = Alpha,
                TargetNode = Beta,
                EndpointsResolved = true
            };

            Model.Nodes.Add(Alpha);
            Model.Nodes.Add(Beta);
            Model.Edges.Add(Edge);
        }

        public RoomGraphNode AddGamma()
        {
            RoomDefinition gammaDefinition = Definition("room-gamma", "Gamma Ruins", "R_Gamma");
            Gamma = new RoomGraphNode(
                gammaDefinition,
                "Assets/Scenes/Rooms/R_Gamma.unity");
            Model.Nodes.Add(Gamma);
            return Gamma;
        }

        private RoomDefinition Definition(string id, string display, string scene)
        {
            RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
            definition.name = id;
            definition.AssignIdentity(
                id,
                display,
                scene,
                "Assets/Scenes/Rooms/" + scene + ".unity");
            _created.Add(definition);
            return definition;
        }

        public void Dispose()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    UnityEngine.Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }
    }

    [MenuItem("Tools/Project Spear/Rooms/Tests/Run Layout & Drag Tests")]
    public static void RunAllMenu()
    {
        RunAll(true);
    }

    /// <summary>
    /// Entry point for -executeMethod. Throws when a test fails so batch mode
    /// can report a non-zero exit code.
    /// </summary>
    public static void RunAllBatch()
    {
        int failed = RunAll(true);

        if (failed > 0)
            throw new Exception($"{failed} room graph layout test(s) failed.");
    }

    public static int RunAll(bool writeReport)
    {
        DeleteTempAsset();

        List<KeyValuePair<string, Action>> tests = new List<KeyValuePair<string, Action>>
        {
            Test("Empty layout uses deterministic fallback", EmptyLayoutUsesFallback),
            Test("Deterministic automatic positions", DeterministicAutomaticPositions),
            Test("Save, load and resolve a position", SaveLoadPosition),
            Test("Serialization round trip", SerializationRoundTrip),
            Test("Refresh preserves saved positions", RefreshPreservesPositions),
            Test("New rooms do not disturb saved positions", NewRoomKeepsSavedPositions),
            Test("Positions are matched by NodeKey only", PositionsUseNodeKeyOnly),
            Test("NodeKey change does not transfer a position", NodeKeyChangeDoesNotTransfer),
            Test("Stale entries are detected and kept", StaleEntriesAreKept),
            Test("Duplicate entries are deterministic", DuplicateEntriesDeterministic),
            Test("Invalid entries are ignored safely", InvalidEntriesAreIgnored),
            Test("Edge geometry follows a moved node", EdgeGeometryFollowsNode),
            Test("Pointer delta is zoom-aware", PointerDeltaIsZoomAware),
            Test("Click and drag separation helpers", ClickAndDragSeparation),
            Test("Single drag Undo and Redo", SingleDragUndoRedo),
            Test("Undo restores a previous position", UndoRestoresPreviousPosition),
            Test("Auto Layout changes positions", AutoLayoutChangesPositions),
            Test("Auto Layout Undo and Redo", AutoLayoutUndoRedo),
            Test("Reset Layout clears overrides", ResetLayoutClearsOverrides),
            Test("Reset Layout Undo and Redo", ResetLayoutUndoRedo),
            Test("Auto Layout on an empty graph does nothing", AutoLayoutEmptyGraph),
            Test("Reset Layout on an empty layout does nothing", ResetEmptyLayout),
            Test("Reading layout never mutates the asset", ReadingLayoutDoesNotMutate),
            Test("Search and filters do not touch layout data", SearchAndFiltersDoNotTouchLayout),
            Test("Layout types are editor-only", LayoutTypesAreEditorOnly),
            Test("Layout asset path is the designated path", LayoutAssetPathIsDesignated),
            Test("Shared layout asset loads", SharedLayoutAssetLoads)
        };

        int passed = 0;
        List<string> failures = new List<string>();

        try
        {
            foreach (KeyValuePair<string, Action> test in tests)
            {
                try
                {
                    test.Value();
                    passed++;
                    Debug.Log($"[RoomLayoutTests] PASS {test.Key}");
                }
                catch (Exception exception)
                {
                    failures.Add($"{test.Key}: {exception.Message}");
                    Debug.LogError($"[RoomLayoutTests] FAIL {test.Key}: {exception.Message}");
                }
            }
        }
        finally
        {
            DeleteTempAsset();
        }

        string summary =
            $"Room Connectivity Graph layout tests: {passed}/{tests.Count} passed, {failures.Count} failed.";

        if (failures.Count == 0)
            Debug.Log($"[RoomLayoutTests] {summary}");
        else
            Debug.LogError($"[RoomLayoutTests] {summary}");

        if (writeReport)
            WriteReport(summary, tests.Count, passed, failures);

        return failures.Count;
    }

    private static KeyValuePair<string, Action> Test(string name, Action action)
    {
        return new KeyValuePair<string, Action>(name, action);
    }

    private static void WriteReport(
        string summary,
        int total,
        int passed,
        List<string> failures)
    {
        try
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string directory = Path.Combine(projectRoot, "Temp");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(summary);
            builder.AppendLine($"Total: {total}, Passed: {passed}, Failed: {failures.Count}");

            if (failures.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Failures:");

                foreach (string failure in failures)
                    builder.AppendLine(failure);
            }

            File.WriteAllText(Path.Combine(directory, ReportFileName), builder.ToString());
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RoomLayoutTests] Could not write test report: {exception.Message}");
        }
    }

    // --------------------------------------------------------------- layout

    private static void EmptyLayoutUsesFallback()
    {
        using (Fixture fixture = Fixture.Create())
        {
            RoomConnectivityGraphLayoutResult baseline =
                RoomConnectivityGraphLayout.Compute(fixture.Model);
            RoomConnectivityGraphLayoutResult empty =
                RoomConnectivityGraphLayout.Compute(
                    fixture.Model,
                    new Dictionary<string, Vector2>());

            CheckRectsEqual(baseline, empty, "An empty layout must equal the automatic layout.");
            Check(RoomConnectivityGraphLayoutService.BuildPositions(null).Count == 0,
                "A null layout must produce no positions.");
        }
    }

    private static void DeterministicAutomaticPositions()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Dictionary<string, Vector2> first =
                RoomConnectivityGraphLayoutService.ComputeDeterministicPositions(fixture.Model);
            Dictionary<string, Vector2> second =
                RoomConnectivityGraphLayoutService.ComputeDeterministicPositions(fixture.Model);

            Check(first.Count == fixture.Model.Nodes.Count,
                "Every node must receive an automatic position.");
            Check(DictionaryEqual(first, second),
                "Automatic positions must be deterministic.");

            List<RoomGraphNode> sorted =
                RoomConnectivityGraphLayout.SortNodesForLayout(fixture.Model);

            for (int i = 0; i < sorted.Count; i++)
            {
                Vector2 expected =
                    RoomConnectivityGraphLayout.GetGridPosition(i, sorted.Count);
                Check(first[sorted[i].NodeKey] == expected,
                    "Automatic position does not match the deterministic grid.");
            }

            Check(RoomConnectivityGraphLayoutService.ComputeDeterministicPositions(null).Count == 0,
                "A null model must produce no automatic positions.");
        }
    }

    private static void SaveLoadPosition()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            Vector2 position = new Vector2(321.5f, 654.25f);
            Check(RoomConnectivityGraphLayoutService.SetPosition(asset, "key-a", position),
                "SetPosition returned false.");
            Check(RoomConnectivityGraphLayoutService.TryGetPosition(asset, "key-a", out Vector2 loaded),
                "TryGetPosition did not find the saved key.");
            Check(loaded == position, "Loaded position does not match the saved position.");

            Dictionary<string, Vector2> positions =
                RoomConnectivityGraphLayoutService.BuildPositions(asset);
            Check(positions.Count == 1 && positions["key-a"] == position,
                "BuildPositions did not return the saved position.");

            Check(!RoomConnectivityGraphLayoutService.TryGetPosition(asset, "missing", out _),
                "TryGetPosition must not resolve an unknown key.");
            Check(!RoomConnectivityGraphLayoutService.SetPosition(
                    asset,
                    "key-a",
                    new Vector2(float.NaN, 0f)),
                "Non-finite positions must be rejected.");
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void SerializationRoundTrip()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            Vector2 position = new Vector2(140f, 260f);
            RoomConnectivityGraphLayoutService.SetPosition(asset, "key-a", position);
            RoomConnectivityGraphLayoutService.Persist(asset);

            RoomConnectivityGraphLayoutAsset copy =
                ScriptableObject.CreateInstance<RoomConnectivityGraphLayoutAsset>();

            try
            {
                EditorUtility.CopySerialized(asset, copy);

                Check(RoomConnectivityGraphLayoutService.TryGetPosition(copy, "key-a", out Vector2 loaded),
                    "A serialized copy lost the layout entry.");
                Check(loaded == position,
                    "A serialized copy changed the layout position.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void RefreshPreservesPositions()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Dictionary<string, Vector2> positions =
                new Dictionary<string, Vector2>
                {
                    { fixture.Alpha.NodeKey, new Vector2(700f, 500f) }
                };

            RoomConnectivityGraphLayoutResult first =
                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);
            RoomConnectivityGraphLayoutResult second =
                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

            Check(first.NodeRects[fixture.Alpha].position == new Vector2(700f, 500f),
                "A saved position was not applied.");
            Check(first.NodeRects[fixture.Alpha] == second.NodeRects[fixture.Alpha],
                "Recomputing the layout moved a saved node.");
        }
    }

    private static void NewRoomKeepsSavedPositions()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Vector2 saved = new Vector2(900f, 700f);
            Dictionary<string, Vector2> positions =
                new Dictionary<string, Vector2>
                {
                    { fixture.Alpha.NodeKey, saved }
                };

            RoomConnectivityGraphLayoutResult before =
                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

            RoomGraphNode gamma = fixture.AddGamma();

            RoomConnectivityGraphLayoutResult after =
                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

            Check(before.NodeRects[fixture.Alpha] == after.NodeRects[fixture.Alpha],
                "Adding a new room moved an existing saved node.");
            Check(after.NodeRects[gamma].position != saved,
                "The new room reused another room's saved position.");
        }
    }

    private static void PositionsUseNodeKeyOnly()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Vector2 saved = new Vector2(880f, 620f);
            Dictionary<string, Vector2> positions =
                new Dictionary<string, Vector2>
                {
                    { fixture.Alpha.NodeKey, saved }
                };

            RoomConnectivityGraphLayoutResult layout =
                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

            Check(layout.NodeRects[fixture.Alpha].position == saved,
                "The correct node did not receive its saved position.");
            Check(layout.NodeRects[fixture.Beta].position != saved,
                "A different node received another node's position.");
            Check(layout.NodeRects[fixture.Alpha].position !=
                  RoomConnectivityGraphLayout.GetGridPosition(0, 2),
                "The saved position was not used.");
        }
    }

    private static void NodeKeyChangeDoesNotTransfer()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            RoomConnectivityGraphLayoutService.SetPosition(
                asset,
                "old-node-key",
                new Vector2(880f, 620f));

            using (Fixture fixture = Fixture.Create())
            {
                Dictionary<string, Vector2> positions =
                    RoomConnectivityGraphLayoutService.BuildPositions(asset);

                RoomConnectivityGraphLayoutResult layout =
                    RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

                Check(layout.NodeRects[fixture.Alpha].position != new Vector2(880f, 620f),
                    "A stale position was transferred to a different NodeKey.");

                List<string> stale = RoomConnectivityGraphLayoutService.FindStaleKeys(
                    asset,
                    new[] { fixture.Alpha.NodeKey, fixture.Beta.NodeKey });

                Check(stale.Count == 1 && stale[0] == "old-node-key",
                    "The stale entry was not reported.");
                Check(asset.EntryCount == 1,
                    "Reading a stale entry must not delete it.");
            }
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void StaleEntriesAreKept()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            RoomConnectivityGraphLayoutService.SetPosition(asset, "stale-a", new Vector2(10, 20));
            RoomConnectivityGraphLayoutService.SetPosition(asset, "stale-b", new Vector2(30, 40));

            using (Fixture fixture = Fixture.Create())
            {
                Dictionary<string, Vector2> positions =
                    RoomConnectivityGraphLayoutService.BuildPositions(asset);

                List<string> stale = RoomConnectivityGraphLayoutService.FindStaleKeys(
                    asset,
                    new[] { fixture.Alpha.NodeKey, fixture.Beta.NodeKey });

                Check(stale.Count == 2, "Expected two stale entries.");
                Check(positions.ContainsKey("stale-a") && positions.ContainsKey("stale-b"),
                    "Stale entries must remain readable.");
                Check(asset.EntryCount == 2,
                    "Stale entries must not be removed during a read.");

                fixture.Model.Nodes.Clear();
                fixture.Model.Nodes.Add(fixture.Alpha);

                List<string> staleAfterRemoval = RoomConnectivityGraphLayoutService.FindStaleKeys(
                    asset,
                    new[] { fixture.Alpha.NodeKey });

                Check(staleAfterRemoval.Count == 2,
                    "Removing rooms from the registry must not transfer or delete positions.");
            }
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void DuplicateEntriesDeterministic()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            asset.MutableEntries.Add(new RoomGraphLayoutEntry("dup", new Vector2(10f, 10f)));
            asset.MutableEntries.Add(new RoomGraphLayoutEntry("dup", new Vector2(20f, 20f)));

            Dictionary<string, Vector2> positions =
                RoomConnectivityGraphLayoutService.BuildPositions(asset);

            Check(positions["dup"] == new Vector2(10f, 10f),
                "The first duplicate entry must win deterministically.");

            List<string> duplicates =
                RoomConnectivityGraphLayoutService.FindDuplicateKeys(asset);

            Check(duplicates.Count == 1 && duplicates[0] == "dup",
                "Duplicate entries were not reported.");

            RoomConnectivityGraphLayoutService.SetPosition(asset, "dup", new Vector2(30f, 30f));

            Check(asset.EntryCount == 1,
                "Writing a position must collapse duplicate entries.");
            Check(RoomConnectivityGraphLayoutService.BuildPositions(asset)["dup"] ==
                  new Vector2(30f, 30f),
                "The collapsed entry has the wrong position.");
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void InvalidEntriesAreIgnored()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            asset.MutableEntries.Add(new RoomGraphLayoutEntry(string.Empty, new Vector2(1f, 2f)));
            asset.MutableEntries.Add(
                new RoomGraphLayoutEntry("non-finite", new Vector2(float.NaN, 5f)));

            Check(RoomConnectivityGraphLayoutService.CountInvalidEntries(asset) == 2,
                "Invalid entries were not counted.");
            Check(RoomConnectivityGraphLayoutService.BuildPositions(asset).Count == 1,
                "An empty NodeKey must be ignored when reading positions.");

            using (Fixture fixture = Fixture.Create())
            {
                Dictionary<string, Vector2> positions =
                    RoomConnectivityGraphLayoutService.BuildPositions(asset);
                positions[fixture.Alpha.NodeKey] = new Vector2(float.PositiveInfinity, 0f);

                RoomConnectivityGraphLayoutResult layout =
                    RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

                Check(layout.NodeRects[fixture.Alpha].position ==
                      RoomConnectivityGraphLayout.GetGridPosition(0, 2),
                    "A non-finite saved position must fall back to the grid.");
            }
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void EdgeGeometryFollowsNode()
    {
        using (Fixture fixture = Fixture.Create())
        {
            RoomConnectivityGraphLayoutResult before =
                RoomConnectivityGraphLayout.Compute(fixture.Model);

            RoomGraphEdgeGeometry beforeGeometry =
                before.EdgeGeometries[fixture.Edge.Connection];

            Vector2 start = beforeGeometry.Start;
            Vector2 midpoint = beforeGeometry.Midpoint;

            Dictionary<string, Vector2> positions =
                new Dictionary<string, Vector2>
                {
                    { fixture.Alpha.NodeKey, new Vector2(600f, 400f) }
                };

            RoomConnectivityGraphLayoutResult after =
                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

            RoomGraphEdgeGeometry afterGeometry =
                after.EdgeGeometries[fixture.Edge.Connection];

            Check(after.NodeRects[fixture.Alpha].position == new Vector2(600f, 400f),
                "The dragged node did not move.");
            Check(after.NodeRects[fixture.Beta] == before.NodeRects[fixture.Beta],
                "A non-dragged node moved.");
            Check(afterGeometry.Start != start,
                "The edge start did not follow the moved node.");
            Check(afterGeometry.Midpoint != midpoint,
                "The edge midpoint did not follow the moved node.");
            Check(afterGeometry.Edge == fixture.Edge,
                "Edge geometry no longer references the same edge.");
        }
    }

    private static void PointerDeltaIsZoomAware()
    {
        Vector2 panelDelta = new Vector2(20f, 10f);

        Check(RoomConnectivityGraphLayoutService.PanelDeltaToGraphDelta(panelDelta, 1f) ==
              panelDelta,
            "Zoom 1 must not change pointer deltas.");
        Check(RoomConnectivityGraphLayoutService.PanelDeltaToGraphDelta(panelDelta, 2f) ==
              new Vector2(10f, 5f),
            "Zoom 2 must halve pointer deltas.");
        Check(RoomConnectivityGraphLayoutService.PanelDeltaToGraphDelta(panelDelta, 0.5f) ==
              new Vector2(40f, 20f),
            "Zoom 0.5 must double pointer deltas.");
        Check(RoomConnectivityGraphLayoutService.PanelDeltaToGraphDelta(panelDelta, 0f) ==
              panelDelta,
            "An invalid zoom must fall back to the raw delta.");
    }

    private static void ClickAndDragSeparation()
    {
        Check(!RoomConnectivityGraphLayoutService.ExceedsDragThreshold(new Vector2(1f, 1f)),
            "A simple click must not start a drag.");
        Check(RoomConnectivityGraphLayoutService.ExceedsDragThreshold(new Vector2(4f, 0f)),
            "Movement above the threshold must start a drag.");
        Check(RoomConnectivityGraphLayoutService.ClampToGraph(new Vector2(-10f, -20f)) ==
              Vector2.zero,
            "Graph positions must not become negative.");
        Check(RoomConnectivityGraphLayoutService.ClampToGraph(new Vector2(10f, 20f)) ==
              new Vector2(10f, 20f),
            "Valid graph positions must be preserved.");
    }

    // --------------------------------------------------------------- undo

    private static void SingleDragUndoRedo()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            Vector2 original = new Vector2(100f, 200f);
            Vector2 moved = new Vector2(360f, 480f);

            RoomConnectivityGraphLayoutService.SetPosition(asset, "key-a", original);
            RoomConnectivityGraphLayoutService.Persist(asset);

            RoomConnectivityGraphLayoutService.RecordUndo(
                asset,
                RoomConnectivityGraphLayoutService.MoveUndoName);
            RoomConnectivityGraphLayoutService.SetPosition(asset, "key-a", moved);
            RoomConnectivityGraphLayoutService.Persist(asset);

            Undo.PerformUndo();

            Check(RoomConnectivityGraphLayoutService.TryGetPosition(asset, "key-a", out Vector2 undone),
                "Undo removed the layout entry.");
            Check(undone == original,
                "One Undo must restore the position recorded at drag start.");

            Undo.PerformRedo();

            Check(RoomConnectivityGraphLayoutService.TryGetPosition(asset, "key-a", out Vector2 redone),
                "Redo removed the layout entry.");
            Check(redone == moved,
                "Redo must restore the final dragged position.");
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void UndoRestoresPreviousPosition()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            RoomConnectivityGraphLayoutService.SetPosition(asset, "key-a", new Vector2(10f, 10f));
            RoomConnectivityGraphLayoutService.Persist(asset);

            RoomConnectivityGraphLayoutService.RecordUndo(
                asset,
                RoomConnectivityGraphLayoutService.MoveUndoName);
            RoomConnectivityGraphLayoutService.SetPosition(asset, "key-b", new Vector2(50f, 50f));
            RoomConnectivityGraphLayoutService.Persist(asset);

            Undo.PerformUndo();

            Dictionary<string, Vector2> afterUndo =
                RoomConnectivityGraphLayoutService.BuildPositions(asset);

            Check(afterUndo.Count == 1 && afterUndo["key-a"] == new Vector2(10f, 10f),
                "Undo must restore the exact pre-drag layout state.");

            Undo.PerformRedo();

            Dictionary<string, Vector2> afterRedo =
                RoomConnectivityGraphLayoutService.BuildPositions(asset);

            Check(afterRedo.Count == 2 &&
                  afterRedo["key-b"] == new Vector2(50f, 50f),
                "Redo must restore the post-drag layout state.");
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void AutoLayoutChangesPositions()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            using (Fixture fixture = Fixture.Create())
            {
                RoomConnectivityGraphLayoutService.SetPosition(
                    asset,
                    fixture.Alpha.NodeKey,
                    new Vector2(999f, 999f));
                RoomConnectivityGraphLayoutService.Persist(asset);

                Check(RoomConnectivityGraphLayoutService.AutoLayout(asset, fixture.Model),
                    "Auto Layout reported no change.");

                Dictionary<string, Vector2> expected =
                    RoomConnectivityGraphLayoutService.ComputeDeterministicPositions(
                        fixture.Model);

                Dictionary<string, Vector2> stored =
                    RoomConnectivityGraphLayoutService.BuildPositions(asset);

                Check(stored.Count == fixture.Model.Nodes.Count,
                    "Auto Layout did not store every current room.");
                Check(DictionaryEqual(stored, expected),
                    "Auto Layout did not store the deterministic positions.");

                Check(RoomConnectivityGraphLayoutService.AutoLayout(asset, fixture.Model),
                    "A repeated Auto Layout should still be valid.");
                Check(DictionaryEqual(
                        RoomConnectivityGraphLayoutService.BuildPositions(asset),
                        expected),
                    "Auto Layout is not deterministic.");
            }
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void AutoLayoutUndoRedo()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            using (Fixture fixture = Fixture.Create())
            {
                RoomConnectivityGraphLayoutService.SetPosition(
                    asset,
                    fixture.Alpha.NodeKey,
                    new Vector2(999f, 999f));
                RoomConnectivityGraphLayoutService.Persist(asset);

                Check(RoomConnectivityGraphLayoutService.AutoLayout(asset, fixture.Model),
                    "Auto Layout failed.");

                Undo.PerformUndo();

                Check(RoomConnectivityGraphLayoutService.TryGetPosition(
                        asset,
                        fixture.Alpha.NodeKey,
                        out Vector2 undone),
                    "Auto Layout Undo removed the entry.");
                Check(undone == new Vector2(999f, 999f),
                    "One Undo must revert the whole Auto Layout operation.");

                Undo.PerformRedo();

                Dictionary<string, Vector2> expected =
                    RoomConnectivityGraphLayoutService.ComputeDeterministicPositions(
                        fixture.Model);

                Check(DictionaryEqual(
                        RoomConnectivityGraphLayoutService.BuildPositions(asset),
                        expected),
                    "Redo must restore the Auto Layout result.");
            }
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void ResetLayoutClearsOverrides()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            using (Fixture fixture = Fixture.Create())
            {
                RoomConnectivityGraphLayoutService.SetPosition(
                    asset,
                    fixture.Alpha.NodeKey,
                    new Vector2(999f, 999f));
                RoomConnectivityGraphLayoutService.SetPosition(
                    asset,
                    fixture.Beta.NodeKey,
                    new Vector2(888f, 888f));
                RoomConnectivityGraphLayoutService.Persist(asset);

                Check(RoomConnectivityGraphLayoutService.ResetLayout(asset),
                    "Reset Layout reported no change.");
                Check(asset.EntryCount == 0,
                    "Reset Layout must clear all manual overrides.");

                RoomConnectivityGraphLayoutResult layout =
                    RoomConnectivityGraphLayout.Compute(
                        fixture.Model,
                        RoomConnectivityGraphLayoutService.BuildPositions(asset));

                Check(layout.NodeRects[fixture.Alpha].position ==
                      RoomConnectivityGraphLayout.GetGridPosition(0, 2),
                    "Reset Layout did not restore the deterministic default.");
            }
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void ResetLayoutUndoRedo()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            RoomConnectivityGraphLayoutService.SetPosition(
                asset,
                "key-a",
                new Vector2(100f, 100f));
            RoomConnectivityGraphLayoutService.SetPosition(
                asset,
                "key-b",
                new Vector2(200f, 200f));
            RoomConnectivityGraphLayoutService.Persist(asset);

            Check(RoomConnectivityGraphLayoutService.ResetLayout(asset),
                "Reset Layout failed.");
            Check(asset.EntryCount == 0,
                "Reset Layout did not clear the layout.");

            Undo.PerformUndo();

            Dictionary<string, Vector2> restored =
                RoomConnectivityGraphLayoutService.BuildPositions(asset);

            Check(restored.Count == 2 &&
                  restored["key-a"] == new Vector2(100f, 100f) &&
                  restored["key-b"] == new Vector2(200f, 200f),
                "One Undo must restore all cleared overrides.");

            Undo.PerformRedo();

            Check(asset.EntryCount == 0,
                "Redo must clear the overrides again.");
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void AutoLayoutEmptyGraph()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            RoomConnectivityGraphModel model = new RoomConnectivityGraphModel();

            Check(!RoomConnectivityGraphLayoutService.AutoLayout(asset, model),
                "Auto Layout must do nothing for an empty graph.");
            Check(asset.EntryCount == 0,
                "Auto Layout changed the asset for an empty graph.");
            Check(!RoomConnectivityGraphLayoutService.AutoLayout(asset, null),
                "Auto Layout must do nothing without a model.");
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void ResetEmptyLayout()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            Check(!RoomConnectivityGraphLayoutService.ResetLayout(asset),
                "Reset Layout must do nothing when there are no overrides.");
            Check(!RoomConnectivityGraphLayoutService.ResetLayout(null),
                "Reset Layout must do nothing without an asset.");
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void ReadingLayoutDoesNotMutate()
    {
        RoomConnectivityGraphLayoutAsset asset = CreateTempAsset();

        try
        {
            using (Fixture fixture = Fixture.Create())
            {
                RoomConnectivityGraphLayoutService.SetPosition(
                    asset,
                    fixture.Alpha.NodeKey,
                    new Vector2(300f, 300f));
                RoomConnectivityGraphLayoutService.SetPosition(
                    asset,
                    "stale-key",
                    new Vector2(400f, 400f));
                RoomConnectivityGraphLayoutService.Persist(asset);

                int entriesBefore = asset.EntryCount;

                Dictionary<string, Vector2> positions =
                    RoomConnectivityGraphLayoutService.BuildPositions(asset);

                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);
                RoomConnectivityGraphLayoutService.FindStaleKeys(
                    asset,
                    new[] { fixture.Alpha.NodeKey, fixture.Beta.NodeKey });
                RoomConnectivityGraphLayoutService.FindDuplicateKeys(asset);
                RoomConnectivityGraphLayoutService.CountInvalidEntries(asset);

                Check(asset.EntryCount == entriesBefore,
                    "Reading or computing the layout mutated the asset.");

                Check(RoomConnectivityGraphLayoutService.AutoLayout(asset, fixture.Model),
                    "Auto Layout failed during the integrity check.");
                Check(RoomConnectivityGraphLayoutService.ResetLayout(asset),
                    "Reset Layout failed during the integrity check.");
                Check(asset.EntryCount == 0,
                    "Reset Layout did not clear the asset.");
            }
        }
        finally
        {
            DeleteTempAsset();
        }
    }

    private static void SearchAndFiltersDoNotTouchLayout()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Dictionary<string, Vector2> positions =
                new Dictionary<string, Vector2>
                {
                    { fixture.Alpha.NodeKey, new Vector2(700f, 300f) },
                    { fixture.Beta.NodeKey, new Vector2(400f, 600f) }
                };

            Dictionary<string, Vector2> snapshot =
                new Dictionary<string, Vector2>(positions);

            RoomConnectivityGraphLayoutResult layout =
                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

            HashSet<RoomGraphNode> visible =
                RoomConnectivityGraphNavigation.ComputeVisibleRooms(
                    fixture.Model,
                    RoomGraphRoomFilter.AllRooms,
                    null);

            RoomConnectivityGraphLayoutResult afterFilter =
                RoomConnectivityGraphLayout.Compute(fixture.Model, positions);

            HashSet<RoomGraphNode> revealed =
                RoomConnectivityGraphNavigation.ComputeVisibleRooms(
                    fixture.Model,
                    RoomGraphRoomFilter.RoomsWithErrors,
                    new[] { fixture.Alpha });

            Check(visible.Contains(fixture.Alpha) && visible.Contains(fixture.Beta),
                "All Rooms filter hid a node.");
            Check(revealed.Contains(fixture.Alpha),
                "A temporary search reveal did not include the node.");
            Check(DictionaryEqual(positions, snapshot),
                "Search or filtering changed the layout position data.");
            Check(layout.NodeRects[fixture.Alpha] == afterFilter.NodeRects[fixture.Alpha] &&
                  layout.NodeRects[fixture.Beta] == afterFilter.NodeRects[fixture.Beta],
                "Filtering changed the node positions.");
            Check(layout.NodeRects[fixture.Alpha].position == new Vector2(700f, 300f),
                "A hidden or filtered node lost its manual position.");
        }
    }

    private static void LayoutTypesAreEditorOnly()
    {
        string assetAssembly =
            typeof(RoomConnectivityGraphLayoutAsset).Assembly.GetName().Name;

        Check(!string.IsNullOrEmpty(assetAssembly) && assetAssembly.Contains("Editor"),
            $"The layout asset type must live in the editor assembly, but was in '{assetAssembly}'.");

        Check(typeof(RoomGraphLayoutEntry).Assembly ==
              typeof(RoomConnectivityGraphLayoutAsset).Assembly,
            "The layout entry and asset must share the editor assembly.");
    }

    private static void LayoutAssetPathIsDesignated()
    {
        Check(RoomConnectivityGraphLayoutService.DefaultAssetPath ==
              "Assets/Data/Rooms/RoomConnectivityGraphLayout.asset",
            "The layout asset path does not match the Phase 5.1B contract.");
        Check(RoomConnectivityGraphLayoutService.DefaultAssetPath.EndsWith(".asset"),
            "The layout asset path must point to a Unity asset.");
    }

    private static void SharedLayoutAssetLoads()
    {
        RoomConnectivityGraphLayoutAsset asset =
            AssetDatabase.LoadAssetAtPath<RoomConnectivityGraphLayoutAsset>(
                RoomConnectivityGraphLayoutService.DefaultAssetPath);

        Check(asset != null,
            "The shared RoomConnectivityGraphLayout.asset could not be loaded. " +
            "Check its script GUID and meta file.");
        Check(asset.name == RoomConnectivityGraphLayoutService.AssetName,
            "The shared layout asset has an unexpected name.");
        Check(asset.Entries != null,
            "The shared layout asset has no serializable entry list.");
    }

    // --------------------------------------------------------------- helpers

    private static RoomConnectivityGraphLayoutAsset CreateTempAsset()
    {
        AssetDatabase.DeleteAsset(TempAssetPath);

        RoomConnectivityGraphLayoutAsset asset =
            ScriptableObject.CreateInstance<RoomConnectivityGraphLayoutAsset>();
        asset.name = "RoomConnectivityGraphLayoutTests";

        AssetDatabase.CreateAsset(asset, TempAssetPath);
        return asset;
    }

    private static void DeleteTempAsset()
    {
        AssetDatabase.DeleteAsset(TempAssetPath);
    }

    private static void CheckRectsEqual(
        RoomConnectivityGraphLayoutResult a,
        RoomConnectivityGraphLayoutResult b,
        string message)
    {
        Check(a.NodeRects.Count == b.NodeRects.Count, message);

        foreach (KeyValuePair<RoomGraphNode, Rect> pair in a.NodeRects)
        {
            Check(b.NodeRects.TryGetValue(pair.Key, out Rect other) && other == pair.Value,
                message);
        }
    }

    private static bool DictionaryEqual(
        Dictionary<string, Vector2> a,
        Dictionary<string, Vector2> b)
    {
        if (a.Count != b.Count)
            return false;

        foreach (KeyValuePair<string, Vector2> pair in a)
        {
            if (!b.TryGetValue(pair.Key, out Vector2 other) || other != pair.Value)
                return false;
        }

        return true;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new AssertException(message);
    }
}
