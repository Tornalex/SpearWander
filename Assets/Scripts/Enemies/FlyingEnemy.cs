using UnityEngine;

public class FlyingEnemy : MonoBehaviour, IDamageable, IBounceable
{
    [Header("Stats")]
    [SerializeField] private int life = 2;
    [SerializeField] private float flySpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 4.5f;
    
    [Header("Movement Limits")]
    [SerializeField] private float patrolRadius = 3f;
    [SerializeField] private float detectionRadius = 7f;
    [SerializeField] private float loseRadius = 12f;

    [Header("Stun Settings")]
    [SerializeField] private float stunDuration = 0.5f;

    private Vector2 _startPosition;
    private Vector2 _targetPatrolPoint;
    private Transform _player;
    private Rigidbody2D _rb;
    private SpriteRenderer _sprite;
    private HitFlash _hitFlash;
    private bool _isDead = false;
    private bool _isStunned = false;
    private bool _isChasing = false;
    private float _stunTimer = 0f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sprite = GetComponent<SpriteRenderer>();
        _hitFlash = GetComponent<HitFlash>();
        _startPosition = transform.position;
        
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        SetNewPatrolPoint();
    }

    void FixedUpdate()
    {
        if (_isDead || _player == null) return;

        if (_isStunned)
        {
            _stunTimer -= Time.fixedDeltaTime;
            if (_stunTimer <= 0) _isStunned = false;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);

        if (!_isChasing)
        {
            if (distanceToPlayer < detectionRadius) _isChasing = true;
        }
        else
        {
            if (distanceToPlayer > loseRadius) _isChasing = false;
        }

        if (_isChasing) ChasePlayer();
        else Patrol();

        FlipSprite();
    }

    private void Patrol()
    {
        transform.position = Vector2.MoveTowards(transform.position, _targetPatrolPoint, flySpeed * Time.fixedDeltaTime);
        if (Vector2.Distance(transform.position, _targetPatrolPoint) < 0.2f) SetNewPatrolPoint();
    }

    private void SetNewPatrolPoint()
    {
        _targetPatrolPoint = _startPosition + (Random.insideUnitCircle * patrolRadius);
    }

    private void ChasePlayer()
    {
        transform.position = Vector2.MoveTowards(transform.position, _player.position, chaseSpeed * Time.fixedDeltaTime);
    }

    private void FlipSprite()
    {
        float directionX = _isChasing ? (_player.position.x - transform.position.x) : (_targetPatrolPoint.x - transform.position.x);
        if (Mathf.Abs(directionX) > 0.1f) _sprite.flipX = directionX < 0;
    }

    public void TakeDamage(int damage, Vector2 hitPoint)
    {
        if (_isDead) return;

        life -= damage;
        _isStunned = true;
        _stunTimer = stunDuration;
        _rb.linearVelocity = Vector2.zero;

        if (_player != null && Vector2.Distance(transform.position, _player.position) < loseRadius) _isChasing = true;
        
        if (_hitFlash != null) _hitFlash.Flash();
        SFXManager.Instance.PlaySFX(SFXType.EnemyPierced);
        VFXManager.Instance.PlayVFX(VFXType.HitDash, hitPoint, (hitPoint - (Vector2)transform.position).normalized);

        if (life <= 0) Die();
    }

    public float GetBounceMultiplier() => 1f;

    public void OnPogoBounce()
    {
        _isStunned = true;
        _stunTimer = stunDuration;
        if (_hitFlash != null) _hitFlash.Flash();
    }

    private void Die()
    {
        _isDead = true;
        Spear[] attachedSpears = GetComponentsInChildren<Spear>();
        foreach (Spear spear in attachedSpears) spear.OnEnemyDeath();
        Destroy(gameObject, 0.05f);
    }
}