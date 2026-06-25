using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCombatV2 : MonoBehaviour
{
    [Header("Spear Physics")]
    [SerializeField] private float shootForce = 20f;
    [SerializeField] private GameObject spearPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform aimIndicator;

    [Header("Cooldown Settings")]
    [SerializeField] private float throwCooldown = 0.42f;

    public enum SpearUIState { Ready, Thrown, Returning }

    private Player _player;
    private float _throwCooldownTimer;
    private bool _isSpearReturning;
    private bool _waitingForRecallRelease;
    private SpearV2 _currentSpear;
    private Camera _mainCam;

    public SpearUIState CurrentSpearUIState
    {
        get
        {
            if (_currentSpear == null) return SpearUIState.Ready;
            if (_isSpearReturning || _currentSpear.currentState == SpearV2.SpearState.Returning) return SpearUIState.Returning;
            return SpearUIState.Thrown;
        }
    }

    void Awake()
    {
        _player = GetComponent<Player>();
        _mainCam = Camera.main;
    }

    void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    void Update()
    {
        HandleAiming();

        if (_player.Input.FireTriggered && _throwCooldownTimer <= 0 && _currentSpear == null && !_isSpearReturning)
        {
            if (_player.RopeClimb != null && _player.RopeClimb.IsClimbing) return;
            Fire();
        }

        if (!_player.Input.IsRecallHeld())
        {
            _waitingForRecallRelease = false;

            if (_isSpearReturning && _currentSpear != null)
            {
                AbortRecall();
            }
        }

        if (_player.Input.IsRecallHeld() && !_isSpearReturning && !_waitingForRecallRelease)
        {
            RecallSpear();
        }
    }

    void FixedUpdate()
    {
        if (_throwCooldownTimer > 0) _throwCooldownTimer -= Time.fixedDeltaTime;
    }

    void HandleAiming()
    {
        Vector2 dir = Vector2.zero;

        if (_player.Input.IsGamepad)
        {
            dir = _player.Input.AimInput;
        }
        else
        {
            Vector3 mousePos = _mainCam.ScreenToWorldPoint(new Vector3(_player.Input.AimInput.x, _player.Input.AimInput.y, -_mainCam.transform.position.z));
            dir = ((Vector2)mousePos - (Vector2)transform.position).normalized;
        }

        if (dir.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            aimIndicator.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void Fire()
    {
        _throwCooldownTimer = throwCooldown;

        GameObject s = Instantiate(spearPrefab, firePoint.position, aimIndicator.rotation);
        _currentSpear = s.GetComponentInChildren<SpearV2>();
        _currentSpear.Initialize(_player.Collider, _player.RopeClimb.enabled);
        s.GetComponent<Rigidbody2D>().AddForce(aimIndicator.right * shootForce, ForceMode2D.Impulse);
    }

    void RecallSpear()
    {
        if (_currentSpear == null) return;
        if (_currentSpear.currentState == SpearV2.SpearState.Returning) return;

        _isSpearReturning = true;
        _waitingForRecallRelease = true;

        _currentSpear.StartReturn(transform);
        _currentSpear.OnSpearReturned += CatchSpear;
    }

    void AbortRecall()
    {
        if (_currentSpear == null) return;
        _currentSpear.OnSpearReturned -= CatchSpear;
        _currentSpear.AbortReturn();
        _isSpearReturning = false;
    }

    void OnSceneUnloaded(Scene scene)
    {
        if (_currentSpear != null)
        {
            if (_isSpearReturning)
                _currentSpear.OnSpearReturned -= CatchSpear;
            Destroy(_currentSpear.gameObject);
            _currentSpear = null;
        }
        _isSpearReturning = false;
        _waitingForRecallRelease = false;
        _throwCooldownTimer = 0f;
    }

    void CatchSpear(SpearV2 spear)
    {
        spear.OnSpearReturned -= CatchSpear;
        _isSpearReturning = false;

        if (spear.HasHitEnemy && _player.Health != null)
        {
            _player.Health.AddEssenceFromCatch(false);
        }

        if (_currentSpear == spear)
        {
            _currentSpear = null;
        }
        Destroy(spear.gameObject);
    }

    void OnDestroy()
    {
        if (_currentSpear != null)
        {
            _currentSpear.OnSpearReturned -= CatchSpear;
        }
    }
}
