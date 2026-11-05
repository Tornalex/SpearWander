using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private GameObject spearPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private SpearTrajectoryPredictor _trajectoryPredictor;
    [SerializeField] private SpearTrajectoryRenderer _trajectoryRenderer;
    [SerializeField, Min(0f)] private float aimDeadzone = 0.15f;
    [SerializeField, Min(0f)] private float aimSmoothing = 12f;

    public enum SpearUIState
    {
        Ready,
        Thrown,
        Returning
    }

    private Player _player;
    private float _throwCooldownTimer;
    private bool _isSpearReturning;
    private bool _isMouseAiming;
    private Spear _currentSpear;
    private Camera _mainCam;
    private Vector2 _aimDirection = Vector2.right;

    public bool HasSpear =>
        _currentSpear == null &&
        !_isSpearReturning;

    public Spear CurrentSpear => _currentSpear;

    public SpearUIState CurrentSpearUIState
    {
        get
        {
            if (_currentSpear == null)
                return SpearUIState.Ready;

            if (_isSpearReturning ||
                _currentSpear.currentState == Spear.SpearState.Returning)
            {
                return SpearUIState.Returning;
            }

            return SpearUIState.Thrown;
        }
    }

    void Awake()
    {
        _player = GetComponent<Player>();
        _mainCam = Camera.main;

        if (_trajectoryPredictor == null)
        {
            Debug.LogWarning(
                "[PlayerCombat] Trajectory preview disabled: assign the SpearTrajectoryPredictor component on Fire Point.",
                this);
        }
        else
        {
            _trajectoryPredictor.Configure(spearPrefab, _player.Collider);
        }

        if (_trajectoryRenderer == null)
        {
            Debug.LogWarning(
                "[PlayerCombat] Trajectory preview disabled: assign the SpearTrajectoryRenderer component on Fire Point.",
                this);
        }

        if (_trajectoryPredictor == null || _trajectoryRenderer == null)
            HideTrajectoryPreview();
    }

    void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    void OnDisable()
    {
        _isMouseAiming = false;
        HideTrajectoryPreview();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    void Update()
    {
        if (!_player.HasControl)
        {
            _isMouseAiming = false;
            HideTrajectoryPreview();
            return;
        }

        if (_player.Input.MouseFirePressed)
        {
            if (_currentSpear != null)
            {
                _isMouseAiming = false;
                HideTrajectoryPreview();
                RecallSpear();
                return;
            }

            _isMouseAiming = true;
        }

        if (_isMouseAiming)
        {
            UpdateAimDirection();
            UpdateTrajectoryPreview(true);

            if (_player.Input.MouseFireReleased)
            {
                _isMouseAiming = false;
                HideTrajectoryPreview();
                TryLaunchSpear();
            }
            else if (!_player.Input.MouseFireHeld)
            {
                // Cancel an interrupted hold rather than launching later.
                _isMouseAiming = false;
                HideTrajectoryPreview();
            }

            return;
        }

        UpdateAimDirection();

        bool controllerIsAiming =
            _player.Input.IsGamepad &&
            _player.Input.AimInput.sqrMagnitude > 0.1f;
        UpdateTrajectoryPreview(controllerIsAiming);

        if (!_player.Input.FireTriggered)
            return;

        HandleFireTriggered();
    }

    private void HandleFireTriggered()
    {
        if (_currentSpear != null)
        {
            RecallSpear();
            return;
        }

        TryLaunchSpear();
    }

    private void TryLaunchSpear()
    {
        HideTrajectoryPreview();

        if (_throwCooldownTimer <= 0 &&
            !_isSpearReturning)
        {
            if (_player.RopeClimb != null &&
                _player.RopeClimb.IsClimbing)
            {
                return;
            }

            Fire();
        }
    }

    private void UpdateTrajectoryPreview(bool isAiming)
    {
        if (!isAiming ||
            !CanLaunchSpear() ||
            firePoint == null ||
            _trajectoryPredictor == null ||
            _trajectoryRenderer == null)
        {
            HideTrajectoryPreview();
            return;
        }

        int pointCount = _trajectoryPredictor.Predict(
            firePoint.position,
            _aimDirection,
            _player.CombatStats.shootForce);

        _trajectoryRenderer.Show(
            _trajectoryPredictor.Points,
            pointCount);
    }

    private bool CanLaunchSpear()
    {
        return _player != null &&
               _player.HasControl &&
               _currentSpear == null &&
               !_isSpearReturning &&
               _throwCooldownTimer <= 0f &&
               spearPrefab != null &&
               firePoint != null &&
               _player.CombatStats != null &&
               (_player.RopeClimb == null || !_player.RopeClimb.IsClimbing);
    }

    private void HideTrajectoryPreview()
    {
        if (_trajectoryRenderer != null)
            _trajectoryRenderer.Hide();
    }

    void FixedUpdate()
    {
        if (_throwCooldownTimer > 0)
            _throwCooldownTimer -= Time.fixedDeltaTime;
    }

    void UpdateAimDirection()
    {
        if (_mainCam == null || firePoint == null)
            return;

        Vector2 dir = Vector2.zero;

        if (_player.Input.IsGamepad)
        {
            dir = _player.Input.AimInput;

            if (dir.magnitude <= aimDeadzone)
            {
                dir = new Vector2(
                    Mathf.Sign(transform.localScale.x),
                    0.15f
                );
            }
            else
            {
                Vector2 targetDirection = dir.normalized;
                float smoothingFactor = aimSmoothing > 0f
                    ? 1f - Mathf.Exp(-aimSmoothing * Time.deltaTime)
                    : 1f;
                Vector2 smoothedDirection = Vector2.Lerp(
                    _aimDirection,
                    targetDirection,
                    smoothingFactor);

                dir = smoothedDirection.sqrMagnitude > 0.0001f
                    ? smoothedDirection.normalized
                    : targetDirection;
            }
        }
        else
        {
            Vector3 mousePos =
                _mainCam.ScreenToWorldPoint(
                    new Vector3(
                        _player.Input.AimInput.x,
                        _player.Input.AimInput.y,
                        -_mainCam.transform.position.z
                    )
                );

            dir =
                ((Vector2)mousePos -
                 (Vector2)firePoint.position).normalized;
        }

        if (dir.sqrMagnitude > 0.01f)
            _aimDirection = dir.normalized;
    }

    void Fire()
    {
        if (spearPrefab == null || firePoint == null)
            return;

        _throwCooldownTimer =
            _player.CombatStats.throwCooldown;

        GameObject s =
            Instantiate(
                spearPrefab,
                firePoint.position,
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(_aimDirection.y, _aimDirection.x) *
                    Mathf.Rad2Deg)
            );

        _currentSpear =
            s.GetComponentInChildren<Spear>();

        if (_currentSpear == null)
        {
            Debug.LogError("[PlayerCombat] Spear prefab has no Spear component.", s);
            Destroy(s);
            return;
        }

        _currentSpear.Initialize(
            _player.Collider,
            _player.RopeClimb != null && _player.RopeClimb.enabled,
            _player.RopeStats.ropeLength
        );

        Rigidbody2D spearRb = s.GetComponent<Rigidbody2D>();

        if (spearRb != null)
        {
            spearRb.AddForce(
                _aimDirection *
                _player.CombatStats.shootForce,
                ForceMode2D.Impulse
            );
        }

        _currentSpear.SetDamage(
            _player.CombatStats.spearImpactDamage,
            _player.CombatStats.spearRecallDamage
        );
    }

    void RecallSpear()
    {
        if (_currentSpear == null)
            return;

        if (_currentSpear.currentState ==
            Spear.SpearState.Returning)
        {
            return;
        }

        _isSpearReturning = true;
        _currentSpear.StartReturn(transform);
        _currentSpear.OnSpearReturned += CatchSpear;
    }

    void OnSceneUnloaded(Scene scene)
    {
        _isMouseAiming = false;
        HideTrajectoryPreview();

        if (_currentSpear != null)
        {
            if (_isSpearReturning)
            {
                _currentSpear.OnSpearReturned -= CatchSpear;
            }

            Destroy(_currentSpear.gameObject);
            _currentSpear = null;
        }

        _isSpearReturning = false;
        _throwCooldownTimer = 0f;
    }

    void CatchSpear(Spear spear)
    {
        spear.OnSpearReturned -= CatchSpear;
        _isSpearReturning = false;

        if (spear.HasHitEnemy &&
            _player.Essence != null)
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
            Destroy(_currentSpear.gameObject);
        }
    }

    public void ResetState()
    {
        _isMouseAiming = false;
        HideTrajectoryPreview();

        if (_currentSpear != null)
        {
            _currentSpear.OnSpearReturned -= CatchSpear;
            Destroy(_currentSpear.gameObject);
            _currentSpear = null;
        }

        _isSpearReturning = false;
        _throwCooldownTimer = 0f;
    }
}
