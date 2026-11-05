using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Isolated, in-memory tests for Phase 5.1A search and filtering.
///
/// The project has no test assembly definition, so these tests are compiled
/// into the existing editor assembly and exposed through a menu item. They
/// build synthetic graph models with ScriptableObject instances that are never
/// saved, so no production asset, scene or registry is touched.
///
/// Run: Tools/Project Spear/Rooms/Tests/Run Navigation &amp; Filter Tests
/// Batch: -executeMethod RoomConnectivityGraphNavigationTests.RunAllBatch
/// </summary>
public static class RoomConnectivityGraphNavigationTests
{
    private const string ReportFileName = "RoomConnectivityGraphNavigationTests.log";

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
        public RoomGraphNode Zeta;
        public RoomGraphNode Delta;
        public RoomGraphNode Epsilon;
        public RoomGraphDoor AlphaNorth;
        public RoomGraphDoor AlphaUnused;
        public RoomGraphDoor BetaSouth;
        public RoomGraphDoor GammaWest;
        public RoomGraphDoor ZetaEast;
        public RoomGraphDoor EpsilonDoor;
        public RoomGraphEdge ValidEdge;
        public RoomGraphEdge InvalidEdge;
        public RoomGraphEdge UnresolvedEdge;

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
            RoomDefinition gammaDefinition = Definition("room-gamma", "Gamma Ruins", "R_Gamma");
            RoomDefinition zetaDefinition = Definition("room-zeta", "Zeta Vault", "R_Zeta");
            RoomDefinition deltaDefinition = Definition("room-delta", "Delta Watch", "R_Delta");
            RoomDefinition epsilonDefinition = Definition("room-epsilon", "Epsilon Rest", "R_Epsilon");

            Alpha = Node(alphaDefinition);
            Beta = Node(betaDefinition);
            Gamma = Node(gammaDefinition);
            Zeta = Node(zetaDefinition);
            Delta = Node(deltaDefinition);
            Epsilon = Node(epsilonDefinition);

            Model.Nodes.Add(Alpha);
            Model.Nodes.Add(Beta);
            Model.Nodes.Add(Gamma);
            Model.Nodes.Add(Zeta);
            Model.Nodes.Add(Delta);
            Model.Nodes.Add(Epsilon);

            AlphaNorth = Door(alphaDefinition, "A_North", "LEG-A", "Door_North");
            AlphaUnused = Door(alphaDefinition, "A_Unused", "OldUnused", "Door_Unused");
            BetaSouth = Door(betaDefinition, "B_South", "LEG-B", "Door_South");
            GammaWest = Door(gammaDefinition, "G_West", string.Empty, "Door_West");
            ZetaEast = Door(zetaDefinition, "Z_East", string.Empty, "Door_East");

            RoomConnection backlinkOnly = ScriptableObject.CreateInstance<RoomConnection>();
            backlinkOnly.name = "RC_Epsilon_BacklinkOnly";
            backlinkOnly.Assign(
                "room-epsilon",
                "E_Only",
                "room-alpha",
                "A_North",
                RoomConnectionDirection.Bidirectional);
            _created.Add(backlinkOnly);

            EpsilonDoor = new RoomGraphDoor(
                epsilonDefinition,
                "Assets/Scenes/Rooms/R_Epsilon.unity",
                "E_Only",
                string.Empty,
                "Door_Only",
                true,
                backlinkOnly);

            Alpha.Doors.Add(AlphaNorth);
            Alpha.Doors.Add(AlphaUnused);
            Beta.Doors.Add(BetaSouth);
            Gamma.Doors.Add(GammaWest);
            Zeta.Doors.Add(ZetaEast);
            Epsilon.Doors.Add(EpsilonDoor);

            Delta.AddIssue(new RoomGraphIssue(
                "TEST-W101",
                RoomValidationSeverity.Warning,
                "Delta has a warning.",
                deltaDefinition));

