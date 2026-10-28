using UnityEngine;

public class WalkingEnemy : BaseEnemy, IPlayerDamageReceiver
{
    private enum AIState
    {
        Patrol,
        Chase
    }

    [Header("Player Detection")]
    [SerializeField, Min(0f)]
    [Tooltip("Maximum distance at which a player in front can start a chase.")]
    private float playerAcquisitionRadius = 6f;

    [SerializeField, Min(0f)]
    [Tooltip("Distance after which a chased player is lost. Should be larger than the acquisition radius.")]
    private float playerAggroRadius = 10f;

    [SerializeField, Min(0f)]
    [Tooltip("Horizontal dead zone used to avoid rapid direction changes while chasing.")]
    private float chaseDirectionDeadzone = 0.4f;

    [Header("Obstacle Detection")]
    [SerializeField, Min(0f)]
    [Tooltip("Extra distance beyond the enemy collider used for wall checks.")]
    private float wallCheckDistance = 0.1f;

    [SerializeField, Min(0f)]
    [Tooltip("Distance below the front of the enemy used to find ground.")]
    private float groundCheckDistance = 0.75f;

    [SerializeField, Min(0f)]
    [Tooltip("Forward offset from the enemy collider edge used for the ground check.")]
    private float groundCheckForwardOffset = 0.15f;

    [SerializeField]
    [Tooltip("Layers containing walkable ground.")]
    private LayerMask groundLayers = 1;

    [SerializeField]
    [Tooltip("Layers containing walls and other solid obstacles.")]
    private LayerMask obstacleLayers = 1;

    // speed preso da enemyData.speed
    private Vector2 movementDirection = Vector2.right;
    private AIState state = AIState.Patrol;

    private Collider2D bodyCollider;
    private Transform player;
    private Collider2D playerCollider;

    // Reused by all queries to avoid per-frame allocations.
    private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[8];

    private float AggroRadius =>
        Mathf.Max(playerAggroRadius, playerAcquisitionRadius);

    protected override void Awake()
    {
        base.Awake();

        bodyCollider = GetComponent<Collider2D>();
        CachePlayer();
    }

    public bool TakeDamageFromPlayer(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
    {
        bool damageApplied = TakeDamage(damage, hitPoint, damageSourcePosition);

        if (damageApplied && !isDead)
            TryAcquirePlayerFromDamage();

        return damageApplied;
    }

    private void TryAcquirePlayerFromDamage()
    {
        if (state == AIState.Chase)
            return;

        if (player == null)
            CachePlayer();

        if (player == null || !IsWithinAggroRadius())
            return;

        // Damage cannot justify chasing a player who is still separated from
        // the enemy by unreachable solid terrain.
        if (IsPlayerAboveSolidPlatform())
            return;

        // Damage-triggered acquisition ignores the front-facing and line-of-sight
        // checks, so turn toward the player explicitly before entering Chase.
        float horizontalOffset = player.position.x - transform.position.x;
        if (Mathf.Abs(horizontalOffset) > 0.01f)
        {
            float desiredDirection = Mathf.Sign(horizontalOffset);
            if (!Mathf.Approximately(desiredDirection, movementDirection.x))
            {
                FlipDirection();
            }
        }

        state = AIState.Chase;
    }

    void FixedUpdate()
    {
        if (isDead || (knockback != null && knockback.IsKnockedBack) || enemyData == null) return;

        if (state == AIState.Chase)
        {
            UpdateChase();
        }
        else
        {
            UpdatePatrol();
        }

        Move();
    }

    private void UpdatePatrol()
    {
        // Do not walk into a wall or off a ledge. Turning first also makes
        // acquisition use the direction in which the enemy can actually move.
        if (IsPathBlockedAhead())
        {
            FlipDirection();
        }

        if (CanAcquirePlayer())
        {
            state = AIState.Chase;
        }
    }

    private void UpdateChase()
    {
        if (player == null || !IsWithinAggroRadius())
        {
            state = AIState.Patrol;
            UpdatePatrol();
            return;
        }

        // A player standing on a platform above the enemy cannot be reached by
        // horizontal movement. Keep the current movement direction and resume
        // patrol instead of orbiting below the player's horizontal position.
        if (IsPlayerAboveSolidPlatform())
        {
            state = AIState.Patrol;
            UpdatePatrol();
            return;
        }

        // Turn toward the player only when the player is clearly on the other
        // side. The dead zone prevents oscillation around the enemy's position.
        float horizontalOffset = player.position.x - transform.position.x;
        if (Mathf.Abs(horizontalOffset) > chaseDirectionDeadzone)
        {
            float desiredDirection = Mathf.Sign(horizontalOffset);
            if (!Mathf.Approximately(desiredDirection, movementDirection.x))
            {
                FlipDirection();
            }
        }

        // A wall or ledge makes the target unreachable with horizontal movement.
        // Turn away from the obstacle and resume patrol.
        if (IsPathBlockedAhead())
        {
            FlipDirection();
            state = AIState.Patrol;
        }
    }

    private void Move()
    {
        rb.linearVelocity = new Vector2(movementDirection.x * enemyData.speed, rb.linearVelocity.y);

        if (animator != null)
            animator.SetFloat("Speed", Mathf.Abs(movementDirection.x * enemyData.speed));
    }

    private bool CanAcquirePlayer()
    {
        if (player == null) return false;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        if (toPlayer.sqrMagnitude > playerAcquisitionRadius * playerAcquisitionRadius) return false;

        // Do not immediately reacquire a player that is still on a platform
        // above solid terrain. It becomes acquirable again once it can be
        // reached without jumping or climbing.
        if (IsPlayerAboveSolidPlatform()) return false;

        // Acquisition is directional: the player must be in front of the enemy.
        if (toPlayer.x * movementDirection.x <= 0f) return false;

        return HasLineOfSightToPlayer();
    }

    private bool IsWithinAggroRadius()
    {
        if (player == null) return false;

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float aggroRadius = AggroRadius;
        return toPlayer.sqrMagnitude <= aggroRadius * aggroRadius;
    }

    private bool HasLineOfSightToPlayer()
    {
        Vector2 origin = bodyCollider != null
            ? (Vector2)bodyCollider.bounds.center
            : (Vector2)transform.position;

        Vector2 target = playerCollider != null && playerCollider.enabled
            ? (Vector2)playerCollider.bounds.center
            : (Vector2)player.position;

        Vector2 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 0.01f) return true;

        int hitCount = Physics2D.RaycastNonAlloc(
            origin,
            delta / distance,
            raycastHits,
            distance,
            obstacleLayers
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = raycastHits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;
            return false;
        }

        return true;
    }

