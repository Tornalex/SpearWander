using UnityEngine;

/// <summary>
/// Releases a hanging stalactite when its SpearImpactCounter completes.
/// After detaching, the stalactite destroys itself on its first collision,
/// damaging the player or a damageable enemy it lands on.
/// </summary>
[RequireComponent(typeof(SpearImpactCounter), typeof(Rigidbody2D))]
public class DestructibleFallingStalactite : MonoBehaviour
{
    // Existing contact hazards (spikes, enemy contact) deal 1 damage.
    private const int ImpactDamage = 1;

    [SerializeField] private SpearImpactCounter impactCounter;

    [SerializeField, Min(0f)]
    [Tooltip("Gravity scale used while hanging. The Rigidbody2D's original gravity scale is restored on detach.")]
    private float hangingGravityScale = 0f;

    private Rigidbody2D _rb;
    private Collider2D[] _colliders;
    private float _fallGravityScale;
    private bool _hasDetached;
    private bool _isResolved;

    private void Awake()
    {
        if (impactCounter == null)
            impactCounter = GetComponent<SpearImpactCounter>();

        _rb = GetComponent<Rigidbody2D>();
        _colliders = GetComponentsInChildren<Collider2D>();

        if (impactCounter == null ||
            impactCounter.gameObject != gameObject)
        {
            Debug.LogError(
                "DestructibleFallingStalactite requires a SpearImpactCounter on the same GameObject.",
                this);
            impactCounter = null;
            enabled = false;
            return;
        }

        if (_rb == null)
        {
            Debug.LogError(
                "DestructibleFallingStalactite requires a Rigidbody2D on the same GameObject.",
                this);
            enabled = false;
            return;
        }

        _fallGravityScale = _rb.gravityScale;

        _rb.bodyType = RigidbodyType2D.Static;
        _rb.gravityScale = hangingGravityScale;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void OnEnable()
    {
        if (impactCounter != null && _rb != null)
        {
            impactCounter.OnCompleted -= HandleCompleted;
            impactCounter.OnCompleted += HandleCompleted;
        }
    }

    private void OnDisable()
    {
        if (impactCounter != null)
            impactCounter.OnCompleted -= HandleCompleted;
    }

    private void HandleCompleted(SpearImpactCounter counter)
    {
        if (_hasDetached || _rb == null)
            return;

        _hasDetached = true;

        IgnoreSpearCollisions();

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = _fallGravityScale;
        _rb.WakeUp();
    }

    private void IgnoreSpearCollisions()
    {
        Spear[] spears = FindObjectsByType<Spear>(
            FindObjectsInactive.Include);

        foreach (Spear spear in spears)
        {
            if (spear != null)
                IgnoreSpearColliders(spear);
        }
    }

    private void IgnoreSpearColliders(Spear spear)
    {
        if (spear == null || _colliders == null)
            return;

        Collider2D[] spearColliders =
            spear.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D spearCollider in spearColliders)
        {
            if (spearCollider == null)
                continue;

            foreach (Collider2D collider in _colliders)
            {
                if (collider == null || collider == spearCollider)
                    continue;

                Physics2D.IgnoreCollision(collider, spearCollider, true);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryResolveImpact(collision);
    }

    private void TryResolveImpact(Collision2D collision)
    {
        if (!_hasDetached || _isResolved || _rb == null)
            return;

        if (collision == null || collision.collider == null)
            return;

        Spear spear =
            collision.collider.GetComponentInParent<Spear>();

        if (spear != null)
        {
            // Handles a spear created after detaching; never treat a spear
            // contact as a valid impact.
            IgnoreSpearColliders(spear);
            return;
        }

        // Mark resolved before touching any target so later callbacks in the
        // same physics step cannot apply damage or destroy the object again.
        _isResolved = true;

        DisableColliders();

        Vector2 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : (Vector2)collision.collider.bounds.center;

        PlayerHealth playerHealth =
            collision.collider.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
        {
            // PlayerHealth owns the player's i-frames and knockback.
            playerHealth.TakeDamage(ImpactDamage, transform.position);
        }
        else
        {
            IDamageable damageable =
                collision.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                // Enemy implementations apply their own knockback.
                damageable.TakeDamage(
                    ImpactDamage,
                    hitPoint,
                    transform.position
                );
            }
        }

        Destroy(gameObject);
    }

    private void DisableColliders()
    {
        if (_colliders == null)
            return;

        foreach (Collider2D collider in _colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }
    }
}