            ValidEdge = Edge(
                "RC_Alpha_Beta",
                "Assets/Data/Rooms/Connections/RC_Alpha_Beta.asset",
                alphaDefinition,
                betaDefinition,
                Alpha,
                Beta,
                AlphaNorth,
                BetaSouth);

            ValidEdge.AddIssue(new RoomGraphIssue(
                "TEST-W102",
                RoomValidationSeverity.Warning,
                "Valid connection has a warning.",
                ValidEdge.Connection));

            InvalidEdge = Edge(
                "RC_Gamma_Zeta",
                "Assets/Data/Rooms/Connections/RC_Gamma_Zeta.asset",
                gammaDefinition,
                zetaDefinition,
                Gamma,
                Zeta,
                GammaWest,
                ZetaEast);

            InvalidEdge.AddIssue(new RoomGraphIssue(
                "RC021",
                RoomValidationSeverity.Error,
                "Synthetic invalid connection.",
                InvalidEdge.Connection));

            UnresolvedEdge = Unresolved("RC_Broken_Unresolved", "Assets/Data/Rooms/Connections/RC_Broken_Unresolved.asset");

            Model.Edges.Add(ValidEdge);
            Model.Edges.Add(InvalidEdge);
            Model.Edges.Add(UnresolvedEdge);

            AlphaNorth.ReferencingEdges.Add(ValidEdge);
            BetaSouth.ReferencingEdges.Add(ValidEdge);
            GammaWest.ReferencingEdges.Add(InvalidEdge);
            ZetaEast.ReferencingEdges.Add(InvalidEdge);
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

        private RoomGraphNode Node(RoomDefinition definition)
        {
            return new RoomGraphNode(
                definition,
                "Assets/Scenes/Rooms/" + definition.SceneName + ".unity");
        }

        private RoomGraphDoor Door(
            RoomDefinition definition,
            string stableId,
            string legacyId,
            string gameObjectName)
        {
            return new RoomGraphDoor(
                definition,
                "Assets/Scenes/Rooms/" + definition.SceneName + ".unity",
                stableId,
                legacyId,
                gameObjectName,
                true,
                null);
        }

        private RoomGraphEdge Edge(
            string assetName,
            string assetPath,
            RoomDefinition roomA,
            RoomDefinition roomB,
            RoomGraphNode source,
            RoomGraphNode target,
            RoomGraphDoor doorA,
            RoomGraphDoor doorB)
        {
            RoomConnection connection = ScriptableObject.CreateInstance<RoomConnection>();
            connection.name = assetName;
            connection.Assign(
                roomA.RoomId,
                doorA.StableDoorId,
                roomB.RoomId,
                doorB.StableDoorId,
                RoomConnectionDirection.Bidirectional);
            _created.Add(connection);

            RoomGraphEdge edge = new RoomGraphEdge(connection, assetPath)
            {
                SourceNode = source,
                TargetNode = target,
                EndpointADoor = doorA,
                EndpointBDoor = doorB,
                EndpointsResolved = true
            };

            return edge;
        }

        private RoomGraphEdge Unresolved(string assetName, string assetPath)
        {
            RoomConnection connection = ScriptableObject.CreateInstance<RoomConnection>();
            connection.name = assetName;
            connection.Assign(
                "room-unknown",
                "missing-door-a",
                "room-unknown",
                "missing-door-b",
                RoomConnectionDirection.OneWay);
            _created.Add(connection);

            return new RoomGraphEdge(connection, assetPath)
            {
                EndpointsResolved = false
            };
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

    [MenuItem("Tools/Project Spear/Rooms/Tests/Run Navigation & Filter Tests")]
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
            throw new Exception($"{failed} room graph navigation test(s) failed.");
    }

