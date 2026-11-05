using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Releases a hanging platform when its SpearImpactCounter completes.
/// The counter owns hit counting; this component restores the Rigidbody2D's
/// original gravity so the platform falls and remains a physical obstacle.
/// Spear collisions are ignored while falling and restored after landing.
/// </summary>
[RequireComponent(typeof(SpearImpactCounter), typeof(Rigidbody2D))]
public class FallingPlatform : MonoBehaviour, IEmbeddable
{
    private const float MinLandingNormalY = 0.5f;

    private struct SpearCollisionPairState
    {
        public Collider2D platformCollider;
        public Collider2D spearCollider;
        public bool wasIgnored;
    }

    [SerializeField] private SpearImpactCounter impactCounter;

    [SerializeField, Min(0f)]
    [Tooltip("Gravity scale used while hanging. The Rigidbody2D's original gravity scale is restored on detach.")]
    private float hangingGravityScale = 0f;

    private Rigidbody2D _rb;
    private Collider2D[] _colliders;
    private readonly List<SpearCollisionPairState> _ignoredSpearCollisionPairs =
        new List<SpearCollisionPairState>();
    private float _fallGravityScale;
    private bool _hasDetached;
    private bool _hasLanded;

    private void Awake()
    {
        if (impactCounter == null)
            impactCounter = GetComponent<SpearImpactCounter>();

        _rb = GetComponent<Rigidbody2D>();

        if (impactCounter == null ||
            impactCounter.gameObject != gameObject)
        {
            Debug.LogError(
                "FallingPlatform requires a SpearImpactCounter on the same GameObject.",
                this);
            impactCounter = null;
            enabled = false;
            return;
        }

        if (_rb == null)
        {
            Debug.LogError(
                "FallingPlatform requires a Rigidbody2D on the same GameObject.",
                this);
            enabled = false;
            return;
        }

        _colliders = GetComponentsInChildren<Collider2D>(true);
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (TryIgnoreSpearContact(collision))
            return;

        TryLand(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (TryIgnoreSpearContact(collision))
            return;

        TryLand(collision);
    }

    private bool TryIgnoreSpearContact(Collision2D collision)
    {
        if (!_hasDetached || _hasLanded || collision == null)
            return false;

        Spear spear = GetSpearFromCollider(collision.collider);

        if (spear == null)
            return false;

        IgnoreSpearColliders(spear);
        return true;
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
            if (spearCollider == null ||
                spearCollider.GetComponentInParent<Spear>() != spear)
            {
                continue;
            }

            foreach (Collider2D platformCollider in _colliders)
            {
                if (platformCollider == null ||
                    platformCollider == spearCollider)
                {
                    continue;
                }

                if (IsSpearPairTracked(platformCollider, spearCollider))
                    continue;

                _ignoredSpearCollisionPairs.Add(
                    new SpearCollisionPairState
                    {
                        platformCollider = platformCollider,
                        spearCollider = spearCollider,
                        wasIgnored = Physics2D.GetIgnoreCollision(
                            platformCollider,
                            spearCollider)
                    });

                Physics2D.IgnoreCollision(
                    platformCollider,
                    spearCollider,
                    true);
            }
        }
    }

    private bool IsSpearPairTracked(
        Collider2D platformCollider,
        Collider2D spearCollider)
    {
        foreach (SpearCollisionPairState pair in _ignoredSpearCollisionPairs)
        {
            if (pair.platformCollider == platformCollider &&
                pair.spearCollider == spearCollider)
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreSpearCollisions()
    {
        foreach (SpearCollisionPairState pair in _ignoredSpearCollisionPairs)
        {
            if (pair.platformCollider == null || pair.spearCollider == null)
                continue;

            Physics2D.IgnoreCollision(
                pair.platformCollider,
                pair.spearCollider,
                pair.wasIgnored);
        }

        _ignoredSpearCollisionPairs.Clear();
    }

    private static Spear GetSpearFromCollider(Collider2D collider)
    {
        return collider != null
            ? collider.GetComponentInParent<Spear>()
            : null;
    }

    private void TryLand(Collision2D collision)
    {
        if (!_hasDetached || _hasLanded || _rb == null)
            return;

        if (_rb.bodyType != RigidbodyType2D.Dynamic)
            return;

        if (collision == null)
            return;

        // A spear must never count as ground; ignores normally prevent this,
        // but the final activation contact may still be reported this step.
        if (GetSpearFromCollider(collision.collider) != null)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            // Ground contacts face upward; vertical walls have a normal near zero.
            if (contact.normal.y > MinLandingNormalY)
            {
                _hasLanded = true;
                _rb.bodyType = RigidbodyType2D.Static;
                RestoreSpearCollisions();
                return;
            }
        }
    }

    public void OnSpearEmbedded(Spear spear, Vector2 hitPoint, Vector2 surfaceNormal)
    {
    }

    public void OnSpearRemoved(Spear spear)
    {
    }
}
