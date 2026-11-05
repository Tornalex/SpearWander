using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using NUnit.Framework;

/// <summary>
/// In-memory tests for runtime catalog generation, validation and lookup.
/// These tests do not create or save project assets.
/// Run: Tools/Project Spear/Rooms/Tests/Run Runtime Room Catalog Tests
/// Batch: -executeMethod RuntimeRoomCatalogTests.RunAllBatch
/// </summary>
[TestFixture]
public class RuntimeRoomCatalogTests
{
    private sealed class TestFailure : Exception
    {
        public TestFailure(string message) : base(message) { }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly RoomRegistry Registry;
        public readonly Dictionary<string, string> PathsByGuid = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> GuidsByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly HashSet<string> ScenePaths = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> BuildPaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();

        public Fixture()
        {
            Registry = ScriptableObject.CreateInstance<RoomRegistry>();
            _objects.Add(Registry);
        }

        public RoomDefinition AddRoom(
            string roomId,
            string sceneGuid,
            string scenePath,
            bool sceneAsset = true,
            bool includedInBuild = true)
        {
            RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
            definition.name = string.IsNullOrEmpty(roomId) ? "TestRoom" : roomId;
            definition.AssignIdentity(roomId, definition.name, "ignored-cached-name", sceneGuid);
            _objects.Add(definition);
            Registry.Add(definition);

            if (!string.IsNullOrEmpty(sceneGuid) && !string.IsNullOrEmpty(scenePath))
            {
                PathsByGuid[sceneGuid] = scenePath;
                GuidsByPath[scenePath] = sceneGuid;
                if (sceneAsset)
                    ScenePaths.Add(scenePath);
                if (includedInBuild)
                    BuildPaths.Add(scenePath);
            }

            return definition;
        }

        public RuntimeRoomCatalogValidationReport Build(out List<RuntimeRoomCatalogEntry> entries)
        {
            return RuntimeRoomCatalogGenerator.ValidateSource(
                Registry,
                out entries,
                GuidToPath,
                PathToGuid,
                IsSceneAsset,
                IsPersistent,
                IsBuildIncluded);
        }

