using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private GameObject spearPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform aimIndicator;

    public enum SpearUIState { Ready, Thrown, Returning }

    private Player _player;
    private float _throwCooldownTimer;
    private bool _isSpearReturning;
    private bool _waitingForRecallRelease;
    private Spear _currentSpear;
    private Camera _mainCam;

    public SpearUIState CurrentSpearUIState
    {
        get
        {
            if (_currentSpear == null) return SpearUIState.Ready;
            if (_isSpearReturning || _currentSpear.currentState == Spear.SpearState.Returning) return SpearUIState.Returning;
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
        if (!_player.HasControl) return;

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
        _throwCooldownTimer = _player.CombatStats.throwCooldown;

        GameObject s = Instantiate(spearPrefab, firePoint.position, aimIndicator.rotation);
        _currentSpear = s.GetComponentInChildren<Spear>();
        _currentSpear.Initialize(_player.Collider, _player.RopeClimb.enabled, _player.RopeStats.ropeLength);
        s.GetComponent<Rigidbody2D>().AddForce(aimIndicator.right * _player.CombatStats.shootForce, ForceMode2D.Impulse);
        _currentSpear.SetDamage(_player.CombatStats.spearImpactDamage, _player.CombatStats.spearRecallDamage);
    }

    void RecallSpear()
    {
        if (_currentSpear == null) return;
        if (_currentSpear.currentState == Spear.SpearState.Returning) return;

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

    void CatchSpear(Spear spear)
    {
        spear.OnSpearReturned -= CatchSpear;
        _isSpearReturning = false;

        if (spear.HasHitEnemy && _player.Essence != null)
        {
            _player.Essence.AddEssenceFromCatch(false);
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

    public void ResetState()
    {
        if (_currentSpear != null)
        {
            _currentSpear.OnSpearReturned -= CatchSpear;
            Destroy(_currentSpear.gameObject);
            _currentSpear = null;
        }
        _isSpearReturning = false;
        _waitingForRecallRelease = false;
        _throwCooldownTimer = 0f;
    }
}
