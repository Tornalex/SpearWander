using UnityEngine;

/// <summary>
/// Enemy designed to teach the player the Spear Recall mechanic.
///
/// Behavior:
/// - Maintains a preferred distance from the player.
/// - Periodically shoots projectiles at the player.
/// - Flashes red before shooting.
/// - Jumps when a thrown Spear is actually approaching it.
/// - Does not react to a Spear during Recall.
///
/// Intended interaction:
///
/// Throw Spear
///     ↓
/// Enemy jumps
///     ↓
/// Spear lands behind enemy
///     ↓
/// Recall
///     ↓
/// Spear passes through enemy
///     ↓
/// Recall damage
/// </summary>
public class RecallEnemy : BaseEnemy
{
    [Header("Movement")]

    [Tooltip("Preferred distance from the player.")]
    [SerializeField] private float preferredDistance = 6f;

    [Tooltip("Allowed distance around the preferred distance.")]
    [SerializeField] private float distanceTolerance = 1f;

    [Tooltip("Horizontal movement speed.")]
    [SerializeField] private float moveSpeed = 2f;


    [Header("Spear Dodge")]

    [Tooltip("Vertical velocity applied when dodging the Spear.")]
    [SerializeField] private float dodgeJumpForce = 8f;

    [Tooltip("Maximum distance at which the enemy reacts to the Spear.")]
    [SerializeField] private float dodgeDetectionDistance = 5f;

    [Tooltip("Minimum time between two dodge reactions.")]
    [SerializeField] private float dodgeCooldown = 0.75f;

    [Tooltip("How strongly the Spear must be moving toward the enemy.")]
    [Range(0f, 1f)]
    [SerializeField] private float spearDirectionThreshold = 0.7f;


    [Header("Ranged Attack")]

    [SerializeField] private GameObject projectilePrefab;

    [SerializeField] private Transform firePoint;

    [Tooltip("Time between shots.")]
    [SerializeField] private float attackInterval = 2f;

    [Tooltip("Projectile movement speed.")]
    [SerializeField] private float projectileSpeed = 7f;


    [Header("Ranged Attack Telegraph")]

    [Tooltip("How long the enemy warns the player before shooting.")]
    [SerializeField] private float attackTelegraphDuration = 0.5f;

    [Tooltip("Time between each red/normal flash.")]
    [SerializeField] private float flashInterval = 0.1f;

    [Tooltip("Color used during the attack warning.")]
    [SerializeField] private Color telegraphColor = Color.red;


    private Transform player;

    private float attackTimer;
    private float dodgeTimer;

    private Spear trackedSpear;

    private bool isTelegraphing;
    private float telegraphTimer;
    private float flashTimer;
    private bool isFlashing;


    protected override void Awake()
    {
        base.Awake();

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }


    protected override void Start()
    {
        base.Start();

        attackTimer = attackInterval;
    }


    private void Update()
    {
        if (isDead || player == null)
            return;

        UpdateTimers();

        MaintainDistance();

        CheckForIncomingSpear();

        HandleRangedAttack();
    }


    private void UpdateTimers()
    {
        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        if (dodgeTimer > 0f)
            dodgeTimer -= Time.deltaTime;
    }


    // ============================================================
    // MOVEMENT
    // ============================================================

    private void MaintainDistance()
    {
        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );


        // Player is too close.
        // Move away from the player.
        if (distance < preferredDistance - distanceTolerance)
        {
            Vector2 direction =
                ((Vector2)transform.position -
                 (Vector2)player.position).normalized;

            rb.linearVelocity = new Vector2(
                direction.x * moveSpeed,
                rb.linearVelocity.y
            );

            return;
        }


        // Player is too far.
        // Move toward the player.
        if (distance > preferredDistance + distanceTolerance)
        {
            Vector2 direction =
                ((Vector2)player.position -
                 (Vector2)transform.position).normalized;

            rb.linearVelocity = new Vector2(
                direction.x * moveSpeed,
                rb.linearVelocity.y
            );

            return;
        }


