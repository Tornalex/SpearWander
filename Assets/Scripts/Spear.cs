using UnityEngine;

public class Spear : MonoBehaviour
{
    public enum SpearState { Flying, Embedded, Returning, Dropped }
    public SpearState currentState = SpearState.Flying;

    [Header("Settings")]
    [SerializeField] private AnimationCurve returnCurve;
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private float maxReturnSpeed = 50f;
    [SerializeField] private int impactDamage = 1;

    private float _returnTimer;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private PlatformEffector2D _effector;
    private Collider2D _playerCollider;
    private Transform _playerTransform;
    private PlayerCombat _playerCombat;
    private int _spearLayer;

    public bool HasHitEnemy { get; private set; }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();
        _spearLayer = LayerMask.NameToLayer("Spear");
    
        gameObject.layer = _spearLayer;
        foreach (Transform child in transform) child.gameObject.layer = _spearLayer;
    }

    public void Initialize(Collider2D playerCol)
    {
        _playerCollider = playerCol;
        _collider.isTrigger = false;
        HasHitEnemy = false;

        if (_playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider, _playerCollider, true);
        }
    }

    void FixedUpdate()
    {
        // Aggiorniamo la rotazione anche quando cade (Dropped) per realismo
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
        // Evitiamo rotazioni strane se la velocità è quasi nulla
        if (_rb.linearVelocity.sqrMagnitude > 0.5f)
        {
            transform.right = _rb.linearVelocity;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == _spearLayer) return;

        // MODIFICA QUI: Permettiamo la collisione se Flying O Dropped
        if (currentState == SpearState.Flying || currentState == SpearState.Dropped)
        {
            if (collision.gameObject.CompareTag("Enemy"))
            {
                // Solo se sta volando (lanciata) può danneggiare e piantarsi nei nemici
                if (currentState == SpearState.Flying)
                {
                    HandleEnemyHit(collision);
                }
                // Se è 'Dropped', ignoriamo la collisione fisica con i nemici (ci passa attraverso)
                // o puoi lasciarla rimbalzare non mettendo nulla qui.
            }
            else // Probabilmente Environment/Wall/Ground
            {
                // Sia Flying che Dropped si conficcano nel terreno
                StickToTarget(null, collision.contacts[0]);
            }
        }
    }

    private void HandleEnemyHit(Collision2D collision)
    {
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            HasHitEnemy = true;
            StickToTarget(collision.transform, collision.contacts[0]);
            damageable.TakeDamage(impactDamage, collision.contacts[0].point);
        }
    }

    void StickToTarget(Transform target, ContactPoint2D contact)
    {
        // Se la velocità è zero (es. appena caduta), usiamo transform.right come direzione di caduta
        Vector2 flyDirection = _rb.linearVelocity.sqrMagnitude > 0.1f ? _rb.linearVelocity.normalized : (Vector2)transform.right;
        
        currentState = SpearState.Embedded;

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        // Snap orizzontale solo su pareti verticali
        if (Mathf.Abs(contact.normal.x) > 0.5f) 
        {
            float targetAngle = (flyDirection.x > 0) ? 0f : 180f;
            transform.rotation = Quaternion.Euler(0, 0, targetAngle);
        }

        transform.position = (Vector3)contact.point + (Vector3)flyDirection * 0.15f;

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
            if (_playerCollider != null)
            {
                Physics2D.IgnoreCollision(_collider, _playerCollider, false);
            }
        }
    }

    public void StartReturn(Transform player, PlayerCombat combat)
    {
        _playerTransform = player;
        _playerCombat = combat;
        _returnTimer = 0f;
        currentState = SpearState.Returning;
        
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

        if (Vector2.Distance(transform.position, _playerTransform.position) < 0.7f)
        {
            _playerCombat.CatchSpear(this);
        }
    }

    public void OnEnemyDeath()
    {
        transform.SetParent(null);
        currentState = SpearState.Dropped;
        
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = 1.5f;
        
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(new Vector2(Random.Range(-6f, 6f), 10f), ForceMode2D.Impulse);
        
        _collider.isTrigger = false;
    }
}