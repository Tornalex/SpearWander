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

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();
        _spearLayer = LayerMask.NameToLayer("Spear");
        
        gameObject.layer = _spearLayer;
        foreach (Transform child in transform) child.gameObject.layer = _spearLayer;
    }

    // Viene chiamato dal Player nel momento esatto del lancio
    public void Initialize(Collider2D playerCol)
    {
        _playerCollider = playerCol;
        _collider.isTrigger = false; // Solida per poter sbattere contro il muro

        // IGNORIAMO fisicamente il giocatore mentre vola! (Non ci respingerà più)
        if (_playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider, _playerCollider, true);
        }
    }

    void FixedUpdate()
    {
        if (currentState == SpearState.Flying || currentState == SpearState.Returning)
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
        if (_rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            transform.right = _rb.linearVelocity;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == _spearLayer) return;
        if (currentState != SpearState.Flying) return;

        if (collision.gameObject.CompareTag("Enemy"))
        {
            HandleEnemyHit(collision);
        }
        else
        {
            StickToTarget(null, collision.contacts[0]);
        }
    }

    private void HandleEnemyHit(Collision2D collision)
    {
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(impactDamage);
            StickToTarget(collision.transform, collision.contacts[0]);
        }
    }

    void StickToTarget(Transform target, ContactPoint2D contact)
    {
        currentState = SpearState.Embedded;

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        transform.position = contact.point;

        // Gestione Rotazione e PlatformEffector
        if (Mathf.Abs(contact.normal.x) > 0.7f)
        {
            bool isFacingLeft = contact.normal.x < 0;
            transform.rotation = Quaternion.Euler(0, 0, isFacingLeft ? 180 : 0);

            if (_effector != null)
            {
                _effector.rotationalOffset = isFacingLeft ? 180 : 0;
            }
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
        _rb.AddForce(new Vector2(Random.Range(-4f, 4f), 7f), ForceMode2D.Impulse);
        
        _collider.isTrigger = false;
    }
}