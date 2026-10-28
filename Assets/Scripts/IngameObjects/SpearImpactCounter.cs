using System;
using UnityEngine;

/// <summary>
/// Counts accepted spear impacts on this component and reports progress.
/// This component only tracks hits; object behaviour is implemented by
/// separate listeners subscribed to <see cref="OnHit"/> and
/// <see cref="OnCompleted"/>.
/// </summary>
public class SpearImpactCounter : MonoBehaviour, ISpearImpactReceiver
{
    [SerializeField, Min(1)] private int requiredHits = 3;

    public int RequiredHits => Mathf.Max(1, requiredHits);
    public int CurrentHits { get; private set; }
    public bool IsComplete { get; private set; }

    /// <summary>
    /// Raised after an accepted impact that does not complete the counter.
    /// Not raised on the final impact; use <see cref="OnCompleted"/> for that.
    /// </summary>
    public event Action<SpearImpactCounter> OnHit;

    /// <summary>
    /// Raised exactly once, when the required hit count is reached.
    /// </summary>
    public event Action<SpearImpactCounter> OnCompleted;

    private void OnValidate()
    {
        requiredHits = Mathf.Max(1, requiredHits);
    }

    public bool TryReceiveSpearImpact(
        Spear spear,
        Vector2 hitPoint,
        Vector2 surfaceNormal)
    {
        if (IsComplete)
            return false;

        CurrentHits++;

        if (CurrentHits >= RequiredHits)
        {
            IsComplete = true;
            OnCompleted?.Invoke(this);
        }
        else
        {
            OnHit?.Invoke(this);
        }

        return true;
    }
}
