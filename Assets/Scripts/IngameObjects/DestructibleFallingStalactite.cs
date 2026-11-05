using System.Collections.Generic;
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

    private struct InitialOverlapPair
    {
        public Collider2D stalactiteCollider;
        public Collider2D otherCollider;
    }

    [SerializeField] private SpearImpactCounter impactCounter;

    [SerializeField, Min(0f)]
    [Tooltip("Gravity scale used while hanging. The Rigidbody2D's original gravity scale is restored on detach.")]
    private float hangingGravityScale = 0f;

    private Rigidbody2D _rb;
    private Collider2D[] _colliders;
    private float _fallGravityScale;
    private bool _hasDetached;
    private bool _isResolved;

    private readonly List<InitialOverlapPair> _initialOverlapPairs =
        new List<InitialOverlapPair>();

    private readonly List<Collider2D> _overlapBuffer =
        new List<Collider2D>(8);

    private ContactFilter2D _initialOverlapFilter;

    private void Awake()
    {
        if (impactCounter == null)
            impactCounter = GetComponent<SpearImpactCounter>();

        _rb = GetComponent<Rigidbody2D>();
        RefreshColliders();

        _initialOverlapFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false,
            useOutsideNormalAngle = false
        };

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

        if (_rb.bodyType != RigidbodyType2D.Static)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        _rb.gravityScale = hangingGravityScale;
        _rb.bodyType = RigidbodyType2D.Static;
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

    private void FixedUpdate()
    {
        // Spears created after detaching, and colliders created or enabled
        // later (such as the spear rope), are not covered by the snapshot
        // taken in HandleCompleted. Reapply the ignores every physics step
        // of the fall so those pairs exist before the next physics query.
        if (!_hasDetached || _isResolved)
            return;

        IgnoreSpearCollisions();
        RestoreSeparatedInitialOverlaps();
    }

    private void HandleCompleted(SpearImpactCounter counter)
    {
        if (_hasDetached || _rb == null)
            return;

        _hasDetached = true;

        // Cover the spear that delivered the final hit before the body starts
        // falling in this physics step.
        IgnoreSpearCollisions();

        // The stalactite is often embedded in the ceiling while hanging.
        // Static bodies do not generate contacts, so any solid overlap that
        // already exists would become an impact as soon as the body turns
        // Dynamic. Ignore only those existing overlaps until they separate.
        CaptureInitialOverlaps();

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = _fallGravityScale;
        _rb.WakeUp();

        // A body type change can recreate the physics representation of the
        // body, so make sure the pairs are in place for the first fall step.
        IgnoreSpearCollisions();
    }

    private void IgnoreSpearCollisions()
    {
        RefreshColliders();

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

        // Colliders belong to the spear when they are attached to its body,
        // even if their own hierarchy does not expose a Spear component.
        // The rope, for example, is created at runtime under the spear.
        Rigidbody2D spearBody =
            spear.GetComponentInParent<Rigidbody2D>();

        Collider2D[] spearColliders = spearBody != null
            ? spearBody.GetComponentsInChildren<Collider2D>(true)
            : spear.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D spearCollider in spearColliders)
        {
            if (spearCollider == null)
                continue;

            if (spearBody != null)
            {
                if (spearCollider.attachedRigidbody != spearBody)
                    continue;
            }
            else if (spearCollider.GetComponentInParent<Spear>(true) != spear)
            {
                continue;
            }

            foreach (Collider2D collider in _colliders)
            {
                if (collider == null || collider == spearCollider)
                    continue;

                // Avoid resetting an already ignored pair every physics step.
                if (Physics2D.GetIgnoreCollision(collider, spearCollider))
                    continue;

                Physics2D.IgnoreCollision(collider, spearCollider, true);
            }
        }
    }

    /// <summary>
    /// Ignores solid collider pairs that already overlap while the body is
    /// still Static. Those overlaps do not generate contacts until the body
    /// becomes Dynamic, at which point they would be reported as impacts.
    /// Only pairs changed by this method are tracked for restoration.
    /// </summary>
    private void CaptureInitialOverlaps()
    {
        if (_colliders == null)
            return;

        foreach (Collider2D collider in _colliders)
        {
            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            CollectOverlaps(collider);

            for (int i = 0; i < _overlapBuffer.Count; i++)
            {
                Collider2D other = _overlapBuffer[i];

                if (other == null || other == collider)
                    continue;

                // Own body colliders are never impact targets.
                if (other.attachedRigidbody == _rb)
                    continue;

                // Spears are managed by IgnoreSpearCollisions.
                if (IsSpearCollider(other))
                    continue;

                // Player and enemy colliders must still produce their
                // normal impact behaviour.
                if (other.GetComponentInParent<PlayerHealth>() != null)
                    continue;

                if (other.GetComponentInParent<IDamageable>() != null)
                    continue;

                if (IsTrackedInitialOverlap(collider, other))
                    continue;

                // Pairs that were already ignored were not changed by this
                // script, so they must not be restored later.
                if (Physics2D.GetIgnoreCollision(collider, other))
                    continue;

                Physics2D.IgnoreCollision(collider, other, true);

                _initialOverlapPairs.Add(
                    new InitialOverlapPair
                    {
                        stalactiteCollider = collider,
                        otherCollider = other
                    });
            }
        }
    }

    /// <summary>
    /// Restores each ignored initial overlap as soon as its colliders stop
    /// overlapping. The ceiling and the floor can share the same
    /// CompositeCollider2D, so waiting a fixed number of frames would let the
    /// stalactite fall through the floor instead of resolving its impact.
    /// </summary>
    private void RestoreSeparatedInitialOverlaps()
    {
        for (int i = _initialOverlapPairs.Count - 1; i >= 0; i--)
        {
            InitialOverlapPair pair = _initialOverlapPairs[i];

            if (pair.stalactiteCollider == null ||
                pair.otherCollider == null)
            {
                _initialOverlapPairs.RemoveAt(i);
                continue;
            }

            // Keep tracking while either collider is inactive so the pair
            // can still be restored if the collider is enabled again.
            if (!pair.stalactiteCollider.enabled ||
                !pair.otherCollider.enabled ||
                !pair.stalactiteCollider.gameObject.activeInHierarchy ||
                !pair.otherCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (IsOverlapping(
                    pair.stalactiteCollider,
                    pair.otherCollider))
            {
                continue;
            }

            Physics2D.IgnoreCollision(
                pair.stalactiteCollider,
                pair.otherCollider,
                false);

            _initialOverlapPairs.RemoveAt(i);
        }
    }

    private bool IsTrackedInitialOverlap(
        Collider2D stalactiteCollider,
        Collider2D other)
    {
        for (int i = 0; i < _initialOverlapPairs.Count; i++)
        {
            InitialOverlapPair pair = _initialOverlapPairs[i];

            if (pair.stalactiteCollider == stalactiteCollider &&
                pair.otherCollider == other)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOverlapping(
        Collider2D first,
        Collider2D second)
    {
        // Geometry query rather than an overlap query: the pair is currently
        // collision-ignored, and this check must keep reporting the real
        // overlap so the ignore is only lifted once the colliders separate.
        return Physics2D.Distance(first, second).isOverlapped;
    }

    private void CollectOverlaps(Collider2D collider)
    {
        _overlapBuffer.Clear();

        Physics2D.OverlapCollider(
            collider,
            _initialOverlapFilter,
            _overlapBuffer);
    }

    private static bool IsSpearCollider(Collider2D collider)
    {
        return collider != null &&
               collider.GetComponentInParent<Spear>(true) != null;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryResolveImpact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryResolveImpact(collision);
    }

    private void TryResolveImpact(Collision2D collision)
    {
        if (!_hasDetached || _isResolved || _rb == null)
            return;

        if (collision == null || collision.collider == null)
            return;

        Spear spear = GetSpearFromCollision(collision);

        if (spear != null)
        {
            // Unity can still report a contact in the step in which the body
            // becomes dynamic. A spear is never a valid impact target for
            // this hazard while the stalactite is falling.
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

    private static Spear GetSpearFromCollision(Collision2D collision)
    {
        if (collision == null)
            return null;

        Collider2D collider = collision.collider;

        if (collider != null)
        {
            Spear spear = collider.GetComponentInParent<Spear>(true);

            if (spear != null)
                return spear;
        }

        // The collider can belong to a body that has no Spear component in
        // its own hierarchy. Checking the body avoids treating the object the
        // spear is embedded in as a spear.
        Rigidbody2D otherBody = collision.rigidbody;

        if (otherBody == null && collider != null)
            otherBody = collider.attachedRigidbody;

        if (otherBody != null)
        {
            Spear spear = otherBody.GetComponent<Spear>();

            if (spear == null)
                spear = otherBody.GetComponentInParent<Spear>(true);

            if (spear != null)
                return spear;
        }

        return null;
    }

    private void RefreshColliders()
    {
        _colliders = GetComponentsInChildren<Collider2D>(true);
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
