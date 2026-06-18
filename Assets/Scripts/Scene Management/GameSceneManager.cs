using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Unity.Cinemachine; // Cinemachine 3 (Unity 6)

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
    private string _targetDoorID;
    private bool _isTransitioning;

    private const float MAX_DELTA_TIME = 0.033f; 

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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

        Scene activeScene = SceneManager.GetActiveScene();
        
        if (activeScene.name == gameObject.scene.name)
        {
            if (SceneManager.sceneCount > 1)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene loadedScene = SceneManager.GetSceneAt(i);
                    if (loadedScene.name != gameObject.scene.name)
                    {
                        _currentRoomScene = loadedScene.name;
                        SceneManager.SetActiveScene(loadedScene);
                        break;
                    }
                }
            }
            else
            {
                LoadInitialRoom(firstRoomSceneName, firstRoomDoorID);
            }
        }
        else
        {
            _currentRoomScene = activeScene.name;
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
        _isTransitioning = true;
        _targetDoorID = targetDoorID;

        SetPlayerControl(false);

        yield return StartCoroutine(FadeToBlack());

        if (!string.IsNullOrEmpty(_currentRoomScene))
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(_currentRoomScene);
            while (!unloadOp.isDone) yield return null;
        }

        _currentRoomScene = sceneName;
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(_currentRoomScene, LoadSceneMode.Additive);
        while (!loadOp.isDone) yield return null;

        Scene newScene = SceneManager.GetSceneByName(_currentRoomScene);
        SceneManager.SetActiveScene(newScene);
        
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

    public void LoadInitialRoom(string sceneName, string targetDoorID)
    {
        StartCoroutine(TransitionRoutine(sceneName, targetDoorID));
    }
    private void SetPlayerControl(bool hasControl)
    {
        if (playerTransform == null) return;

        Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            if (!hasControl)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        var moveScript = playerTransform.GetComponent<PlayerMovement>();
        if (moveScript != null)
        {
            moveScript.enabled = hasControl;

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
        RoomTransition[] doors = FindObjectsByType<RoomTransition>(FindObjectsInactive.Exclude);
        foreach (RoomTransition door in doors)
        {
            if (door.DoorID == _targetDoorID)
            {
                playerTransform.position = door.SpawnPoint.position;
                Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }
        }
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
}