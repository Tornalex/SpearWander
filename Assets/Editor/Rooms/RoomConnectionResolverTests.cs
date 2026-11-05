using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NUnit.Framework;

[TestFixture]
public class RoomConnectionResolverTests
{
    private const string RoomAId = "test-room-a";
    private const string RoomBId = "test-room-b";
    private const string RoomAPath = "Assets/TestRooms/RoomA.unity";
    private const string RoomBPath = "Assets/TestRooms/RoomB.unity";
    private const string DoorAId = "test-door-a";
    private const string DoorBId = "test-door-b";

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private readonly Scene _testScene;
        private bool _disposed;

        public readonly RoomDefinition RoomA;
        public readonly RoomDefinition RoomB;
        public readonly RuntimeRoomCatalog Catalog;

        public Fixture(bool includeRoomB = true)
        {
            // Preview scenes are isolated from the user's open scene and do not
            // require an untitled scene to be saved before creating a scene.
            _testScene = EditorSceneManager.NewPreviewScene();
            try
            {
                RoomA = Definition(RoomAId, "Test Room A");
                RoomB = includeRoomB ? Definition(RoomBId, "Test Room B") : null;

                Catalog = ScriptableObject.CreateInstance<RuntimeRoomCatalog>();
                Catalog.name = "Transient Test Catalog";
                _objects.Add(Catalog);

                List<RuntimeRoomCatalogEntry> entries = new List<RuntimeRoomCatalogEntry>
                {
                    new RuntimeRoomCatalogEntry(RoomA, RoomAPath)
                };
                if (RoomB != null)
                    entries.Add(new RuntimeRoomCatalogEntry(RoomB, RoomBPath));

                Catalog.SetGeneratedEntries(entries);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public RoomTransition Door(string id, RoomConnection connection)
        {
            GameObject doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Door.prefab");
            if (doorPrefab == null)
                throw new InvalidOperationException("Could not load Assets/Prefabs/Props/Door.prefab for an isolated test door.");

            GameObject gameObject = PrefabUtility.InstantiatePrefab(doorPrefab, _testScene) as GameObject;
            if (gameObject == null)
                throw new InvalidOperationException("Could not instantiate the Door prefab into the preview scene.");

            gameObject.name = "Transient " + id;
            RoomTransition door = gameObject.GetComponent<RoomTransition>();
            if (door == null)
                throw new InvalidOperationException("The Door prefab has no RoomTransition component.");

            SetDoorFields(door, id, connection);
            _objects.Add(gameObject);
            return door;
        }

        public RoomConnection Connection(
            string roomAId,
            string doorAId,
            string roomBId,
            string doorBId,
            RoomConnectionDirection direction = RoomConnectionDirection.Bidirectional)
        {
            RoomConnection connection = RoomConnectionResolverTests.CreateConnection(
                roomAId, doorAId, roomBId, doorBId, direction);
            _objects.Add(connection);
            return connection;
        }

        public RoomConnectionResolutionResult ResolveFromA(RoomTransition source)
        {
            return RoomConnectionResolver.Resolve(source, Catalog, RoomAPath);
        }

        public RoomConnectionResolutionResult ResolveFromB(RoomTransition source)
        {
            return RoomConnectionResolver.Resolve(source, Catalog, RoomBPath);
        }

        public RoomConnectionResolutionResult ValidateDestination(
            RoomConnectionResolutionResult pending,
            string destinationPath,
            params RoomTransition[] doors)
        {
            return RoomConnectionResolver.ValidateDestinationBacklink(pending, destinationPath, doors);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                for (int i = _objects.Count - 1; i >= 0; i--)
                {
                    if (_objects[i] != null)
                        UnityEngine.Object.DestroyImmediate(_objects[i]);
                }
            }
            finally
            {
                if (_testScene.IsValid())
                    EditorSceneManager.ClosePreviewScene(_testScene);
            }
        }

        private RoomDefinition Definition(string id, string displayName)
        {
            RoomDefinition definition = ScriptableObject.CreateInstance<RoomDefinition>();
            definition.name = displayName;
            definition.AssignIdentity(id, displayName, displayName, "test-guid-" + id);
            _objects.Add(definition);
            return definition;
        }

        private static void SetDoorFields(RoomTransition door, string doorId, RoomConnection connection)
        {
            SerializedObject serializedDoor = new SerializedObject(door);
            serializedDoor.FindProperty("doorId").stringValue = doorId;
            serializedDoor.FindProperty("connection").objectReferenceValue = connection;
            serializedDoor.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    [Test]
    public void BidirectionalConnectionResolvesFromEndpointA()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, RoomBId, DoorBId);
            RoomTransition source = fixture.Door(DoorAId, connection);
            RoomTransition destination = fixture.Door(DoorBId, connection);

            RoomConnectionResolutionResult pending = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.DestinationValidationPending, pending.Status, pending.Message);
            Assert.AreSame(fixture.RoomB, pending.DestinationRoom);
            Assert.AreEqual(DoorBId, pending.DestinationDoorId);

