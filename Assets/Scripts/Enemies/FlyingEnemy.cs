using UnityEngine;

public class FlyingEnemy : BaseEnemy
{
    [Header("Flight Settings")]
    [SerializeField] private float flySpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float patrolRadius = 3f;
    [SerializeField] private float detectionRadius = 7f;
    [SerializeField] private float loseRadius = 12f;

    [Header("Stun Settings")]
    [SerializeField] private float stunDuration = 0.5f;

    private Vector2 _startPosition;
    private Vector2 _targetPatrolPoint;
    private Transform _player;
    private bool _isStunned = false;
    private bool _isChasing = false;
    private float _stunTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        
        _startPosition = transform.position;
        
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;

        SetNewPatrolPoint();
    }

    void FixedUpdate()
    {
        if (isDead || _player == null) return;

        if (_isStunned)
        {
            _stunTimer -= Time.fixedDeltaTime;
            if (_stunTimer <= 0) _isStunned = false;
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, _player.position);
        _isChasing = distanceToPlayer < (_isChasing ? loseRadius : detectionRadius);

        if (_isChasing) ChasePlayer();
        else Patrol();

        FlipSprite();
    }

    public override void TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
    {
        base.TakeDamage(damage, hitPoint, damageSourcePosition); // Esegue logica base (vita, flash, knockback)

        if (!isDead)
        {
            ApplyStun();
            if (Vector2.Distance(transform.position, _player.position) < loseRadius) _isChasing = true;
        }
    }

    public override void OnPogoBounce()
    {
        base.OnPogoBounce();
        ApplyStun();
    }

    private void ApplyStun()
    {
        _isStunned = true;
        _stunTimer = stunDuration;
        rb.linearVelocity = Vector2.zero;
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
        float dirX = _isChasing ? (_player.position.x - transform.position.x) : (_targetPatrolPoint.x - transform.position.x);
        if (Mathf.Abs(dirX) > 0.1f) spriteRenderer.flipX = dirX < 0; // 'spriteRenderer' viene da BaseEnemy
    }
}