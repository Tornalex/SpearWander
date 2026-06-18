using UnityEngine;
using System.Collections.Generic;
using System; // Aggiunto per poter usare le Action (Eventi)

public class Spear : MonoBehaviour
{
    public enum SpearState { Flying, Embedded, Returning, Dropped }
    public SpearState currentState = SpearState.Flying;

    // EVENTO: Urla a chiunque sia in ascolto che la lancia è tornata a casa
    public event Action<Spear> OnSpearReturned;

    [Header("Settings - Damage & Flight")]
    [SerializeField] private AnimationCurve returnCurve;
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private float maxReturnSpeed = 50f;
    [SerializeField] private int impactDamage = 1;
    [SerializeField] private int recallDamage = 1;

    [Header("Settings - Physics & Tags")]
    [Tooltip("Il Tag assegnato ai confini invisibili della telecamera")]
    [SerializeField] private string cameraBoundsTag = "CameraBounds";
    [Tooltip("Profondità con cui la lancia si conficca nei muri")]
    [SerializeField] private float embedDepth = 0.15f;
    [Tooltip("Distanza di intercettazione per afferrare la lancia di ritorno")]
    [SerializeField] private float catchDistance = 0.7f;
    [Tooltip("Lunghezza del raycast per rilevare la normal del muro")]
    [SerializeField] private float tipRaycastLength = 1.5f;

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

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();
        _spearLayer = LayerMask.NameToLayer("Spear");
        gameObject.layer = _spearLayer;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void Initialize(Collider2D playerCol)
    {
        ResetIgnoredWall();
        _playerCollider = playerCol;
        _collider.isTrigger = true; 
        HasHitEnemy = false;
        _lastPosition = transform.position;

        if (_playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider, _playerCollider, true);
        }
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
                    if (_playerCollider != null && hit.collider == _playerCollider) continue;
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
        if (_playerCollider != null && other == _playerCollider) return;

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        Vector2 flyDir = _rb.linearVelocity.sqrMagnitude > 0.1f ? _rb.linearVelocity.normalized : (Vector2)transform.right;
        
        RaycastHit2D hit = Physics2D.Raycast(hitPoint - flyDir * 0.5f, flyDir, tipRaycastLength, 1 << other.gameObject.layer);
        Vector2 normal = hit.collider != null ? hit.normal : Vector2.up;

        ProcessHit(other, hitPoint, normal);
    }

    private void ProcessHit(Collider2D other, Vector2 hitPoint, Vector2 normal)
    {
        if (currentState == SpearState.Embedded) return;

        // REFRACTORING INTERFACCIA: Non cerchiamo più il tag "Enemy", ma qualsiasi cosa sia danneggiabile!
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
            }
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

            if (_playerCollider != null)
            {
                Physics2D.IgnoreCollision(_collider, _playerCollider, false);
            }
        }
    }

    public void StartReturn(Transform player)
    {
        ResetIgnoredWall();
        _enemiesHitDuringReturn.Clear();

        // ====================================================================
        // FIX: IGNORA IL NEMICO IN CUI LA LANCIA È CONFICCATA
        // Controlliamo se siamo figli di un oggetto con l'interfaccia IDamageable
        // PRIMA di staccare il transform con SetParent(null)
        // ====================================================================
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

        if (_playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider, _playerCollider, true);
        }
    }

    public void AbortReturn()
    {
        ResetIgnoredWall();
        currentState = SpearState.Dropped;
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 1.5f;
        _rb.linearDamping = 2f;

        _collider.isTrigger = false;
    }

    void MoveTowardsPlayer()
    {
        _returnTimer += Time.deltaTime;
        float timeNormalized = Mathf.Clamp01(_returnTimer / returnDuration);
        float currentSpeed = returnCurve.Evaluate(timeNormalized) * maxReturnSpeed;

        Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = direction * currentSpeed;

        if (Vector2.Distance(transform.position, _playerTransform.position) < catchDistance)
        {
            // ATTIVA L'EVENTO: Chiunque stia ascoltando saprà che la lancia è tornata
            OnSpearReturned?.Invoke(this);
        }
    }

    public void OnEnemyDeath()
    {
        ResetIgnoredWall();
        transform.SetParent(null);
        currentState = SpearState.Dropped;

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 1.5f;

        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(new Vector2(UnityEngine.Random.Range(-6f, 6f), 10f), ForceMode2D.Impulse);

        _collider.isTrigger = false;
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