        // Player is inside the preferred distance range.
        // Stop horizontal movement.
        rb.linearVelocity = new Vector2(
            0f,
            rb.linearVelocity.y
        );
    }


    // ============================================================
    // SPEAR DODGE
    // ============================================================

    private void CheckForIncomingSpear()
    {
        // We are currently tracking a Spear.
        // Check whether it is still valid.
        if (trackedSpear != null)
        {
            if (trackedSpear.currentState != Spear.SpearState.Flying)
            {
                trackedSpear = null;
            }
        }


        // No Spear currently tracked.
        if (trackedSpear == null)
        {
            FindIncomingSpear();
        }


        if (trackedSpear == null)
            return;


        // Cannot dodge again while on cooldown.
        if (dodgeTimer > 0f)
            return;


        Rigidbody2D spearRb =
            trackedSpear.GetComponent<Rigidbody2D>();

        if (spearRb == null)
            return;


        Vector2 spearVelocity =
            spearRb.linearVelocity;


        // Spear is not moving.
        if (spearVelocity.sqrMagnitude < 0.01f)
            return;


        Vector2 spearPosition =
            trackedSpear.transform.position;


        Vector2 toEnemy =
            (Vector2)transform.position -
            spearPosition;


        float distance =
            toEnemy.magnitude;


        // Too far away to react.
        if (distance > dodgeDetectionDistance)
            return;


        Vector2 spearDirection =
            spearVelocity.normalized;


        Vector2 directionToEnemy =
            toEnemy.normalized;


        float alignment =
            Vector2.Dot(
                spearDirection,
                directionToEnemy
            );


        // Positive value = Spear is moving toward us.
        //
        // 1.0 = directly toward the enemy
        // 0.0 = perpendicular
        // -1.0 = directly away
        if (alignment < spearDirectionThreshold)
            return;


        DodgeSpear();
    }


    private void FindIncomingSpear()
    {
        Spear[] spears = FindObjectsByType<Spear>();

        foreach (Spear spear in spears)
        {
            if (spear == null)
                continue;

            // IMPORTANT:
            // Only react to a thrown Spear.
            //
            // Returning is deliberately ignored.
            if (spear.currentState != Spear.SpearState.Flying)
                continue;


            Rigidbody2D spearRb =
                spear.GetComponent<Rigidbody2D>();

            if (spearRb == null)
                continue;


            Vector2 velocity =
                spearRb.linearVelocity;


            if (velocity.sqrMagnitude < 0.01f)
                continue;


            Vector2 toEnemy =
                (Vector2)transform.position -
                (Vector2)spear.transform.position;


            float distance =
                toEnemy.magnitude;


            if (distance > dodgeDetectionDistance)
                continue;


            Vector2 spearDirection =
                velocity.normalized;


            Vector2 directionToEnemy =
                toEnemy.normalized;


            float alignment =
                Vector2.Dot(
                    spearDirection,
                    directionToEnemy
                );


            if (alignment >= spearDirectionThreshold)
            {
                trackedSpear = spear;
                return;
            }
        }
    }


    private void DodgeSpear()
    {
        dodgeTimer = dodgeCooldown;

        // Preserve horizontal movement.
        // Only add the vertical dodge.
        rb.linearVelocity = new Vector2(
            rb.linearVelocity.x,
            dodgeJumpForce
        );

        trackedSpear = null;
    }


    // ============================================================
    // RANGED ATTACK
    // ============================================================

    private void HandleRangedAttack()
    {
        if (isTelegraphing)
        {
            UpdateAttackTelegraph();
            return;
        }


        if (attackTimer > 0f)
            return;


        StartAttackTelegraph();
    }


    private void StartAttackTelegraph()
    {
        isTelegraphing = true;

        telegraphTimer =
            attackTelegraphDuration;

        flashTimer = 0f;

        isFlashing = false;
    }


    private void UpdateAttackTelegraph()
    {
        telegraphTimer -= Time.deltaTime;
        flashTimer -= Time.deltaTime;


        if (flashTimer <= 0f)
        {
            flashTimer = flashInterval;

            isFlashing = !isFlashing;

            spriteRenderer.color =
                isFlashing
                    ? telegraphColor
                    : Color.white;
        }


        if (telegraphTimer <= 0f)
        {
            isTelegraphing = false;

            spriteRenderer.color = Color.white;

            Shoot();

            attackTimer = attackInterval;
        }
    }


    private void Shoot()
    {
        if (projectilePrefab == null)
            return;

        if (firePoint == null)
            return;

        if (player == null)
            return;


        GameObject projectile =
            Instantiate(
                projectilePrefab,
                firePoint.position,
                Quaternion.identity
            );


        Vector2 direction =
            (
                (Vector2)player.position -
                (Vector2)firePoint.position
            ).normalized;


        EnemyProjectile enemyProjectile =
            projectile.GetComponent<EnemyProjectile>();


        if (enemyProjectile != null)
        {
            enemyProjectile.Launch(
                direction,
                projectileSpeed
            );
        }
    }
}