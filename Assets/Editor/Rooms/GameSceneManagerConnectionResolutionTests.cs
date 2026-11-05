using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NUnit.Framework;

[TestFixture]
public class GameSceneManagerConnectionResolutionTests
{
    private const string TreeBranchPath = "Assets/Scenes/Rooms/Yellow Forest/YellowForest_TreeBranch.unity";
    private const string ToMotherPath = "Assets/Scenes/Rooms/Yellow Forest/YellowForest_ToMOTHER.unity";

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> _temporaryObjects = new List<UnityEngine.Object>();
        private readonly List<Scene> _openedScenes = new List<Scene>();
        private readonly Scene _originalActiveScene;
        private readonly Scene _previewScene;

        public readonly GameSceneManager Manager;

        public Fixture()
        {
            _originalActiveScene = SceneManager.GetActiveScene();
            _previewScene = EditorSceneManager.NewPreviewScene();

            GameObject host = CreateDoorPrefabInstance(_previewScene, "Resolution Test Host");
            Manager = host.AddComponent<GameSceneManager>();
        }

        public Scene OpenRoom(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                _openedScenes.Add(scene);
            }

            RestoreOriginalActiveScene();
            return scene;
        }

        public RoomTransition FindDoor(Scene scene, string doorId)
        {
            RoomTransition found = null;
            int matches = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RoomTransition door in root.GetComponentsInChildren<RoomTransition>(includeInactive: true))
                {
                    if (string.Equals(door.DoorId, doorId, StringComparison.Ordinal))
                    {
                        found = door;
                        matches++;
                    }
                }
            }

            Assert.AreEqual(1, matches, $"Expected one door '{doorId}' in '{scene.path}'.");
            return found;
        }

        public RoomTransition CreateTransientDoor(
            string doorId,
            RoomConnection connection = null,
            bool includeSpawnPoint = true)
        {
            GameObject gameObject = CreateDoorPrefabInstance(_previewScene, "Transient " + doorId);
            RoomTransition door = gameObject.GetComponent<RoomTransition>();
            SerializedObject serializedDoor = new SerializedObject(door);
            serializedDoor.FindProperty("doorId").stringValue = doorId;
            serializedDoor.FindProperty("connection").objectReferenceValue = connection;
            if (!includeSpawnPoint)
                serializedDoor.FindProperty("_spawnPoint").objectReferenceValue = null;
            serializedDoor.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        public RoomConnection CreateTransientConnection(
            string endpointARoomId,
            string endpointADoorId,
            string endpointBRoomId,
            string endpointBDoorId)
        {
            RoomConnection connection = ScriptableObject.CreateInstance<RoomConnection>();
            connection.name = "Transient test connection";
            connection.Assign(
                endpointARoomId,
                endpointADoorId,
                endpointBRoomId,
                endpointBDoorId,
                RoomConnectionDirection.Bidirectional);
            _temporaryObjects.Add(connection);
            return connection;
        }

        public void Dispose()
        {
            for (int i = _temporaryObjects.Count - 1; i >= 0; i--)
            {
                if (_temporaryObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_temporaryObjects[i]);
            }

            RestoreOriginalActiveScene();

            for (int i = _openedScenes.Count - 1; i >= 0; i--)
            {
                Scene scene = _openedScenes[i];
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }

            if (_previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(_previewScene);
        }

        private GameObject CreateDoorPrefabInstance(Scene destinationScene, string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Door.prefab");
            if (prefab == null)
                throw new InvalidOperationException("Could not load Assets/Prefabs/Props/Door.prefab.");

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, destinationScene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Could not instantiate a Door into scene '{destinationScene.name}'.");

            instance.name = name;
            _temporaryObjects.Add(instance);
            return instance;
        }

        private void RestoreOriginalActiveScene()
        {
            if (_originalActiveScene.IsValid() && _originalActiveScene.isLoaded &&
                SceneManager.GetActiveScene() != _originalActiveScene)
            {
                SceneManager.SetActiveScene(_originalActiveScene);
            }
        }
    }

    [Test]
    public void ManagerResolvesConnectedDoorWithoutStartingTransition()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene sourceScene = fixture.OpenRoom(ToMotherPath);
            RoomTransition sourceDoor = fixture.FindDoor(sourceScene, "YellowForest_TreeBranch 1_Right");
            int sceneCountBefore = SceneManager.sceneCount;
            string currentRoomBefore = fixture.Manager.CurrentRoom;
            bool transitioningBefore = GetPrivateField<bool>(fixture.Manager, "_isTransitioning");
            string targetDoorBefore = GetPrivateField<string>(fixture.Manager, "_targetDoorID");

            RoomConnectionResolutionResult result = fixture.Manager.ResolveConnectedRoomTransition(sourceDoor);

            Assert.AreEqual(RoomConnectionResolutionStatus.DestinationValidationPending, result.Status, result.Message);
            Assert.AreEqual("room_79c3ecd5e9d4434a8a630e8283c5f1fb", result.DestinationRoom.RoomId);
            Assert.AreEqual("YellowForest_TreeBranch_TopLeft", result.DestinationDoorId);
            Assert.AreEqual(TreeBranchPath, result.DestinationScenePath);
            Assert.AreEqual(sceneCountBefore, SceneManager.sceneCount, "Resolution must not load or unload scenes.");
            Assert.AreEqual(currentRoomBefore, fixture.Manager.CurrentRoom, "Resolution must not change CurrentRoom.");
            Assert.AreEqual(transitioningBefore, GetPrivateField<bool>(fixture.Manager, "_isTransitioning"),
                "Resolution must not start a transition.");
            Assert.AreEqual(targetDoorBefore, GetPrivateField<string>(fixture.Manager, "_targetDoorID"),
                "Resolution must not mutate the legacy transition target.");
        }
    }

    [Test]
    public void ManagerResolvesBidirectionalRouteFromEitherEndpoint()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene treeBranch = fixture.OpenRoom(TreeBranchPath);
            RoomTransition endpointB = fixture.FindDoor(treeBranch, "YellowForest_TreeBranch_TopLeft");

            RoomConnectionResolutionResult result = fixture.Manager.ResolveConnectedRoomTransition(endpointB);

            Assert.AreEqual(RoomConnectionResolutionStatus.DestinationValidationPending, result.Status, result.Message);
            Assert.AreEqual("room_25763ab8ee804b3cbe7f51bb74708ae3", result.DestinationRoom.RoomId);
            Assert.AreEqual("YellowForest_TreeBranch 1_Right", result.DestinationDoorId);
            Assert.AreEqual(ToMotherPath, result.DestinationScenePath);
        }
    }

    [Test]
    public void ManagerClassifiesInitialSpawnAsMissingConnection()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene treeBranch = fixture.OpenRoom(TreeBranchPath);
            RoomTransition spawnDoor = fixture.FindDoor(treeBranch, "YellowForest_TreeBranch_Spawn");

            RoomConnectionResolutionResult result = fixture.Manager.ResolveConnectedRoomTransition(spawnDoor);

            Assert.AreEqual(RoomConnectionResolutionStatus.MissingConnection, result.Status, result.Message);
            StringAssert.Contains("room_79c3ecd5e9d4434a8a630e8283c5f1fb", result.Message);
            StringAssert.Contains("YellowForest_TreeBranch_Spawn", result.Message);
            Assert.IsTrue(string.IsNullOrEmpty(spawnDoor.TargetSceneName),
                "The initial Spawn door should not gain a legacy destination as a result of resolution.");
            Assert.IsNull(spawnDoor.Connection, "The initial Spawn door should remain unconnected.");
        }
    }

    [Test]
    public void SourceResolutionFailureDoesNotMutateCurrentRoomOrTransitionState()
    {
        using (Fixture fixture = new Fixture())
        {
            Scene treeBranch = fixture.OpenRoom(TreeBranchPath);
            RoomTransition spawnDoor = fixture.FindDoor(treeBranch, "YellowForest_TreeBranch_Spawn");
            SetPrivateField(fixture.Manager, "_currentRoomScene", "Unchanged Room");
            bool transitionBefore = GetPrivateField<bool>(fixture.Manager, "_isTransitioning");
            int sceneCountBefore = SceneManager.sceneCount;

            RoomConnectionResolutionResult result = fixture.Manager.ResolveConnectedRoomTransition(spawnDoor);

            Assert.AreEqual(RoomConnectionResolutionStatus.MissingConnection, result.Status, result.Message);
            Assert.AreEqual("Unchanged Room", fixture.Manager.CurrentRoom);
            Assert.AreEqual(transitionBefore, GetPrivateField<bool>(fixture.Manager, "_isTransitioning"));
            Assert.AreEqual(sceneCountBefore, SceneManager.sceneCount);
        }
    }

    [Test]
    public void AssignedConnectionAlwaysSelectsConnectedRouteAndUnconnectedDoorSelectsLegacyRoute()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection invalidConnection = fixture.CreateTransientConnection(
                "not-a-registered-room", "wrong-source", "also-not-registered", "wrong-destination");
            RoomTransition assignedDoor = fixture.CreateTransientDoor("invalid-connected-door", invalidConnection);
            RoomTransition legacyDoor = fixture.CreateTransientDoor("legacy-door");

            Assert.IsTrue(UsesConnectedTransition(assignedDoor),
                "An invalid assigned connection must fail within the connected path, never use legacy routing.");
            Assert.IsFalse(UsesConnectedTransition(legacyDoor),
                "A connection-less door must retain the legacy compatibility route.");
        }
    }

    [Test]
    public void ManagerReportsMissingOrInvalidCatalogExplicitly()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomTransition sourceDoor = fixture.CreateTransientDoor("test-source-door");

            RoomConnectionResolutionResult missing = InvokeWithCatalogLoader(
                sourceDoor,
                () => null);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidSourceRoom, missing.Status, missing.Message);
            StringAssert.Contains("RuntimeRoomCatalog", missing.Message);

            RuntimeRoomCatalog invalidCatalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
            try
            {
                RoomConnectionResolutionResult invalid = InvokeWithCatalogLoader(
                    sourceDoor,
                    () => invalidCatalog);
                Assert.AreEqual(RoomConnectionResolutionStatus.InvalidSourceRoom, invalid.Status, invalid.Message);
                StringAssert.Contains("invalid", invalid.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidCatalog);
            }
        }
    }

    [Test]
    public void DestinationValidationAllowsCommitOnlyForUniqueBacklinkedDoorWithSpawnPoint()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnectionResolutionResult pending = ResolveTreeBranchDestination(fixture, out RoomConnection connection);
            RoomTransition destinationDoor = fixture.CreateTransientDoor("YellowForest_TreeBranch_TopLeft", connection);

            bool valid = InvokeDestinationValidation(
                destinationDoor.gameObject.scene,
                pending,
                out RoomTransition validatedDoor,
                out Transform spawnPoint,
                out string error);

            Assert.IsTrue(valid, error);
            Assert.AreSame(destinationDoor, validatedDoor);
            Assert.AreSame(destinationDoor.SpawnPoint, spawnPoint);
        }
    }

    [Test]
    public void DestinationValidationRejectsMissingDestinationDoor()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnectionResolutionResult pending = ResolveTreeBranchDestination(fixture, out _);
            RoomTransition unrelatedDoor = fixture.CreateTransientDoor("not-the-destination-door");

            bool valid = InvokeDestinationValidation(
                unrelatedDoor.gameObject.scene,
                pending,
                out _,
                out _,
                out string error);

            Assert.IsFalse(valid);
            StringAssert.Contains("was not found", error);
        }
    }

    [Test]
    public void DestinationValidationRejectsDuplicateDestinationDoorIds()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnectionResolutionResult pending = ResolveTreeBranchDestination(fixture, out RoomConnection connection);
            RoomTransition first = fixture.CreateTransientDoor("YellowForest_TreeBranch_TopLeft", connection);
            fixture.CreateTransientDoor("YellowForest_TreeBranch_TopLeft", connection);

            bool valid = InvokeDestinationValidation(
                first.gameObject.scene,
                pending,
                out _,
                out _,
                out string error);

            Assert.IsFalse(valid);
            StringAssert.Contains("appears 2 times", error);
        }
    }

    [Test]
    public void DestinationValidationRejectsMissingSpawnPoint()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnectionResolutionResult pending = ResolveTreeBranchDestination(fixture, out RoomConnection connection);
            RoomTransition destinationDoor = fixture.CreateTransientDoor(
                "YellowForest_TreeBranch_TopLeft",
                connection,
                includeSpawnPoint: false);

            bool valid = InvokeDestinationValidation(
                destinationDoor.gameObject.scene,
                pending,
                out _,
                out _,
                out string error);

            Assert.IsFalse(valid);
            StringAssert.Contains("SpawnPoint", error);
        }
    }

    [Test]
    public void DestinationValidationRejectsBacklinkMismatch()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnectionResolutionResult pending = ResolveTreeBranchDestination(fixture, out _);
            RoomConnection wrongConnection = fixture.CreateTransientConnection(
                "room_25763ab8ee804b3cbe7f51bb74708ae3",
                "YellowForest_TreeBranch_TopLeft",
                "room_79c3ecd5e9d4434a8a630e8283c5f1fb",
                "YellowForest_TreeBranch 1_Right");
            RoomTransition destinationDoor = fixture.CreateTransientDoor(
                "YellowForest_TreeBranch_TopLeft",
                wrongConnection);

            bool valid = InvokeDestinationValidation(
                destinationDoor.gameObject.scene,
                pending,
                out _,
                out _,
                out string error);

            Assert.IsFalse(valid);
            StringAssert.Contains("backlink", error);
        }
    }

    private static RoomConnectionResolutionResult ResolveTreeBranchDestination(
        Fixture fixture,
        out RoomConnection connection)
    {
        Scene sourceScene = fixture.OpenRoom(ToMotherPath);
        RoomTransition sourceDoor = fixture.FindDoor(sourceScene, "YellowForest_TreeBranch 1_Right");
        connection = sourceDoor.Connection;
        RoomConnectionResolutionResult result = fixture.Manager.ResolveConnectedRoomTransition(sourceDoor);
        Assert.AreEqual(RoomConnectionResolutionStatus.DestinationValidationPending, result.Status, result.Message);
        return result;
    }

    private static bool InvokeDestinationValidation(
        Scene destinationScene,
        RoomConnectionResolutionResult pending,
        out RoomTransition destinationDoor,
        out Transform spawnPoint,
        out string error)
    {
        MethodInfo method = typeof(GameSceneManager).GetMethod(
            "TryValidateConnectedDestination",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "Connected destination validation helper was not found.");
        object[] arguments = { destinationScene, pending, null, null, null };
        bool valid = (bool)method.Invoke(null, arguments);
        destinationDoor = (RoomTransition)arguments[2];
        spawnPoint = (Transform)arguments[3];
        error = (string)arguments[4];
        return valid;
    }

    private static RoomConnectionResolutionResult InvokeWithCatalogLoader(
        RoomTransition sourceDoor,
        Func<RuntimeRoomCatalog> catalogLoader)
    {
        MethodInfo method = typeof(GameSceneManager).GetMethod(
            "ResolveConnectedRoomTransitionWithCatalogLoader",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "Manager catalog-loader seam was not found.");
        return (RoomConnectionResolutionResult)method.Invoke(null, new object[] { sourceDoor, catalogLoader });
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private manager field '{fieldName}'.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private manager field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static bool UsesConnectedTransition(RoomTransition sourceDoor)
    {
        MethodInfo method = typeof(GameSceneManager).GetMethod(
            "UsesConnectedTransition",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "Connected/legacy route selector was not found.");
        return (bool)method.Invoke(null, new object[] { sourceDoor });
    }
}
