using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine; // Cinemachine 3 (Unity 6)
using SpearWander.Abilities;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    [Header("Configurazione Iniziale")]
    [SerializeField] private string firstRoomSceneName = "Stanza_01_Tutorial";
    [SerializeField] private string firstRoomDoorID = "Spawn_Iniziale";

    [Header("Telecamera")]
    [SerializeField] private CinemachineConfiner2D cameraConfiner;

    [Header("Interfaccia Transizione (Game Feel)")]
    [SerializeField] private CanvasGroup transitionCanvasGroup;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float fadeInDuration = 0.4f;

    private string _currentRoomScene;
    private RoomDefinition _currentRoomDefinition;
    private string _targetDoorID;
    private bool _isTransitioning;

    private const float MAX_DELTA_TIME = 0.033f;

    private sealed class ConnectedTransitionSnapshot
    {
        public Scene SourceScene;
        public Scene PreviousActiveScene;
        public string CurrentRoomScene;
        public RoomDefinition CurrentRoomDefinition;
        public string TargetDoorId;
        public float TimeScale;
        public float FadeAlpha;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;
        public Player Player;
        public bool HadTransitionControlRequest;
        public Rigidbody2D PlayerRigidbody;
        public RigidbodyType2D RigidbodyBodyType;
        public Vector2 RigidbodyVelocity;
        public float RigidbodyAngularVelocity;
        public bool RigidbodySimulated;
        public RigidbodyConstraints2D RigidbodyConstraints;
        public Collider2D[] PlayerColliders;
        public bool[] PlayerColliderEnabled;
        public Collider2D PreviousCameraBounds;
        public Transform CameraTransform;
        public Vector3 CameraPosition;
        public Quaternion CameraRotation;
    }

    public string CurrentRoom => _currentRoomScene;
    public RoomDefinition CurrentRoomDefinition => _currentRoomDefinition;
    public string CurrentRoomId => _currentRoomDefinition != null ? _currentRoomDefinition.RoomId : null;

    /// <summary>
    /// Resolves the connection assigned to a source door without starting a
    /// scene transition or changing current-room/player state.
    /// </summary>
    public RoomConnectionResolutionResult ResolveConnectedRoomTransition(RoomTransition sourceDoor)
    {
        return ResolveConnectedRoomTransitionWithCatalogLoader(
            sourceDoor,
            RuntimeRoomCatalog.LoadDefault);
    }

    private static RoomConnectionResolutionResult ResolveConnectedRoomTransitionWithCatalogLoader(
        RoomTransition sourceDoor,
        Func<RuntimeRoomCatalog> catalogLoader)
    {
        if (sourceDoor == null)
            return RoomConnectionResolver.Resolve(null, null);

        RuntimeRoomCatalog catalog;
        try
        {
            catalog = catalogLoader != null ? catalogLoader() : null;
        }
        catch (InvalidOperationException exception)
        {
            return new RoomConnectionResolutionResult(
                RoomConnectionResolutionStatus.InvalidSourceRoom,
                "RuntimeRoomCatalog could not be loaded: " + exception.Message);
        }

        return RoomConnectionResolver.Resolve(sourceDoor, catalog);
    }

    /// <summary>
    /// Routes a door request to the connected transaction when it owns a
    /// RoomConnection; connection-less doors retain the legacy route.
    /// </summary>
    public void RequestRoomTransition(RoomTransition sourceDoor)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning("[GameSceneManager] Room transition request rejected; another transition is already in progress.", this);
            return;
        }

        if (sourceDoor == null)
        {
            Debug.LogError("[GameSceneManager] Room transition request has no source door.", this);
            return;
        }

        if (UsesConnectedTransition(sourceDoor))
        {
            StartCoroutine(ConnectedTransitionRoutine(sourceDoor));
            return;
        }

        // Preserve the old trigger's no-op behavior for an unconfigured door.
        if (string.IsNullOrEmpty(sourceDoor.TargetSceneName))
            return;

        TransitionToRoom(sourceDoor.TargetSceneName, sourceDoor.TargetDoorID);
    }

    private static bool UsesConnectedTransition(RoomTransition sourceDoor)
    {
        return sourceDoor != null && sourceDoor.Connection != null;
    }

    private IEnumerator ConnectedTransitionRoutine(RoomTransition sourceDoor)
    {
        _isTransitioning = true;

        RoomConnectionResolutionResult resolution;
        try
        {
            resolution = ResolveConnectedRoomTransition(sourceDoor);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GameSceneManager] Connected transition failed at source validation: {exception.Message}", this);
            _isTransitioning = false;
            yield break;
        }
        if (resolution.Status != RoomConnectionResolutionStatus.DestinationValidationPending ||
            resolution.DestinationRoom == null ||
            string.IsNullOrEmpty(resolution.DestinationScenePath) ||
            string.IsNullOrEmpty(resolution.DestinationDoorId))
        {
            Debug.LogError(
                $"[GameSceneManager] Connected transition failed at source validation: " +
                $"{resolution.Status}: {resolution.Message}", this);
            _isTransitioning = false;
            yield break;
        }

        Scene sourceScene = sourceDoor.gameObject.scene;
        if (!sourceScene.IsValid() || !sourceScene.isLoaded || !IsCurrentSourceRoom(sourceScene, resolution))
        {
            Debug.LogError(
                $"[GameSceneManager] Connected transition failed at source validation: " +
                $"door '{resolution.SourceDoorId}' is not in the manager's loaded current room.", this);
            _isTransitioning = false;
            yield break;
        }

        ConnectedTransitionSnapshot snapshot = CaptureConnectedTransitionSnapshot(sourceScene);
        if (snapshot == null)
        {
            Debug.LogError("[GameSceneManager] Connected transition failed at source validation: player or source state is unavailable.", this);
            _isTransitioning = false;
            yield break;
        }

        string failedStage = "destination load";
        string failureReason = null;
        Scene destinationScene = default;
        bool playerRelocated = false;

        // Keep room simulation from advancing while two room scenes overlap.
        Time.timeScale = 0f;
        SetConnectedPlayerControl(snapshot, false);
        yield return StartCoroutine(FadeToBlack());

        if (HasLoadedScenePath(resolution.DestinationScenePath))
        {
            failureReason = $"Destination scene '{resolution.DestinationScenePath}' is already loaded.";
        }
        else if (!TryBeginAdditiveLoad(resolution.DestinationScenePath, out AsyncOperation loadOperation, out failureReason))
        {
            // The source is intentionally kept loaded until the transaction commits.
        }
        else
        {
            while (!loadOperation.isDone)
                yield return null;

            if (!TryGetUniqueLoadedScene(resolution.DestinationScenePath, out destinationScene, out failureReason))
            {
                // Handled by the rollback below.
            }
        }

        if (failureReason != null)
        {
            yield return StartCoroutine(RollbackConnectedTransition(
                snapshot, destinationScene, playerRelocated, failedStage, failureReason));
            yield break;
        }

        failedStage = "destination validation";
        if (!TryValidateConnectedDestination(destinationScene, resolution, out _,
                out Transform spawnPoint, out failureReason))
        {
            yield return StartCoroutine(RollbackConnectedTransition(
                snapshot, destinationScene, playerRelocated, failedStage, failureReason));
            yield break;
        }

        failedStage = "player relocation";
        try
        {
            playerRelocated = true;
            playerTransform.position = spawnPoint.position;
            if (snapshot.PlayerRigidbody != null)
            {
                snapshot.PlayerRigidbody.bodyType = snapshot.RigidbodyBodyType;
                snapshot.PlayerRigidbody.linearVelocity = Vector2.zero;
                snapshot.PlayerRigidbody.angularVelocity = snapshot.RigidbodyAngularVelocity;
            }
            Physics2D.SyncTransforms();
        }
        catch (Exception exception)
        {
            failureReason = exception.Message;
        }

        if (failureReason != null)
        {
            yield return StartCoroutine(RollbackConnectedTransition(
                snapshot, destinationScene, playerRelocated, failedStage, failureReason));
            yield break;
        }

        failedStage = "destination activation and camera setup";
        try
        {
            if (!SceneManager.SetActiveScene(destinationScene))
            {
                failureReason = $"Unity refused to make destination scene '{destinationScene.path}' active.";
            }
            else if (!TrySetConnectedCameraBounds(destinationScene, out failureReason))
            {
                // Keep the source room and restore the prior camera state.
            }
            else
            {
                PositionCameraAtPlayer();
            }
        }
        catch (Exception exception)
        {
            failureReason = exception.Message;
        }

        if (failureReason != null)
        {
            yield return StartCoroutine(RollbackConnectedTransition(
                snapshot, destinationScene, playerRelocated, failedStage, failureReason));
            yield break;
        }

        // Commit only after the destination is loaded, validated, occupied,
        // active, and its camera bounds are assigned.
        _currentRoomScene = destinationScene.name;
        _currentRoomDefinition = resolution.DestinationRoom;

        AsyncOperation unloadOperation = null;
        try
        {
            unloadOperation = SceneManager.UnloadSceneAsync(sourceScene);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GameSceneManager] Connected transition committed, but source unload failed: {exception.Message}", this);
        }

        if (unloadOperation != null)
        {
            while (!unloadOperation.isDone)
                yield return null;
        }
        else if (sourceScene.IsValid() && sourceScene.isLoaded)
        {
            Debug.LogError($"[GameSceneManager] Connected transition committed, but source scene '{sourceScene.path}' could not be unloaded.", this);
        }

        yield return StartCoroutine(FadeFromBlack());
        SetConnectedPlayerControl(snapshot, true);
        Time.timeScale = snapshot.TimeScale;
        yield return new WaitForSecondsRealtime(0.1f);
        _isTransitioning = false;
    }

    private bool IsCurrentSourceRoom(Scene sourceScene, RoomConnectionResolutionResult resolution)
    {
        if (string.IsNullOrEmpty(_currentRoomScene) || sourceScene.name != _currentRoomScene)
            return false;

        return _currentRoomDefinition == null ||
               string.Equals(_currentRoomDefinition.RoomId, resolution.SourceRoom.RoomId, StringComparison.Ordinal);
    }

    private ConnectedTransitionSnapshot CaptureConnectedTransitionSnapshot(Scene sourceScene)
    {
        if (playerTransform == null)
            return null;

        Player player = playerTransform.GetComponent<Player>();
        if (player == null)
            return null;

        ConnectedTransitionSnapshot snapshot = new ConnectedTransitionSnapshot
        {
            SourceScene = sourceScene,
            PreviousActiveScene = SceneManager.GetActiveScene(),
            CurrentRoomScene = _currentRoomScene,
            CurrentRoomDefinition = _currentRoomDefinition,
            TargetDoorId = _targetDoorID,
            TimeScale = Time.timeScale,
            FadeAlpha = transitionCanvasGroup != null ? transitionCanvasGroup.alpha : 0f,
            PlayerPosition = playerTransform.position,
            PlayerRotation = playerTransform.rotation,
            Player = player,
            HadTransitionControlRequest = player.HasControlRequest(Player.ControlReason.Transition),
            PlayerRigidbody = playerTransform.GetComponent<Rigidbody2D>(),
            PlayerColliders = playerTransform.GetComponentsInChildren<Collider2D>(includeInactive: true),
            PreviousCameraBounds = cameraConfiner != null ? cameraConfiner.BoundingShape2D : null
        };

        snapshot.PlayerColliderEnabled = new bool[snapshot.PlayerColliders.Length];
        for (int i = 0; i < snapshot.PlayerColliders.Length; i++)
            snapshot.PlayerColliderEnabled[i] = snapshot.PlayerColliders[i] != null && snapshot.PlayerColliders[i].enabled;

        if (snapshot.PlayerRigidbody != null)
        {
            snapshot.RigidbodyBodyType = snapshot.PlayerRigidbody.bodyType;
            snapshot.RigidbodyVelocity = snapshot.PlayerRigidbody.linearVelocity;
            snapshot.RigidbodyAngularVelocity = snapshot.PlayerRigidbody.angularVelocity;
            snapshot.RigidbodySimulated = snapshot.PlayerRigidbody.simulated;
            snapshot.RigidbodyConstraints = snapshot.PlayerRigidbody.constraints;
        }

        if (cameraConfiner != null)
        {
            CinemachineCamera camera = cameraConfiner.GetComponent<CinemachineCamera>();
            if (camera != null)
            {
                snapshot.CameraTransform = camera.transform;
                snapshot.CameraPosition = camera.transform.position;
                snapshot.CameraRotation = camera.transform.rotation;
            }
        }

        return snapshot;
    }

    private static bool TryBeginAdditiveLoad(string scenePath, out AsyncOperation operation, out string error)
    {
        operation = null;
        error = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
            if (operation == null)
            {
                error = $"Unity could not start loading destination scene '{scenePath}'.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"Could not load destination scene '{scenePath}': {exception.Message}";
            return false;
        }
    }

    private static bool HasLoadedScenePath(string scenePath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && string.Equals(scene.path, scenePath, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool TryGetUniqueLoadedScene(string scenePath, out Scene scene, out string error)
    {
        scene = default;
        error = null;
        int matches = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidate = SceneManager.GetSceneAt(i);
            if (candidate.isLoaded && string.Equals(candidate.path, scenePath, StringComparison.Ordinal))
            {
                scene = candidate;
                matches++;
            }
        }

        if (matches == 1)
            return true;

        error = matches == 0
            ? $"Destination scene '{scenePath}' did not become loaded."
            : $"Destination scene path '{scenePath}' is loaded {matches} times; a unique destination is required.";
        scene = default;
        return false;
    }

    private static bool TryValidateConnectedDestination(
        Scene destinationScene,
        RoomConnectionResolutionResult resolution,
        out RoomTransition destinationDoor,
        out Transform spawnPoint,
        out string error)
    {
        destinationDoor = null;
        spawnPoint = null;
        error = null;
        if (!destinationScene.IsValid() || !destinationScene.isLoaded ||
            (!string.IsNullOrEmpty(destinationScene.path) &&
             !string.Equals(destinationScene.path, resolution.DestinationScenePath, StringComparison.Ordinal)))
        {
            error = "The loaded destination scene is invalid or does not match the catalog scene path.";
            return false;
        }

        List<RoomTransition> doors = new List<RoomTransition>();
        foreach (GameObject root in destinationScene.GetRootGameObjects())
            doors.AddRange(root.GetComponentsInChildren<RoomTransition>(includeInactive: true));

        RoomConnectionResolutionResult validated = RoomConnectionResolver.ValidateDestinationBacklink(
            resolution,
            resolution.DestinationScenePath,
            doors);
        if (validated.Status != RoomConnectionResolutionStatus.Success)
        {
            error = $"{validated.Status}: {validated.Message}";
            return false;
        }

        int matches = 0;
        foreach (RoomTransition candidate in doors)
        {
            if (candidate != null && string.Equals(candidate.DoorId, resolution.DestinationDoorId, StringComparison.Ordinal))
            {
                destinationDoor = candidate;
                matches++;
            }
        }

        if (matches != 1 || destinationDoor == null)
        {
            error = $"Destination doorId '{resolution.DestinationDoorId}' must match exactly one door; found {matches}.";
            destinationDoor = null;
            return false;
        }

        spawnPoint = destinationDoor.SpawnPoint;
        if (spawnPoint == null || spawnPoint.gameObject.scene != destinationScene)
        {
            error = $"Destination door '{resolution.DestinationDoorId}' has no valid SpawnPoint in the destination scene.";
            spawnPoint = null;
            return false;
        }

        return true;
    }

    private bool TrySetConnectedCameraBounds(Scene destinationScene, out string error)
    {
        error = null;
        if (cameraConfiner == null)
            return true;

        Collider2D bounds = null;
        int matches = 0;
        foreach (GameObject root in destinationScene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (!child.CompareTag("CameraBounds"))
                    continue;

                Collider2D candidate = child.GetComponent<Collider2D>();
                if (candidate == null)
                    continue;

                bounds = candidate;
                matches++;
            }
        }

        if (matches != 1)
        {
            error = $"Destination scene '{destinationScene.path}' must contain exactly one CameraBounds collider; found {matches}.";
            return false;
        }

        cameraConfiner.BoundingShape2D = bounds;
        cameraConfiner.InvalidateBoundingShapeCache();
        return true;
    }

    private void PositionCameraAtPlayer()
    {
        if (cameraConfiner == null || playerTransform == null)
            return;

        CinemachineCamera camera = cameraConfiner.GetComponent<CinemachineCamera>();
        if (camera == null)
            return;

        Vector3 targetPosition = new Vector3(
            playerTransform.position.x,
            playerTransform.position.y,
            camera.transform.position.z);
        camera.transform.position = targetPosition;
        camera.ForceCameraPosition(targetPosition, camera.transform.rotation);
        cameraConfiner.InvalidateBoundingShapeCache();
    }

    private void SetConnectedPlayerControl(ConnectedTransitionSnapshot snapshot, bool restoreMovement)
    {
        if (snapshot.PlayerRigidbody != null)
        {
            if (restoreMovement)
            {
                snapshot.PlayerRigidbody.bodyType = snapshot.RigidbodyBodyType;
                snapshot.PlayerRigidbody.linearVelocity = Vector2.zero;
                snapshot.PlayerRigidbody.angularVelocity = snapshot.RigidbodyAngularVelocity;
            }
            else
            {
                snapshot.PlayerRigidbody.linearVelocity = Vector2.zero;
                snapshot.PlayerRigidbody.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        if (!snapshot.HadTransitionControlRequest)
        {
            if (restoreMovement)
                snapshot.Player.RemoveControlRequest(Player.ControlReason.Transition);
            else
                snapshot.Player.AddControlRequest(Player.ControlReason.Transition);
        }
    }

    private IEnumerator RollbackConnectedTransition(
        ConnectedTransitionSnapshot snapshot,
        Scene destinationScene,
        bool playerRelocated,
        string failedStage,
        string failureReason)
    {
        Debug.LogError($"[GameSceneManager] Connected transition rolled back at {failedStage}: {failureReason}", this);

        _currentRoomScene = snapshot.CurrentRoomScene;
        _currentRoomDefinition = snapshot.CurrentRoomDefinition;
        _targetDoorID = snapshot.TargetDoorId;

        if (playerRelocated && playerTransform != null)
        {
            playerTransform.SetPositionAndRotation(snapshot.PlayerPosition, snapshot.PlayerRotation);
            for (int i = 0; i < snapshot.PlayerColliders.Length; i++)
            {
                if (snapshot.PlayerColliders[i] != null)
                    snapshot.PlayerColliders[i].enabled = snapshot.PlayerColliderEnabled[i];
            }
            RestoreRigidbodySnapshot(snapshot);
            Physics2D.SyncTransforms();
        }

        if (snapshot.PreviousActiveScene.IsValid() && snapshot.PreviousActiveScene.isLoaded &&
            SceneManager.GetActiveScene() != snapshot.PreviousActiveScene)
        {
            SceneManager.SetActiveScene(snapshot.PreviousActiveScene);
        }

        RestoreCameraSnapshot(snapshot);

        if (destinationScene.IsValid() && destinationScene.isLoaded && destinationScene != snapshot.SourceScene)
        {
            AsyncOperation unloadOperation = null;
            try
            {
                unloadOperation = SceneManager.UnloadSceneAsync(destinationScene);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameSceneManager] Rollback could not unload destination scene '{destinationScene.path}': {exception.Message}", this);
            }

            if (unloadOperation != null)
            {
                while (!unloadOperation.isDone)
                    yield return null;
            }
            if (destinationScene.IsValid() && destinationScene.isLoaded)
            {
                Debug.LogError($"[GameSceneManager] Rollback destination scene '{destinationScene.path}' remained loaded.", this);
            }
        }

        if (!playerRelocated)
            RestoreRigidbodySnapshot(snapshot);

        yield return StartCoroutine(FadeToAlpha(snapshot.FadeAlpha));
        if (snapshot.Player != null && !snapshot.HadTransitionControlRequest)
            snapshot.Player.RemoveControlRequest(Player.ControlReason.Transition);
        Time.timeScale = snapshot.TimeScale;
        _isTransitioning = false;
    }

    private static void RestoreRigidbodySnapshot(ConnectedTransitionSnapshot snapshot)
    {
        Rigidbody2D rb = snapshot.PlayerRigidbody;
        if (rb == null)
            return;

        rb.bodyType = snapshot.RigidbodyBodyType;
        rb.constraints = snapshot.RigidbodyConstraints;
        rb.simulated = snapshot.RigidbodySimulated;
        rb.linearVelocity = snapshot.RigidbodyVelocity;
        rb.angularVelocity = snapshot.RigidbodyAngularVelocity;
    }

    private void RestoreCameraSnapshot(ConnectedTransitionSnapshot snapshot)
    {
        if (cameraConfiner == null)
            return;

        cameraConfiner.BoundingShape2D = snapshot.PreviousCameraBounds;
        if (snapshot.CameraTransform != null)
        {
            snapshot.CameraTransform.SetPositionAndRotation(snapshot.CameraPosition, snapshot.CameraRotation);
            CinemachineCamera camera = snapshot.CameraTransform.GetComponent<CinemachineCamera>();
            if (camera != null)
                camera.ForceCameraPosition(snapshot.CameraPosition, snapshot.CameraRotation);
        }
        cameraConfiner.InvalidateBoundingShapeCache();
    }

    private IEnumerator FadeToAlpha(float targetAlpha)
    {
        if (transitionCanvasGroup == null)
            yield break;

        float startAlpha = transitionCanvasGroup.alpha;
        float duration = targetAlpha > startAlpha ? fadeOutDuration : fadeInDuration;
        if (duration <= 0f)
        {
            transitionCanvasGroup.alpha = targetAlpha;
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Mathf.Min(Time.unscaledDeltaTime, MAX_DELTA_TIME);
            transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }
        transitionCanvasGroup.alpha = targetAlpha;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (transitionCanvasGroup != null) transitionCanvasGroup.alpha = 0f;

        // After DontDestroyOnLoad, gameObject.scene is "DontDestroyOnLoad".
        // Check by scene count: if only 1 real scene (non-DontDestroyOnLoad), load the initial room.
        int realSceneCount = 0;
        Scene realScene = default;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.name != "DontDestroyOnLoad")
            {
                realSceneCount++;
                realScene = s;
            }
        }

        if (realSceneCount > 1)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.name != "DontDestroyOnLoad" && s != SceneManager.GetActiveScene())
                {
                    _currentRoomScene = s.name;
                    _currentRoomDefinition = ResolveRoomDefinition(s);
                    SceneManager.SetActiveScene(s);
                    break;
                }
            }
        }
        else if (realSceneCount == 1)
        {
            if (realScene.name != "Master")
            {
                _currentRoomScene = realScene.name;
                _currentRoomDefinition = ResolveRoomDefinition(realScene);
            }
            else
                LoadInitialRoom(firstRoomSceneName, firstRoomDoorID);
        }
    }

    public void TransitionToRoom(string sceneName, string targetDoorID)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning($"[GameSceneManager] Transizione rifiutata. Una transizione è già in corso!");
            return;
        }
        StartCoroutine(TransitionRoutine(sceneName, targetDoorID));
    }

    private IEnumerator TransitionRoutine(string sceneName, string targetDoorID)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameSceneManager] TransitionToRoom called with an empty scene name.");
            yield break;
        }

        _isTransitioning = true;
        _targetDoorID = targetDoorID;

        SetPlayerControl(false);

        yield return StartCoroutine(FadeToBlack());

        if (!string.IsNullOrEmpty(_currentRoomScene))
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(_currentRoomScene);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone) yield return null;
            }
        }

        _currentRoomScene = sceneName;
        _currentRoomDefinition = null;
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(_currentRoomScene, LoadSceneMode.Additive);

        if (loadOp == null)
        {
            Debug.LogError($"[GameSceneManager] Could not load room scene '{_currentRoomScene}'. Is it added to the build settings?");
            SetPlayerControl(true);
            _isTransitioning = false;
            yield break;
        }

        while (!loadOp.isDone) yield return null;

        Scene newScene = SceneManager.GetSceneByName(_currentRoomScene);
        SceneManager.SetActiveScene(newScene);
        _currentRoomDefinition = ResolveRoomDefinition(newScene);

        Physics2D.SyncTransforms();
        yield return null;

        FocusTargetDoor();
        UpdateCameraBounds();

        if (cameraConfiner != null)
        {
            CinemachineCamera cCamera = cameraConfiner.GetComponent<CinemachineCamera>();
            if (cCamera != null)
            {
                Vector3 targetCamPos = new Vector3(playerTransform.position.x, playerTransform.position.y, cCamera.transform.position.z);
                cCamera.transform.position = targetCamPos;
                cCamera.ForceCameraPosition(targetCamPos, cCamera.transform.rotation);
            }
            cameraConfiner.InvalidateBoundingShapeCache();
        }

        yield return null;
        yield return null;
        yield return null;

        yield return StartCoroutine(FadeFromBlack());

        SetPlayerControl(true);

        yield return new WaitForSecondsRealtime(0.1f);

        _isTransitioning = false;
    }

    public void ReloadCurrentRoom(bool resetState = true, Vector3? overrideSpawn = null)
    {
        if (_isTransitioning) return;
        if (string.IsNullOrEmpty(_currentRoomScene)) return;

        string targetScene = _currentRoomScene;

        if (!overrideSpawn.HasValue &&
            CheckpointManager.Instance != null &&
            CheckpointManager.Instance.HasCheckpoint)
        {
            var (room, pos) = CheckpointManager.Instance.GetCheckpoint();
            overrideSpawn = pos;
            targetScene = room;
        }

        if (!overrideSpawn.HasValue)
            targetScene = firstRoomSceneName;

        StartCoroutine(ReloadRoomRoutine(targetScene, overrideSpawn, resetState));
    }

    private IEnumerator ReloadRoomRoutine(string targetScene, Vector3? overrideSpawn = null, bool resetState = true)
    {
        _isTransitioning = true;

        Player player = playerTransform.GetComponent<Player>();
        if (resetState && player != null) player.ResetState();

        SetPlayerControl(false);

        yield return StartCoroutine(FadeToBlack());

        if (!string.IsNullOrEmpty(_currentRoomScene))
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(_currentRoomScene);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone) yield return null;
            }
        }

        _currentRoomScene = targetScene;
        _currentRoomDefinition = null;
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(_currentRoomScene, LoadSceneMode.Additive);

        if (loadOp == null)
        {
            Debug.LogError($"[GameSceneManager] Could not load room scene '{_currentRoomScene}'. Is it added to the build settings?");
            SetPlayerControl(true);
            _isTransitioning = false;
            yield break;
        }

        while (!loadOp.isDone) yield return null;

        Scene newScene = SceneManager.GetSceneByName(_currentRoomScene);
        SceneManager.SetActiveScene(newScene);
        _currentRoomDefinition = ResolveRoomDefinition(newScene);

        Physics2D.SyncTransforms();
        yield return null;

        if (overrideSpawn.HasValue)
        {
            playerTransform.position = overrideSpawn.Value;
            Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
        else
        {
            FocusSpawnDoor();
        }
        UpdateCameraBounds();

        if (cameraConfiner != null)
        {
            CinemachineCamera cCamera = cameraConfiner.GetComponent<CinemachineCamera>();
            if (cCamera != null)
            {
                Vector3 targetCamPos = new Vector3(playerTransform.position.x, playerTransform.position.y, cCamera.transform.position.z);
                cCamera.transform.position = targetCamPos;
                cCamera.ForceCameraPosition(targetCamPos, cCamera.transform.rotation);
            }
            cameraConfiner.InvalidateBoundingShapeCache();
        }

        yield return null;
        yield return null;
        yield return null;

        if (player != null) player.RemoveControlRequest(Player.ControlReason.Death);

        // Restore abilities after scene reload when the player state was not reset.
        if (!resetState && player != null && AbilityManager.Instance != null)
        {
            AbilityManager.Instance.RestoreAbilities(player);
        }

        yield return StartCoroutine(FadeFromBlack());

        SetPlayerControl(true);

        yield return new WaitForSecondsRealtime(0.1f);

        _isTransitioning = false;
    }

    private void FocusSpawnDoor()
    {
        RoomTransition[] doors = FindObjectsByType<RoomTransition>(FindObjectsInactive.Exclude);
        if (doors.Length == 0) return;

        foreach (RoomTransition door in doors)
        {
            if (door.DoorID == firstRoomDoorID)
            {
                if (playerTransform != null && door.SpawnPoint != null)
                {
                    playerTransform.position = door.SpawnPoint.position;
                    Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                }
                return;
            }
        }

        RoomTransition first = doors[0];
        if (playerTransform != null && first.SpawnPoint != null)
        {
            playerTransform.position = first.SpawnPoint.position;
            Rigidbody2D rb2 = playerTransform.GetComponent<Rigidbody2D>();
            if (rb2 != null) rb2.linearVelocity = Vector2.zero;
        }
    }

    public void LoadInitialRoom(string sceneName, string targetDoorID)
    {
        StartCoroutine(TransitionRoutine(sceneName, targetDoorID));
    }

    private void SetPlayerControl(bool hasControl)
    {
        if (playerTransform == null) return;

        Player player = playerTransform.GetComponent<Player>();
        if (player == null) return;

        Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb == null) return;

        if (!hasControl)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            player.AddControlRequest(Player.ControlReason.Transition);
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            player.RemoveControlRequest(Player.ControlReason.Transition);
        }
    }

    public IEnumerator FadeToBlack()
    {
        if (transitionCanvasGroup == null) yield break;
        float startAlpha = transitionCanvasGroup.alpha;
        float time = 0;

        while (time < fadeOutDuration)
        {
            float safeDelta = Mathf.Min(Time.unscaledDeltaTime, MAX_DELTA_TIME);
            time += safeDelta;
            transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, time / fadeOutDuration);
            yield return null;
        }
        transitionCanvasGroup.alpha = 1f;
    }

    public IEnumerator FadeFromBlack()
    {
        if (transitionCanvasGroup == null) yield break;
        float startAlpha = transitionCanvasGroup.alpha;
        float time = 0;

        while (time < fadeInDuration)
        {
            float safeDelta = Mathf.Min(Time.unscaledDeltaTime, MAX_DELTA_TIME);
            time += safeDelta;
            transitionCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, time / fadeInDuration);
            yield return null;
        }
        transitionCanvasGroup.alpha = 0f;
    }

    private void FocusTargetDoor()
    {
        if (playerTransform == null) return;
        RoomTransition[] doors = FindObjectsByType<RoomTransition>(FindObjectsInactive.Exclude);
        foreach (RoomTransition door in doors)
        {
            if (door.DoorID == _targetDoorID)
            {
                if (door.SpawnPoint == null)
                {
                    Debug.LogWarning($"[GameSceneManager] Door '{_targetDoorID}' has no spawn point assigned.");
                    return;
                }

                playerTransform.position = door.SpawnPoint.position;
                Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }
        }
        Debug.LogWarning($"[GameSceneManager] Porta target '{_targetDoorID}' non trovata nella stanza '{_currentRoomScene}'.");
    }

    private void UpdateCameraBounds()
    {
        if (cameraConfiner == null) return;

        GameObject boundsObj = GameObject.FindGameObjectWithTag("CameraBounds");
        if (boundsObj != null)
        {
            Collider2D boundsCollider = boundsObj.GetComponent<Collider2D>();
            if (boundsCollider != null)
            {
                cameraConfiner.BoundingShape2D = boundsCollider;
            }
        }
    }

    private static RoomDefinition ResolveRoomDefinition(Scene scene)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            return null;

        try
        {
            RuntimeRoomCatalog catalog = RuntimeRoomCatalog.LoadDefault();
            return catalog.TryGetRoomByScenePath(scene.path, out RoomDefinition definition)
                ? definition
                : null;
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogWarning($"[GameSceneManager] Could not resolve room identity for '{scene.path}': {exception.Message}");
            return null;
        }
    }
}
