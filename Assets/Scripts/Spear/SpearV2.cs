using UnityEngine;
using System.Collections.Generic;
using System;

public class SpearV2 : MonoBehaviour
{
    public enum SpearState { Flying, Embedded, Returning, Dropped }
    public SpearState currentState = SpearState.Flying;

    public event Action<SpearV2> OnSpearReturned;

    [Header("Settings - Damage & Flight")]
    [SerializeField] private AnimationCurve returnCurve;
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private float maxReturnSpeed = 50f;
    [SerializeField] private int impactDamage = 1;
    [SerializeField] private int recallDamage = 1;

    [Header("Settings - Physics & Tags")]
    [Tooltip("Tag assigned to camera boundary triggers")]
    [SerializeField] private string cameraBoundsTag = "CameraBounds";
    [Tooltip("How deep the spear embeds into walls")]
    [SerializeField] private float embedDepth = 0.15f;
    [Tooltip("Distance to catch the returning spear")]
    [SerializeField] private float catchDistance = 0.7f;
    [Tooltip("Raycast length to detect wall normal")]
    [SerializeField] private float tipRaycastLength = 1.5f;

    [Header("Rope Settings")]
    [Tooltip("Length of the rope hanging from the spear")]
    [SerializeField] private float ropeLength = 6f;
    [Tooltip("Local offset where the rope attaches (handle)")]
    [SerializeField] private Vector2 ropeAttachOffset = new Vector2(-1.25f, 0f);
    [Tooltip("Visual width of the rope")]
    [SerializeField] private float ropeWidth = 0.08f;
    [Tooltip("Material used for the rope line renderer")]
    [SerializeField] private Material ropeMaterial;

    public int RecallDamage => recallDamage;

    private float _returnTimer;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private PlatformEffector2D _effector;
    private Collider2D _playerCollider;
    private Collider2D _ignoredEnvironmentCollider;
    private Transform _playerTransform;
    private int _spearLayer;
    private List<IDamageable> _enemiesHitDuringReturn = new List<IDamageable>();
    private Vector2 _lastPosition;

    public bool HasHitEnemy { get; private set; }

    private GameObject _rope;
    private bool _canSpawnRope;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();
        _spearLayer = LayerMask.NameToLayer("Spear");
        if (_spearLayer < 0) _spearLayer = 6;
        gameObject.layer = _spearLayer;

