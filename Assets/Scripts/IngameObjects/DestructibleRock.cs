using UnityEngine;

/// <summary>
/// Handles the destruction of a rock after its SpearImpactCounter completes.
/// The counter owns hit counting; this component owns the break sequence.
/// </summary>
[RequireComponent(typeof(SpearImpactCounter))]
public class DestructibleRock : MonoBehaviour
{
    [SerializeField] private SpearImpactCounter impactCounter;
    [SerializeField] private GameObject destructionEffectPrefab;
    [SerializeField, Min(0f)] private float removalDelay = 0.3f;

    private bool _isDestroying;

    private void Awake()
    {
        if (impactCounter == null)
            impactCounter = GetComponent<SpearImpactCounter>();

        if (impactCounter == null ||
            impactCounter.gameObject != gameObject)
        {
            Debug.LogError(
                "DestructibleRock requires a SpearImpactCounter on the same GameObject.",
                this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (impactCounter != null)
            impactCounter.OnCompleted += HandleCompleted;
    }

    private void OnDisable()
    {
        if (impactCounter != null)
            impactCounter.OnCompleted -= HandleCompleted;
    }

    private void HandleCompleted(SpearImpactCounter counter)
    {
        if (_isDestroying)
            return;

        _isDestroying = true;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D collider in colliders)
            collider.enabled = false;

        if (destructionEffectPrefab != null)
        {
            // Spawn at the scene root so the effect survives the rock's removal.
            Instantiate(
                destructionEffectPrefab,
                transform.position,
                transform.rotation
            );
        }

        Destroy(gameObject, Mathf.Max(0f, removalDelay));
    }
}