        public void Dispose()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }
        }

        private string GuidToPath(string guid)
        {
            return PathsByGuid.TryGetValue(guid, out string path) ? path : null;
        }

        private string PathToGuid(string path)
        {
            return GuidsByPath.TryGetValue(path, out string guid) ? guid : null;
        }

        private bool IsSceneAsset(string path) => ScenePaths.Contains(path);
        private bool IsBuildIncluded(string path) => BuildPaths.Contains(path);
        private bool IsPersistent(RoomDefinition definition) => definition != null;
    }

    [MenuItem("Tools/Project Spear/Rooms/Tests/Run Runtime Room Catalog Tests")]
    public static void RunAllMenu()
    {
        RunAllBatch();
    }

    public static void RunAllBatch()
    {
        RuntimeRoomCatalogTests suite = new RuntimeRoomCatalogTests();
        List<KeyValuePair<string, Action>> tests = new List<KeyValuePair<string, Action>>
        {
            Test("Deterministic ordering and lookup", suite.DeterministicOrderingAndLookup),
            Test("Unknown RoomId fails clearly", suite.UnknownRoomIdFailsClearly),
            Test("Missing RoomDefinition is reported", suite.MissingDefinitionIsReported),
            Test("Empty RoomId is reported", suite.EmptyRoomIdIsReported),
            Test("Duplicate RoomId is reported", suite.DuplicateRoomIdIsReported),
            Test("Missing scene GUID is reported", suite.MissingSceneGuidIsReported),
            Test("Unresolved scene GUID is reported", suite.UnresolvedSceneGuidIsReported),
            Test("Resolved path GUID mismatch is reported", suite.ResolvedPathGuidMismatchIsReported),
            Test("Non-scene asset path is reported", suite.NonSceneAssetIsReported),
            Test("Duplicate scene assignment is reported", suite.DuplicateSceneAssignmentIsReported),
            Test("Registered scene excluded from build is reported", suite.ExcludedBuildSceneIsReported),
            Test("Registry and definitions remain unchanged", suite.SourceAssetsRemainUnchanged),
            Test("Invalid catalog entries fail lookup", suite.InvalidCatalogEntriesFailLookup),
            Test("Incomplete catalog entries are rejected", suite.IncompleteCatalogEntriesAreRejected),
            Test("Exact scene path resolves a definition", suite.ExactScenePathResolvesDefinition),
            Test("Unknown scene path is rejected", suite.UnknownScenePathIsRejected),
            Test("Duplicate scene path is rejected", suite.DuplicateScenePathIsRejected)
        };

        int passed = 0;
        List<string> failures = new List<string>();

        foreach (KeyValuePair<string, Action> test in tests)
        {
            try
            {
                test.Value();
                passed++;
                Debug.Log("[RuntimeRoomCatalogTests] PASS " + test.Key);
            }
            catch (Exception exception)
            {
                failures.Add(test.Key + ": " + exception.Message);
                Debug.LogError("[RuntimeRoomCatalogTests] FAIL " + test.Key + ": " + exception);
            }
        }

        string summary =
            $"Runtime Room Catalog tests: {passed}/{tests.Count} passed, {failures.Count} failed.";
        if (failures.Count == 0)
            Debug.Log("[RuntimeRoomCatalogTests] " + summary);
        else
            Debug.LogError("[RuntimeRoomCatalogTests] " + summary + " " + string.Join(" | ", failures));

        if (failures.Count > 0)
            throw new Exception(summary);
    }

    [Test]
    public void DeterministicOrderingAndLookup()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomDefinition beta = fixture.AddRoom("room-beta", "guid-beta", "Assets/Rooms/Beta.unity");
            RoomDefinition alpha = fixture.AddRoom("room-alpha", "guid-alpha", "Assets/Rooms/Alpha.unity");

            RuntimeRoomCatalogValidationReport report = fixture.Build(out List<RuntimeRoomCatalogEntry> entries);
            Check(report.IsValid, "Valid source data was rejected: " + Describe(report));
            Check(entries.Count == 2, "Expected two catalog entries.");
            Check(entries[0].RoomId == "room-alpha" && entries[1].RoomId == "room-beta",
                "Entries were not sorted by RoomId ordinally.");
            Check(entries[0].ScenePath == "Assets/Rooms/Alpha.unity",
                "The exact scene path was not retained.");

            RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            try
            {
                catalog.SetGeneratedEntries(entries);
                Check(catalog.TryGetRoom("room-alpha", out RoomDefinition found, out string path),
                    "RoomId lookup failed.");
                Check(found == alpha && path == "Assets/Rooms/Alpha.unity",
                    "RoomId lookup returned the wrong definition or path.");
                Check(catalog.GetRoomDefinition("room-beta") == beta,
                    "GetRoomDefinition returned the wrong definition.");
                Check(catalog.GetScenePath("room-beta") == "Assets/Rooms/Beta.unity",
                    "GetScenePath returned the wrong path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }

    [Test]
    public void UnknownRoomIdFailsClearly()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-alpha", "guid-alpha", "Assets/Rooms/Alpha.unity");
            fixture.Build(out List<RuntimeRoomCatalogEntry> entries);

            RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            try
            {
                catalog.SetGeneratedEntries(entries);
                Check(!catalog.TryGetRoom("missing-room", out _, out _),
                    "TryGetRoom should return false for an unknown ID.");

                bool threw = false;
                try
                {
                    catalog.GetScenePath("missing-room");
                }
                catch (KeyNotFoundException exception)
                {
                    threw = exception.Message.Contains("missing-room");
                }

                Check(threw, "GetScenePath did not identify the unknown RoomId in its exception.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }

    [Test]
    public void MissingDefinitionIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            List<RoomDefinition> rooms = (List<RoomDefinition>)typeof(RoomRegistry)
                .GetField("rooms", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(fixture.Registry);
            rooms.Add(null);

            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT010"), "Missing RoomDefinition reference was not reported.");
        }
    }

    [Test]
    public void EmptyRoomIdIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom(string.Empty, "guid-a", "Assets/Rooms/A.unity");
            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT011"), "Empty RoomId was not reported.");
        }
    }

    [Test]
    public void DuplicateRoomIdIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-same", "guid-a", "Assets/Rooms/A.unity");
            fixture.AddRoom("room-same", "guid-b", "Assets/Rooms/B.unity");
            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT012"), "Duplicate RoomId was not reported.");
        }
    }

    [Test]
    public void MissingSceneGuidIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-a", string.Empty, string.Empty);
            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT014"), "Missing scene GUID was not reported.");
        }
    }

    [Test]
    public void UnresolvedSceneGuidIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-a", "missing-guid", string.Empty);
            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT016"), "Unresolved scene GUID was not reported.");
        }
    }

    [Test]
    public void ResolvedPathGuidMismatchIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-a", "registered-guid", "Assets/Rooms/A.unity");
            fixture.GuidsByPath["Assets/Rooms/A.unity"] = "different-guid";
            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT017"), "Path-to-GUID mismatch was not reported.");
        }
    }

    [Test]
    public void NonSceneAssetIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-a", "guid-a", "Assets/Rooms/NotAScene.asset", sceneAsset: false);
            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT018"), "Non-scene asset path was not reported.");
        }
    }

    [Test]
    public void DuplicateSceneAssignmentIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-a", "guid-a", "Assets/Rooms/A.unity");
            fixture.AddRoom("room-b", "guid-a", "Assets/Rooms/A.unity");
            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT015"), "Duplicate scene GUID assignment was not reported.");
            Check(HasCode(report, "RCAT019"), "Duplicate scene path assignment was not reported.");
        }
    }

    [Test]
    public void ExcludedBuildSceneIsReported()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-a", "guid-a", "Assets/Rooms/A.unity", includedInBuild: false);
            RuntimeRoomCatalogValidationReport report = fixture.Build(out _);
            Check(HasCode(report, "RCAT020"), "Registered scene excluded from Build Settings was not reported.");
        }
    }

    [Test]
    public void SourceAssetsRemainUnchanged()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomDefinition definition = fixture.AddRoom(
                "room-a", "guid-a", "Assets/Rooms/A.unity");
            int countBefore = fixture.Registry.Rooms.Count;
            string idBefore = definition.RoomId;
            string guidBefore = definition.SceneGuid;
            string nameBefore = definition.SceneName;

            fixture.Build(out _);

            Check(fixture.Registry.Rooms.Count == countBefore, "Validation changed the registry contents.");
            Check(definition.RoomId == idBefore, "Validation changed the RoomId.");
            Check(definition.SceneGuid == guidBefore, "Validation changed the SceneGuid.");
            Check(definition.SceneName == nameBefore, "Validation changed cached SceneName.");
        }
    }

    [Test]
    public void InvalidCatalogEntriesFailLookup()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomDefinition definition = fixture.AddRoom("room-a", "guid-a", "Assets/Rooms/A.unity");
            RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            try
            {
                catalog.SetGeneratedEntries(new List<RuntimeRoomCatalogEntry>
                {
                    new RuntimeRoomCatalogEntry(definition, "Assets/Rooms/A.unity"),
                    new RuntimeRoomCatalogEntry(definition, "Assets/Rooms/A.unity")
                });

                Check(!catalog.IsValid, "Duplicate catalog entries were accepted.");
                Check(catalog.ValidationError.Contains("more than once"),
                    "Invalid catalog did not explain the duplicate.");
                Check(!catalog.TryGetRoom("room-a", out _, out _),
                    "Lookup succeeded on an invalid catalog.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }

    [Test]
    public void IncompleteCatalogEntriesAreRejected()
    {
        RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
        try
        {
            catalog.SetGeneratedEntries(new List<RuntimeRoomCatalogEntry>
            {
                new RuntimeRoomCatalogEntry(null, "Assets/Rooms/A.unity")
            });
            Check(!catalog.IsValid && catalog.ValidationError.Contains("RoomDefinition"),
                "An entry with a missing RoomDefinition was accepted.");

            using (Fixture fixture = new Fixture())
            {
                RoomDefinition definition = fixture.AddRoom("room-a", "guid-a", "Assets/Rooms/A.unity");
                catalog.SetGeneratedEntries(new List<RuntimeRoomCatalogEntry>
                {
                    new RuntimeRoomCatalogEntry(definition, string.Empty)
                });
                Check(!catalog.IsValid && catalog.ValidationError.Contains("scene path"),
                    "An entry with an empty scene path was accepted.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void ExactScenePathResolvesDefinition()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomDefinition definition = fixture.AddRoom(
                "room-alpha", "guid-alpha", "Assets/Rooms/Subfolder/Alpha.unity");
            RuntimeRoomCatalogValidationReport report = fixture.Build(out List<RuntimeRoomCatalogEntry> entries);
            Check(report.IsValid, "Valid source data was rejected: " + Describe(report));

            RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            try
            {
                catalog.SetGeneratedEntries(entries);
                Check(catalog.TryGetRoomByScenePath(
                        "Assets/Rooms/Subfolder/Alpha.unity",
                        out RoomDefinition found),
                    "Exact registered scene path did not resolve.");
                Check(found == definition, "Scene path lookup returned the wrong RoomDefinition.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }

    [Test]
    public void UnknownScenePathIsRejected()
    {
        using (Fixture fixture = new Fixture())
        {
            fixture.AddRoom("room-alpha", "guid-alpha", "Assets/Rooms/Alpha.unity");
            fixture.Build(out List<RuntimeRoomCatalogEntry> entries);

            RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            try
            {
                catalog.SetGeneratedEntries(entries);
                Check(!catalog.TryGetRoomByScenePath("Assets/Rooms/Other/Alpha.unity", out _),
                    "A non-exact or unknown scene path resolved.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }

    [Test]
    public void DuplicateScenePathIsRejected()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomDefinition alpha = fixture.AddRoom("room-alpha", "guid-alpha", "Assets/Rooms/Alpha.unity");
            RoomDefinition beta = fixture.AddRoom("room-beta", "guid-beta", "Assets/Rooms/Beta.unity");

            RuntimeRoomCatalog catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            try
            {
                catalog.SetGeneratedEntries(new List<RuntimeRoomCatalogEntry>
                {
                    new RuntimeRoomCatalogEntry(alpha, "Assets/Rooms/Shared.unity"),
                    new RuntimeRoomCatalogEntry(beta, "Assets/Rooms/Shared.unity")
                });

                Check(!catalog.IsValid, "Catalog accepted duplicate scene paths.");
                Check(!catalog.TryGetRoomByScenePath("Assets/Rooms/Shared.unity", out _),
                    "Lookup resolved an ambiguous duplicate scene path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }

    private static bool HasCode(RuntimeRoomCatalogValidationReport report, string code)
    {
        foreach (RuntimeRoomCatalogValidationIssue issue in report.Issues)
        {
            if (string.Equals(issue.Code, code, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string Describe(RuntimeRoomCatalogValidationReport report)
    {
        List<string> messages = new List<string>();
        foreach (RuntimeRoomCatalogValidationIssue issue in report.Issues)
            messages.Add(issue.Code + ": " + issue.Message);
        return string.Join(" | ", messages);
    }

    private static KeyValuePair<string, Action> Test(string name, Action action)
    {
        return new KeyValuePair<string, Action>(name, action);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new TestFailure(message);
    }
}