        if (returnCurve == null || returnCurve.keys.Length == 0)
            returnCurve = AnimationCurve.Linear(0, 0, 1, 1);
    }

    void OnDestroy()
    {
        DestroyRope();
    }

    public void Initialize(Collider2D playerCol, bool canSpawnRope)
    {
        ResetIgnoredWall();
        _playerCollider = playerCol;
        _playerTransform = playerCol.transform;
        _collider.isTrigger = true;
        HasHitEnemy = false;
        _lastPosition = transform.position;
        _canSpawnRope = canSpawnRope;

        IgnoreAllPlayerColliders(true);
    }

    void FixedUpdate()
    {
        if (currentState == SpearState.Flying || currentState == SpearState.Returning)
        {
            CheckForTunneling();
        }

        if (currentState == SpearState.Flying || currentState == SpearState.Returning || currentState == SpearState.Dropped)
        {
            UpdateRotation();
        }
    }

    void Update()
    {
        if (currentState == SpearState.Returning) MoveTowardsPlayer();
    }

    private void UpdateRotation()
    {
        if (_rb.linearVelocity.sqrMagnitude > 0.5f)
        {
            transform.right = _rb.linearVelocity;
        }
    }

    private void CheckForTunneling()
    {
        Vector2 currentPosition = transform.position;
        Vector2 direction = currentPosition - _lastPosition;
        float distance = direction.magnitude;

        if (distance > 0.01f)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(_lastPosition, direction.normalized, distance);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && hit.collider != _collider && hit.collider.gameObject.layer != _spearLayer && !hit.transform.IsChildOf(transform))
                {
                    if (_playerCollider != null && hit.collider.transform.IsChildOf(_playerCollider.transform)) continue;
                    if (hit.collider.CompareTag(cameraBoundsTag)) continue;

                    ProcessHit(hit.collider, hit.point, hit.normal);
                    return;
                }
            }
        }
        _lastPosition = currentPosition;
    }

    public void OnTipHit(Collider2D other)
    {
        if (other.gameObject.layer == _spearLayer || other.transform.IsChildOf(transform)) return;
        if (currentState == SpearState.Embedded) return;
        if (other.CompareTag(cameraBoundsTag)) return;
        if (_playerCollider != null && other.transform.IsChildOf(_playerCollider.transform)) return;

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        Vector2 flyDir = _rb.linearVelocity.sqrMagnitude > 0.1f ? _rb.linearVelocity.normalized : (Vector2)transform.right;

        RaycastHit2D hit = Physics2D.Raycast(hitPoint - flyDir * 0.5f, flyDir, tipRaycastLength, 1 << other.gameObject.layer);
        Vector2 normal = hit.collider != null ? hit.normal : Vector2.up;

        ProcessHit(other, hitPoint, normal);
    }

    private void ProcessHit(Collider2D other, Vector2 hitPoint, Vector2 normal)
    {
        if (currentState == SpearState.Embedded) return;

        if (other.TryGetComponent(out IDamageable damageable))
        {
            if (currentState == SpearState.Flying)
            {
                HasHitEnemy = true;
                StickToTarget(other.transform, hitPoint, other, normal);
                damageable.TakeDamage(impactDamage, hitPoint, transform.position);
            }
            else if (currentState == SpearState.Returning)
            {
                if (!_enemiesHitDuringReturn.Contains(damageable))
                {
                    _enemiesHitDuringReturn.Add(damageable);
                    HasHitEnemy = true;
                    damageable.TakeDamage(recallDamage, hitPoint, transform.position);
                }
            }
        }
        else
        {
            if (currentState == SpearState.Flying || currentState == SpearState.Dropped)
            {
                StickToTarget(null, hitPoint, other, normal);
                CreateRope();
            }
        }
    }

    private void CreateRope()
    {
        if (_rope != null) return;
        if (!_canSpawnRope) return;

        _rope = new GameObject("Rope");
        _rope.transform.SetParent(transform, false);
        _rope.transform.localPosition = ropeAttachOffset;
        _rope.transform.rotation = Quaternion.identity;
        _rope.layer = _spearLayer;

        LineRenderer lr = _rope.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, Vector3.down * ropeLength);
        lr.startWidth = ropeWidth;
        lr.endWidth = ropeWidth;
        if (ropeMaterial != null) lr.sharedMaterial = ropeMaterial;
        lr.startColor = new Color(0.5f, 0.35f, 0.15f);
        lr.endColor = new Color(0.5f, 0.35f, 0.15f);

        EdgeCollider2D ec = _rope.AddComponent<EdgeCollider2D>();
        ec.points = new Vector2[] { Vector2.zero, Vector2.down * ropeLength };
        ec.isTrigger = true;

        _rope.AddComponent<Rope>();
    }

    private void DestroyRope()
    {
        if (_rope != null)
        {
            if (Application.isPlaying)
                Destroy(_rope);
            else
                DestroyImmediate(_rope);
            _rope = null;
        }
    }

    void StickToTarget(Transform target, Vector2 hitPoint, Collider2D surfaceCollider, Vector2 surfaceNormal)
    {
        Vector2 flyDirection = _rb.linearVelocity.sqrMagnitude > 0.1f ? _rb.linearVelocity.normalized : (Vector2)transform.right;
        Vector3 embedDirection = flyDirection;

        currentState = SpearState.Embedded;

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        if (target == null)
        {
            bool isWall = Mathf.Abs(surfaceNormal.x) > Mathf.Abs(surfaceNormal.y);

            if (isWall)
            {
                float targetAngle = (flyDirection.x > 0) ? 0f : 180f;
                transform.rotation = Quaternion.Euler(0, 0, targetAngle);
                embedDirection = (flyDirection.x > 0) ? Vector3.right : Vector3.left;
            }
            else
            {
                embedDirection = flyDirection;
            }
        }

        transform.position = (Vector3)hitPoint + embedDirection * embedDepth;

        if (_effector != null)
        {
            float currentAngle = transform.eulerAngles.z;
            if (currentAngle < 0) currentAngle += 360;
            _effector.rotationalOffset = (currentAngle > 90 && currentAngle < 270) ? 180 : 0;
        }

        if (target != null)
        {
            transform.SetParent(target);
            _collider.isTrigger = true;
        }
        else
        {
            _collider.isTrigger = false;

            if (surfaceCollider != null)
            {
                _ignoredEnvironmentCollider = surfaceCollider;
                Physics2D.IgnoreCollision(_collider, _ignoredEnvironmentCollider, true);
            }

            IgnoreAllPlayerColliders(false);
        }
    }

    public void StartReturn(Transform player)
    {
        DestroyRope();
        ResetIgnoredWall();
        _enemiesHitDuringReturn.Clear();

        if (transform.parent != null && transform.parent.TryGetComponent(out IDamageable embeddedEnemy))
        {
            _enemiesHitDuringReturn.Add(embeddedEnemy);
        }

        _playerTransform = player;
        _returnTimer = 0f;
        currentState = SpearState.Returning;
        _lastPosition = transform.position;

        transform.SetParent(null);
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearDamping = 0f;

        _collider.isTrigger = true;

        IgnoreAllPlayerColliders(true);
    }

    public void AbortReturn()
    {
        DestroyRope();
        ResetIgnoredWall();
        currentState = SpearState.Dropped;
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 1.5f;
        _rb.linearDamping = 2f;

        _collider.isTrigger = false;
    }

    void MoveTowardsPlayer()
    {
        if (_playerTransform == null) return;
        _returnTimer += Time.deltaTime;
        float timeNormalized = Mathf.Clamp01(_returnTimer / returnDuration);
        float currentSpeed = returnCurve.Evaluate(timeNormalized) * maxReturnSpeed;

        Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = direction * currentSpeed;

        if (Vector2.Distance(transform.position, _playerTransform.position) < catchDistance)
        {
            OnSpearReturned?.Invoke(this);
        }
    }

    public void OnEnemyDeath()
    {
        DestroyRope();
        ResetIgnoredWall();
        transform.SetParent(null);
        currentState = SpearState.Dropped;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 1.5f;

        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(new Vector2(UnityEngine.Random.Range(-6f, 6f), 10f), ForceMode2D.Impulse);

        _collider.isTrigger = false;
    }

    private void IgnoreAllPlayerColliders(bool ignore)
    {
        if (_playerCollider == null) return;
        Physics2D.IgnoreCollision(_collider, _playerCollider, ignore);
        Collider2D[] children = _playerCollider.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D c in children)
        {
            if (c != _playerCollider)
                Physics2D.IgnoreCollision(_collider, c, ignore);
        }
    }

    private void ResetIgnoredWall()
    {
        if (_ignoredEnvironmentCollider != null)
        {
            Physics2D.IgnoreCollision(_collider, _ignoredEnvironmentCollider, false);
            _ignoredEnvironmentCollider = null;
        }
    }
}