            RoomConnectionResolutionResult resolved = fixture.ValidateDestination(pending, RoomBPath, destination);
            Assert.AreEqual(RoomConnectionResolutionStatus.Success, resolved.Status, resolved.Message);
        }
    }

    [Test]
    public void BidirectionalConnectionResolvesFromEndpointB()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, RoomBId, DoorBId);
            RoomTransition source = fixture.Door(DoorBId, connection);
            RoomTransition destination = fixture.Door(DoorAId, connection);

            RoomConnectionResolutionResult pending = fixture.ResolveFromB(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.DestinationValidationPending, pending.Status, pending.Message);
            Assert.AreSame(fixture.RoomA, pending.DestinationRoom);
            Assert.AreEqual(DoorAId, pending.DestinationDoorId);

            RoomConnectionResolutionResult resolved = fixture.ValidateDestination(pending, RoomAPath, destination);
            Assert.AreEqual(RoomConnectionResolutionStatus.Success, resolved.Status, resolved.Message);
        }
    }

    [Test]
    public void OneWayConnectionResolvesFromEndpointA()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(
                RoomAId, DoorAId, RoomBId, DoorBId, RoomConnectionDirection.OneWay);
            RoomTransition source = fixture.Door(DoorAId, connection);
            RoomTransition destination = fixture.Door(DoorBId, connection);

            RoomConnectionResolutionResult pending = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.DestinationValidationPending, pending.Status, pending.Message);
            Assert.AreEqual(DoorBId, pending.DestinationDoorId);

            RoomConnectionResolutionResult resolved = fixture.ValidateDestination(pending, RoomBPath, destination);
            Assert.AreEqual(RoomConnectionResolutionStatus.Success, resolved.Status, resolved.Message);
        }
    }

    [Test]
    public void OneWayConnectionRejectsEndpointBInitiation()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(
                RoomAId, DoorAId, RoomBId, DoorBId, RoomConnectionDirection.OneWay);
            RoomTransition source = fixture.Door(DoorBId, connection);

            RoomConnectionResolutionResult result = fixture.ResolveFromB(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.WrongDirection, result.Status, result.Message);
            StringAssert.Contains("Endpoint B", result.Message);
        }
    }

    [Test]
    public void NullConnectionIsClassifiedAsMissing()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomTransition source = fixture.Door(DoorAId, null);

            RoomConnectionResolutionResult result = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.MissingConnection, result.Status, result.Message);
            StringAssert.Contains(RoomAId, result.Message);
            StringAssert.Contains(DoorAId, result.Message);
        }
    }

    [Test]
    public void ConnectionWithUnmatchedSourceEndpointIsInvalid()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection("unregistered-source", DoorAId, RoomBId, DoorBId);
            RoomTransition source = fixture.Door(DoorAId, connection);

            RoomConnectionResolutionResult result = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidConnection, result.Status, result.Message);
            StringAssert.Contains(RoomAId + "/" + DoorAId, result.Message);
        }
    }

    [Test]
    public void UnknownDestinationRoomIsInvalid()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, "unknown-room", DoorBId);
            RoomTransition source = fixture.Door(DoorAId, connection);

            RoomConnectionResolutionResult result = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidDestinationRoom, result.Status, result.Message);
            StringAssert.Contains("unknown-room", result.Message);
        }
    }

    [Test]
    public void MissingDestinationRoomDefinitionIsInvalid()
    {
        using (Fixture fixture = new Fixture(includeRoomB: false))
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, RoomBId, DoorBId);
            RoomTransition source = fixture.Door(DoorAId, connection);

            RoomConnectionResolutionResult result = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidDestinationRoom, result.Status, result.Message);
            StringAssert.Contains(RoomBId, result.Message);
        }
    }

    [Test]
    public void EmptyDestinationDoorIdIsInvalid()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, RoomBId, string.Empty);
            RoomTransition source = fixture.Door(DoorAId, connection);

            RoomConnectionResolutionResult result = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidConnection, result.Status, result.Message);
            StringAssert.Contains("empty endpoint", result.Message);
        }
    }

    [Test]
    public void SourceBacklinkToConnectionWithDifferentEndpointIsInvalid()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection wrongConnection = fixture.Connection(RoomAId, "other-door", RoomBId, DoorBId);
            RoomTransition source = fixture.Door(DoorAId, wrongConnection);

            RoomConnectionResolutionResult result = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidConnection, result.Status, result.Message);
        }
    }

    [Test]
    public void DestinationBacklinkMismatchIsInvalid()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, RoomBId, DoorBId);
            RoomConnection otherConnection = fixture.Connection(RoomAId, "other-a", RoomBId, "other-b");
            RoomTransition source = fixture.Door(DoorAId, connection);
            RoomTransition destination = fixture.Door(DoorBId, otherConnection);

            RoomConnectionResolutionResult pending = fixture.ResolveFromA(source);
            RoomConnectionResolutionResult result = fixture.ValidateDestination(pending, RoomBPath, destination);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidDestinationRoom, result.Status, result.Message);
            StringAssert.Contains("backlink", result.Message);
        }
    }

    [Test]
    public void InvalidDestinationEndpointDoorIsRejected()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, RoomBId, DoorBId);
            RoomTransition source = fixture.Door(DoorAId, connection);
            RoomTransition wrongDestination = fixture.Door("other-destination-door", connection);

            RoomConnectionResolutionResult pending = fixture.ResolveFromA(source);
            RoomConnectionResolutionResult result = fixture.ValidateDestination(
                pending, RoomBPath, wrongDestination);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidDestinationRoom, result.Status, result.Message);
            StringAssert.Contains(DoorBId, result.Message);
        }
    }

    [Test]
    public void DestinationScenePathMustMatchCatalogPath()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, RoomBId, DoorBId);
            RoomTransition source = fixture.Door(DoorAId, connection);
            RoomTransition destination = fixture.Door(DoorBId, connection);

            RoomConnectionResolutionResult pending = fixture.ResolveFromA(source);
            RoomConnectionResolutionResult result = fixture.ValidateDestination(
                pending, "Assets/TestRooms/OtherRoom.unity", destination);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidDestinationRoom, result.Status, result.Message);
        }
    }

    [Test]
    public void MissingSourceRoomPathIsInvalid()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomTransition source = fixture.Door(DoorAId, null);

            RoomConnectionResolutionResult result =
                RoomConnectionResolver.Resolve(source, fixture.Catalog, "Assets/TestRooms/Unknown.unity");
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidSourceRoom, result.Status, result.Message);
        }
    }

    [Test]
    public void EmptySourceDoorIdIsInvalid()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, string.Empty, RoomBId, DoorBId);
            RoomTransition source = fixture.Door(string.Empty, connection);

            RoomConnectionResolutionResult result = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidSourceDoorId, result.Status, result.Message);
        }
    }

    [Test]
    public void DuplicateSourceDoorIdIsInvalid()
    {
        using (Fixture fixture = new Fixture())
        {
            RoomConnection connection = fixture.Connection(RoomAId, DoorAId, RoomBId, DoorBId);
            RoomTransition source = fixture.Door(DoorAId, connection);
            fixture.Door(DoorAId, connection);

            RoomConnectionResolutionResult result = fixture.ResolveFromA(source);
            Assert.AreEqual(RoomConnectionResolutionStatus.InvalidSourceDoorId, result.Status, result.Message);
            StringAssert.Contains("duplicated", result.Message);
        }
    }

    [Test]
    public void ParallelConnectionsResolveIndependently()
    {
        using (Fixture fixture = new Fixture())
        {
            const string sourceDoorA1 = "source-a-1";
            const string sourceDoorA2 = "source-a-2";
            const string destinationDoorB1 = "destination-b-1";
            const string destinationDoorB2 = "destination-b-2";

            RoomConnection connection1 = fixture.Connection(
                RoomAId, sourceDoorA1, RoomBId, destinationDoorB1);
            RoomConnection connection2 = fixture.Connection(
                RoomAId, sourceDoorA2, RoomBId, destinationDoorB2);
            RoomTransition source1 = fixture.Door(sourceDoorA1, connection1);
            RoomTransition source2 = fixture.Door(sourceDoorA2, connection2);
            RoomTransition destination1 = fixture.Door(destinationDoorB1, connection1);
            RoomTransition destination2 = fixture.Door(destinationDoorB2, connection2);

            RoomConnectionResolutionResult first = fixture.ResolveFromA(source1);
            RoomConnectionResolutionResult second = fixture.ResolveFromA(source2);
            Assert.AreEqual(destinationDoorB1, first.DestinationDoorId, first.Message);
            Assert.AreEqual(destinationDoorB2, second.DestinationDoorId, second.Message);
            Assert.AreSame(connection1, first.Connection);
            Assert.AreSame(connection2, second.Connection);

            Assert.AreEqual(RoomConnectionResolutionStatus.Success,
                fixture.ValidateDestination(first, RoomBPath, destination1).Status);
            Assert.AreEqual(RoomConnectionResolutionStatus.Success,
                fixture.ValidateDestination(second, RoomBPath, destination2).Status);
        }
    }

    private static RoomConnection CreateConnection(
        string roomAId,
        string doorAId,
        string roomBId,
        string doorBId,
        RoomConnectionDirection direction)
    {
        RoomConnection connection = ScriptableObject.CreateInstance<RoomConnection>();
        connection.name = "Transient connection";
        connection.Assign(roomAId, doorAId, roomBId, doorBId, direction);
        return connection;
    }

    private static RoomTransition CreateDoor(string doorId, RoomConnection connection)
    {
        GameObject gameObject = new GameObject("Transient " + doorId);
        RoomTransition door = gameObject.AddComponent<RoomTransition>();
        SerializedObject serializedDoor = new SerializedObject(door);
        serializedDoor.FindProperty("doorId").stringValue = doorId;
        serializedDoor.FindProperty("connection").objectReferenceValue = connection;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();
        return door;
    }
}
