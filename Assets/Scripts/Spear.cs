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
    private Transform _playerTransform;
    private PlayerCombat _playerCombat;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
    }

    void FixedUpdate()
    {
        if (currentState == SpearState.Flying || currentState == SpearState.Dropped)
        {
            if (_rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                transform.right = _rb.linearVelocity;
            }
        }
    }

    void Update()
    {
        if (currentState == SpearState.Returning)
        {
            MoveTowardsPlayer();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState != SpearState.Flying && currentState != SpearState.Dropped) return;

        if (collision.gameObject.CompareTag("Enemy"))
        {
            Enemy enemy = collision.gameObject.GetComponent<Enemy>();
            if (enemy != null && currentState == SpearState.Flying)
            {
                ContactPoint2D contact = collision.contacts[0];
                StickToTarget(collision.transform, contact);
                enemy.TakeDamage(impactDamage);
            }
        }
        else if (Mathf.Abs(collision.contacts[0].normal.x) > 0.7f && currentState == SpearState.Flying)
        {
            StickToTarget(null, collision.contacts[0]);
        }
        else if (currentState == SpearState.Dropped)
        {
            StopPhysics();
        }
    }

    void StickToTarget(Transform target, ContactPoint2D contact)
    {
        currentState = SpearState.Embedded;

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        
        transform.position = contact.point;

        if (target != null)
        {
            transform.SetParent(target);
        }

        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.simulated = true;
        _collider.isTrigger = true;
    }

    void StopPhysics()
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Static;
        _collider.isTrigger = false;
    }

    public void StartReturn(Transform player, PlayerCombat combat)
    {
        _playerTransform = player;
        _playerCombat = combat;
        
        currentState = SpearState.Returning;
        _returnTimer = 0f;
        
        transform.SetParent(null);
        _rb.bodyType = RigidbodyType2D.Kinematic; 
        _rb.linearVelocity = Vector2.zero;
        _collider.isTrigger = true;
    }

    void MoveTowardsPlayer()
    {
        _returnTimer += Time.deltaTime;
        float timeNormalized = Mathf.Clamp01(_returnTimer / returnDuration);
        
        float curveValue = returnCurve.Evaluate(timeNormalized);
        float currentSpeed = curveValue * maxReturnSpeed;

        Vector2 direction = ((Vector2)_playerTransform.position - (Vector2)transform.position).normalized;
        _rb.linearVelocity = direction * currentSpeed;

        if (_rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            transform.right = _rb.linearVelocity;
        }

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
        _rb.gravityScale = 1;
        _collider.isTrigger = false;
        _rb.AddForce(new Vector2(Random.Range(-5f, 5f), 8f), ForceMode2D.Impulse);
    }
}