    public static int RunAll(bool writeReport)
    {
        List<KeyValuePair<string, Action>> tests = new List<KeyValuePair<string, Action>>
        {
            Test("Search by room display name", SearchByRoomDisplayName),
            Test("Search by scene name", SearchBySceneName),
            Test("Search by room ID", SearchByRoomId),
            Test("Search by door ID", SearchByDoorId),
            Test("Search by GameObject name", SearchByGameObjectName),
            Test("Search by legacy door ID", SearchByLegacyDoorId),
            Test("Search by connection asset name", SearchByConnectionAssetName),
            Test("Search by connection asset path", SearchByConnectionAssetPath),
            Test("Case-insensitive and partial search", SearchCaseInsensitiveAndPartial),
            Test("Empty search", EmptySearch),
            Test("No results", NoResults),
            Test("Search ordering and references", SearchOrderingAndReferences),
            Test("All Rooms filter", AllRoomsFilter),
            Test("Connected Rooms filter", ConnectedRoomsFilter),
            Test("Rooms With Unconnected Doors filter", UnconnectedDoorsFilter),
            Test("Rooms With Errors filter", ErrorsFilter),
            Test("Rooms With Warnings filter", WarningsFilter),
            Test("Combined filters", CombinedFilters),
            Test("Filter normalization", FilterNormalization),
            Test("Invalid connection is not connected", InvalidConnectionIsNotConnected),
            Test("Backlinks alone do not infer connectivity", BacklinksAloneDoNotConnect),
            Test("Edge visibility follows endpoints", EdgeVisibilityFollowsEndpoints),
            Test("Filtering preserves model and unresolved edges", FilteringPreservesModel),
            Test("Search result revealed under filters", SearchRevealUnderFilters),
            Test("Clearing search restores filtered presentation", ClearingSearchRestoresFilters),
            Test("Selection and navigation helpers", SelectionAndNavigationHelpers),
            Test("Repeated searches do not rebuild the graph", RepeatedSearchesDoNotRebuild)
        };

        int passed = 0;
        List<string> failures = new List<string>();

        foreach (KeyValuePair<string, Action> test in tests)
        {
            try
            {
                test.Value();
                passed++;
                Debug.Log($"[RoomGraphTests] PASS {test.Key}");
            }
            catch (Exception exception)
            {
                failures.Add($"{test.Key}: {exception.Message}");
                Debug.LogError($"[RoomGraphTests] FAIL {test.Key}: {exception.Message}");
            }
        }

        string summary =
            $"Room Connectivity Graph navigation tests: {passed}/{tests.Count} passed, {failures.Count} failed.";

        if (failures.Count == 0)
            Debug.Log($"[RoomGraphTests] {summary}");
        else
            Debug.LogError($"[RoomGraphTests] {summary}");

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
            Debug.LogWarning($"[RoomGraphTests] Could not write test report: {exception.Message}");
        }
    }

    // ---------------------------------------------------------------- search

    private static void SearchByRoomDisplayName()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "Alpha Hall");

            Check(results.Count == 1, "Expected exactly one result.");
            Check(results[0].Kind == RoomGraphSearchResultKind.Room, "Expected a room result.");
            Check(ReferenceEquals(results[0].Room, fixture.Alpha), "Expected the Alpha node.");
        }
    }

    private static void SearchBySceneName()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "R_Beta");

            Check(results.Count == 1, "Expected exactly one result.");
            Check(ReferenceEquals(results[0].Room, fixture.Beta), "Expected the Beta node.");
        }
    }

    private static void SearchByRoomId()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "room-gamma");

            Check(results.Count == 1, "Expected exactly one result.");
            Check(ReferenceEquals(results[0].Room, fixture.Gamma), "Expected the Gamma node.");
        }
    }

    private static void SearchByDoorId()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "A_North");

            Check(results.Count == 1, "Expected exactly one result.");
            Check(results[0].Kind == RoomGraphSearchResultKind.Door, "Expected a door result.");
            Check(ReferenceEquals(results[0].Door, fixture.AlphaNorth), "Expected the A_North door.");
            Check(ReferenceEquals(results[0].Room, fixture.Alpha), "Expected the owning room.");
        }
    }

    private static void SearchByGameObjectName()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "Door_East");

            Check(results.Count == 1, "Expected exactly one result.");
            Check(ReferenceEquals(results[0].Door, fixture.ZetaEast), "Expected the Door_East door.");
            Check(ReferenceEquals(results[0].Room, fixture.Zeta), "Expected the owning room.");
        }
    }

    private static void SearchByLegacyDoorId()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "OldUnused");

            Check(results.Count == 1, "Expected exactly one result.");
            Check(ReferenceEquals(results[0].Door, fixture.AlphaUnused), "Expected the legacy door.");
        }
    }

    private static void SearchByConnectionAssetName()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "RC_Gamma_Zeta");

            Check(results.Count == 1, "Expected exactly one result.");
            Check(results[0].Kind == RoomGraphSearchResultKind.Connection, "Expected a connection result.");
            Check(ReferenceEquals(results[0].Connection, fixture.InvalidEdge), "Expected the gamma/zeta edge.");
        }
    }

    private static void SearchByConnectionAssetPath()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "Connections/RC_Broken");

            Check(results.Count == 1, "Expected exactly one result.");
            Check(ReferenceEquals(results[0].Connection, fixture.UnresolvedEdge), "Expected the unresolved edge.");
        }
    }

    private static void SearchCaseInsensitiveAndPartial()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> upper =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "aLpHa hAlL");
            List<RoomGraphSearchResult> partial =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "lpha Hal");

            Check(upper.Count == 1 && ReferenceEquals(upper[0].Room, fixture.Alpha), "Case-insensitive match failed.");
            Check(partial.Count == 1 && ReferenceEquals(partial[0].Room, fixture.Alpha), "Partial match failed.");
        }
    }

    private static void EmptySearch()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Check(RoomConnectivityGraphNavigation.Search(fixture.Model, string.Empty).Count == 0, "Empty query matched.");
            Check(RoomConnectivityGraphNavigation.Search(fixture.Model, "   ").Count == 0, "Whitespace query matched.");
            Check(RoomConnectivityGraphNavigation.Search(null, "alpha").Count == 0, "Null model matched.");
        }
    }

    private static void NoResults()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Check(RoomConnectivityGraphNavigation.Search(fixture.Model, "does-not-exist").Count == 0,
                "Unexpected results for an unknown query.");
        }
    }

    private static void SearchOrderingAndReferences()
    {
        using (Fixture fixture = Fixture.Create())
        {
            List<RoomGraphSearchResult> results =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "Door_");

            Check(results.Count == 6, $"Expected six door results, got {results.Count}.");

            for (int i = 1; i < results.Count; i++)
            {
                int comparison = string.CompareOrdinal(results[i - 1].Title, results[i].Title);
                Check(comparison <= 0, "Door results are not sorted by title.");
            }

            Check(results.TrueForAll(result => result.Kind == RoomGraphSearchResultKind.Door),
                "A non-door result appeared in a door-only query.");
        }
    }

    // --------------------------------------------------------------- filters

    private static void AllRoomsFilter()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> visible = Visible(fixture, RoomGraphRoomFilter.AllRooms);

            Check(visible.Count == 6, $"Expected six visible rooms, got {visible.Count}.");
        }
    }

    private static void ConnectedRoomsFilter()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> visible = Visible(fixture, RoomGraphRoomFilter.ConnectedRooms);

            Check(visible.Count == 2, $"Expected two connected rooms, got {visible.Count}.");
            Check(visible.Contains(fixture.Alpha), "Alpha should be connected.");
            Check(visible.Contains(fixture.Beta), "Beta should be connected.");
            Check(!visible.Contains(fixture.Gamma), "Gamma has only an invalid connection.");
        }
    }

    private static void UnconnectedDoorsFilter()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> visible =
                Visible(fixture, RoomGraphRoomFilter.RoomsWithUnconnectedDoors);

            Check(visible.Count == 2, $"Expected two rooms with unconnected doors, got {visible.Count}.");
            Check(visible.Contains(fixture.Alpha), "Alpha has an unreferenced door.");
            Check(visible.Contains(fixture.Epsilon), "Epsilon has a backlink-only door with no topology reference.");
            Check(!visible.Contains(fixture.Beta), "Beta doors are referenced by topology.");
        }
    }

    private static void ErrorsFilter()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> visible = Visible(fixture, RoomGraphRoomFilter.RoomsWithErrors);

            Check(visible.Count == 2, $"Expected two rooms with errors, got {visible.Count}.");
            Check(visible.Contains(fixture.Gamma), "Gamma has an invalid connection.");
            Check(visible.Contains(fixture.Zeta), "Zeta has an invalid connection.");
        }
    }

    private static void WarningsFilter()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> visible = Visible(fixture, RoomGraphRoomFilter.RoomsWithWarnings);

            Check(visible.Count == 3, $"Expected three rooms with warnings, got {visible.Count}.");
            Check(visible.Contains(fixture.Alpha), "Alpha has a warning on an incident connection.");
            Check(visible.Contains(fixture.Beta), "Beta has a warning on an incident connection.");
            Check(visible.Contains(fixture.Delta), "Delta has a room warning.");
        }
    }

    private static void CombinedFilters()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> problems = Visible(
                fixture,
                RoomGraphRoomFilter.RoomsWithErrors | RoomGraphRoomFilter.RoomsWithWarnings);

            Check(problems.Count == 5, $"Expected five problem rooms, got {problems.Count}.");

            HashSet<RoomGraphNode> connectedOrUnconnected = Visible(
                fixture,
                RoomGraphRoomFilter.ConnectedRooms |
                RoomGraphRoomFilter.RoomsWithUnconnectedDoors);

            Check(connectedOrUnconnected.Count == 3,
                "Connected union unconnected-door rooms should be Alpha, Beta and Epsilon.");

            HashSet<RoomGraphNode> connectedAndErrors = Visible(
                fixture,
                RoomGraphRoomFilter.ConnectedRooms | RoomGraphRoomFilter.RoomsWithErrors);

            Check(connectedAndErrors.Count == 4, "Expected the union of connected and error rooms.");
        }
    }

    private static void FilterNormalization()
    {
        Check(
            RoomConnectivityGraphNavigation.Normalize(RoomGraphRoomFilter.None) ==
            RoomGraphRoomFilter.AllRooms,
            "No specific filters should normalize to AllRooms.");

        Check(
            RoomConnectivityGraphNavigation.Normalize(
                RoomGraphRoomFilter.AllRooms | RoomGraphRoomFilter.RoomsWithErrors) ==
            RoomGraphRoomFilter.RoomsWithErrors,
            "AllRooms must be dropped when a specific filter is active.");

        Check(
            RoomConnectivityGraphNavigation.Normalize(
                RoomGraphRoomFilter.RoomsWithErrors | RoomGraphRoomFilter.RoomsWithWarnings) ==
            (RoomGraphRoomFilter.RoomsWithErrors | RoomGraphRoomFilter.RoomsWithWarnings),
            "Specific filters must remain combinable.");

        Check(
            !RoomConnectivityGraphNavigation.HasActiveSpecificFilters(RoomGraphRoomFilter.AllRooms),
            "AllRooms must not be treated as a specific filter.");
    }

    private static void InvalidConnectionIsNotConnected()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Check(!RoomConnectivityGraphNavigation.IsRoomConnected(fixture.Model, fixture.Gamma),
                "A room whose only connection is invalid must not be connected.");
            Check(!RoomConnectivityGraphNavigation.IsRoomConnected(fixture.Model, fixture.Zeta),
                "A room whose only connection is invalid must not be connected.");
            Check(RoomConnectivityGraphNavigation.IsRoomConnected(fixture.Model, fixture.Alpha),
                "Alpha should be connected by a valid topology edge.");
        }
    }

    private static void BacklinksAloneDoNotConnect()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Check(fixture.EpsilonDoor.HasBacklink, "The fixture door should have a scene backlink.");
            Check(!fixture.EpsilonDoor.ReferencedByTopology,
                "The fixture backlink must not create a topology reference.");
            Check(fixture.Epsilon.UnreferencedDoorCount == 1,
                "A backlink-only door must count as unreferenced topology.");
            Check(!RoomConnectivityGraphNavigation.IsRoomConnected(fixture.Model, fixture.Epsilon),
                "A backlink alone must not make a room connected.");
            Check(RoomConnectivityGraphNavigation.IsRoomWithUnconnectedDoors(fixture.Epsilon),
                "A backlink-only door must count as an unconnected door.");
        }
    }

    private static void EdgeVisibilityFollowsEndpoints()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> all = Visible(fixture, RoomGraphRoomFilter.AllRooms);
            HashSet<RoomGraphNode> connected = Visible(fixture, RoomGraphRoomFilter.ConnectedRooms);

            Check(RoomConnectivityGraphNavigation.IsEdgeVisible(fixture.ValidEdge, all),
                "The valid edge should be visible with all rooms.");
            Check(RoomConnectivityGraphNavigation.IsEdgeVisible(fixture.InvalidEdge, all),
                "The invalid edge should be visible with all rooms.");
            Check(!RoomConnectivityGraphNavigation.IsEdgeVisible(fixture.UnresolvedEdge, all),
                "An unresolved edge must not be drawn as a canvas edge.");

            Check(RoomConnectivityGraphNavigation.IsEdgeVisible(fixture.ValidEdge, connected),
                "The valid edge should be visible when both endpoints are visible.");
            Check(!RoomConnectivityGraphNavigation.IsEdgeVisible(fixture.InvalidEdge, connected),
                "An edge must be hidden when one endpoint is hidden.");
        }
    }

    private static void FilteringPreservesModel()
    {
        using (Fixture fixture = Fixture.Create())
        {
            int nodeCount = fixture.Model.Nodes.Count;
            int edgeCount = fixture.Model.Edges.Count;
            DateTime builtAt = fixture.Model.BuiltAtUtc;

            RoomConnectivityGraphLayoutResult layout =
                RoomConnectivityGraphLayout.Compute(fixture.Model);

            Visible(fixture, RoomGraphRoomFilter.RoomsWithErrors);
            Visible(fixture, RoomGraphRoomFilter.ConnectedRooms);

            List<RoomGraphEdge> unresolved =
                RoomConnectivityGraphWindow.GetUnresolvedEdges(fixture.Model, layout);

            Check(fixture.Model.Nodes.Count == nodeCount, "Filtering changed the node count.");
            Check(fixture.Model.Edges.Count == edgeCount, "Filtering changed the edge count.");
            Check(fixture.Model.BuiltAtUtc == builtAt, "Filtering rebuilt the model.");
            Check(unresolved.Count == 1, $"Expected one unresolved edge, got {unresolved.Count}.");
            Check(ReferenceEquals(unresolved[0], fixture.UnresolvedEdge),
                "The unresolved edge must remain accessible under filters.");
        }
    }

    private static void SearchRevealUnderFilters()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> visible = RoomConnectivityGraphNavigation.ComputeVisibleRooms(
                fixture.Model,
                RoomGraphRoomFilter.RoomsWithErrors,
                new[] { fixture.Alpha });

            Check(visible.Contains(fixture.Alpha), "A revealed search result must be visible.");
            Check(visible.Contains(fixture.Gamma), "The active filter must still apply.");
            Check(!visible.Contains(fixture.Beta), "Unrelated rooms must stay hidden.");
        }
    }

    private static void ClearingSearchRestoresFilters()
    {
        using (Fixture fixture = Fixture.Create())
        {
            HashSet<RoomGraphNode> filtered = Visible(fixture, RoomGraphRoomFilter.RoomsWithErrors);
            HashSet<RoomGraphNode> revealed = RoomConnectivityGraphNavigation.ComputeVisibleRooms(
                fixture.Model,
                RoomGraphRoomFilter.RoomsWithErrors,
                new[] { fixture.Alpha });

            Check(revealed.Contains(fixture.Alpha), "Reveal should include the search result.");
            Check(!filtered.Contains(fixture.Alpha), "Clearing the search should restore the filter.");
            Check(filtered.SetEquals(Visible(fixture, RoomGraphRoomFilter.RoomsWithErrors)),
                "Clearing the search must not change filter configuration.");
        }
    }

    private static void SelectionAndNavigationHelpers()
    {
        using (Fixture fixture = Fixture.Create())
        {
            Check(
                ReferenceEquals(
                    RoomConnectivityGraphWindow.RestoreNodeSelection(fixture.Model, fixture.Alpha.NodeKey),
                    fixture.Alpha),
                "Room selection could not be restored by NodeKey.");

            Check(
                RoomConnectivityGraphWindow.RestoreNodeSelection(fixture.Model, "missing") == null,
                "Unknown NodeKey should not resolve.");

            Check(
                RoomConnectivityGraphWindow.GetNodeStatus(fixture.Model, fixture.Gamma) ==
                RoomGraphStatus.Invalid,
                "Gamma should be classified Invalid through its incident connection.");

            Check(
                RoomConnectivityGraphWindow.GetNodeStatus(fixture.Model, fixture.Alpha) ==
                RoomGraphStatus.Warning,
                "Alpha should be classified as Warnings through its incident connection.");

            Check(
                RoomConnectivityGraphWindow.IsEdgeIncidentToNode(fixture.ValidEdge, fixture.Alpha),
                "The valid edge should be incident to Alpha.");
            Check(
                !RoomConnectivityGraphWindow.IsEdgeIncidentToNode(fixture.ValidEdge, fixture.Gamma),
                "The valid edge should not be incident to Gamma.");

            Check(
                RoomConnectivityGraphWindow.GetIssueCodes(fixture.InvalidEdge).Contains("RC021"),
                "Connection issue codes should still be reported.");

            List<string> doorLines =
                RoomConnectivityGraphWindow.GetDoorDiagnosticLines(fixture.AlphaUnused);

            Check(doorLines.Contains("Fully unconnected"),
                "Fully unconnected door diagnostics should still be reported.");

            Check(
                RoomConnectivityGraphWindow.GetUnresolvedReason(fixture.UnresolvedEdge, fixture.Model)
                    .Contains("unknown room"),
                "Unresolved connection reasons should still be reported.");
        }
    }

    private static void RepeatedSearchesDoNotRebuild()
    {
        using (Fixture fixture = Fixture.Create())
        {
            int nodeCount = fixture.Model.Nodes.Count;
            int edgeCount = fixture.Model.Edges.Count;
            DateTime builtAt = fixture.Model.BuiltAtUtc;

            List<RoomGraphSearchResult> first =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "Alpha");
            List<RoomGraphSearchResult> second =
                RoomConnectivityGraphNavigation.Search(fixture.Model, "Alpha");

            Check(first.Count == second.Count, "Repeated searches returned different result counts.");
            Check(first.Count > 0, "Expected at least one result.");

            for (int i = 0; i < first.Count; i++)
            {
                Check(ReferenceEquals(first[i].Room, second[i].Room), "Search results were rebuilt.");
                Check(ReferenceEquals(first[i].Connection, second[i].Connection), "Connection results were rebuilt.");
            }

            Check(fixture.Model.Nodes.Count == nodeCount, "Searching changed the node count.");
            Check(fixture.Model.Edges.Count == edgeCount, "Searching changed the edge count.");
            Check(fixture.Model.BuiltAtUtc == builtAt, "Searching rebuilt the graph model.");
        }
    }

    // --------------------------------------------------------------- helpers

    private static HashSet<RoomGraphNode> Visible(
        Fixture fixture,
        RoomGraphRoomFilter filters)
    {
        return RoomConnectivityGraphNavigation.ComputeVisibleRooms(
            fixture.Model,
            filters,
            null);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new AssertException(message);
    }
}
