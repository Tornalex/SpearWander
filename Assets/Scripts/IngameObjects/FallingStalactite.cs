using UnityEngine;

/// <summary>
/// Releases a hanging stalactite when its SpearImpactCounter completes.
/// The counter owns hit counting; this component restores the Rigidbody2D's
/// original gravity so the stalactite falls and remains a physical obstacle.
/// </summary>
[RequireComponent(typeof(SpearImpactCounter), typeof(Rigidbody2D))]
public class FallingStalactite : MonoBehaviour
{
    private const float MinLandingNormalY = 0.5f;

    [SerializeField] private SpearImpactCounter impactCounter;

    [SerializeField, Min(0f)]
    [Tooltip("Gravity scale used while hanging. The Rigidbody2D's original gravity scale is restored on detach.")]
    private float hangingGravityScale = 0f;

    private Rigidbody2D _rb;
    private float _fallGravityScale;
    private bool _hasDetached;

    private void Awake()
    {
        if (impactCounter == null)
            impactCounter = GetComponent<SpearImpactCounter>();

        _rb = GetComponent<Rigidbody2D>();

        if (impactCounter == null ||
            impactCounter.gameObject != gameObject)
        {
            Debug.LogError(
                "FallingStalactite requires a SpearImpactCounter on the same GameObject.",
                this);
            impactCounter = null;
            enabled = false;
            return;
        }

        if (_rb == null)
        {
            Debug.LogError(
                "FallingStalactite requires a Rigidbody2D on the same GameObject.",
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

        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = _fallGravityScale;
        _rb.WakeUp();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryLand(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryLand(collision);
    }

    private void TryLand(Collision2D collision)
    {
        if (!_hasDetached || _rb == null)
            return;

        if (_rb.bodyType != RigidbodyType2D.Dynamic)
            return;

        if (collision == null)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            // Ground contacts face upward; vertical walls have a normal near zero.
            if (contact.normal.y > MinLandingNormalY)
            {
                _rb.bodyType = RigidbodyType2D.Static;
                return;
            }
        }
    }
}