    private bool IsPlayerAboveSolidPlatform()
    {
        if (bodyCollider == null || playerCollider == null || !playerCollider.enabled)
            return false;

        Bounds enemyBounds = bodyCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;

        // The player must be at or above the top of the enemy body. A player
        // merely jumping above the enemy is still a valid target unless solid
        // terrain actually separates them.
        if (playerBounds.min.y < enemyBounds.max.y)
            return false;

        float startY = enemyBounds.max.y;
        float endY = playerBounds.center.y;

        // Check both columns: the platform under the player and the space the
        // enemy would have to move through. This also covers players standing
        // close to a platform edge.
        return HasSolidTerrainBetween(playerBounds.center.x, startY, endY)
            || HasSolidTerrainBetween(enemyBounds.center.x, startY, endY);
    }

    private bool HasSolidTerrainBetween(float x, float startY, float endY)
    {
        if (endY <= startY)
            return false;

        int hitCount = Physics2D.RaycastNonAlloc(
            new Vector2(x, startY),
            Vector2.up,
            raycastHits,
            endY - startY,
            groundLayers.value | obstacleLayers.value
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = raycastHits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger) continue;
            if (hitCollider == playerCollider) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;
            return true;
        }

        return false;
    }

    private bool IsPathBlockedAhead()
    {
        return IsWallAhead() || !HasGroundAhead();
    }

    private bool IsWallAhead()
    {
        if (bodyCollider == null) return false;

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = bounds.center;
        float distance = bounds.extents.x + wallCheckDistance;

        int hitCount = Physics2D.RaycastNonAlloc(
            origin,
            movementDirection,
            raycastHits,
            distance,
            obstacleLayers
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = raycastHits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;
            return true;
        }

        return false;
    }

    private bool HasGroundAhead()
    {
        if (bodyCollider == null) return true;

        Bounds bounds = bodyCollider.bounds;
        float direction = Mathf.Sign(movementDirection.x);

        float checkX = (direction > 0f ? bounds.max.x : bounds.min.x)
            + direction * groundCheckForwardOffset;

        // Start slightly above the collider bottom so the ray can leave the
        // enemy body and still detect ground when the collider overlaps it.
        float checkY = bounds.min.y + 0.2f;
        Vector2 origin = new Vector2(checkX, checkY);

        int hitCount = Physics2D.RaycastNonAlloc(
            origin,
            Vector2.down,
            raycastHits,
            groundCheckDistance,
            groundLayers
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = raycastHits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;
            return true;
        }

        return false;
    }

    private void CachePlayer()
    {
        Player playerComponent = FindAnyObjectByType<Player>();
        if (playerComponent != null)
        {
            player = playerComponent.transform;
            playerCollider = playerComponent.Collider;
            return;
        }

        // Fallback for scenes where the player is only identifiable by tag.
        GameObject taggedPlayer = GameObject.FindWithTag("Player");
        if (taggedPlayer == null) return;

        playerComponent = taggedPlayer.GetComponentInParent<Player>();
        if (playerComponent != null)
        {
            player = playerComponent.transform;
            playerCollider = playerComponent.Collider;
        }
        else
        {
            player = taggedPlayer.transform;
            playerCollider = taggedPlayer.GetComponent<Collider2D>();
        }
    }

    private void FlipDirection()
    {
        movementDirection = -movementDirection;

        if (spriteRenderer != null)
            spriteRenderer.flipX = !spriteRenderer.flipX;
    }
}
