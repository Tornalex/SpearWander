
using UnityEngine;

public class SpearImpactCounterTestListener : MonoBehaviour
{
    [SerializeField] private SpearImpactCounter counter;

    private void OnEnable()
    {
        if (counter == null)
            return;

        counter.OnHit += HandleHit;
        counter.OnCompleted += HandleCompleted;
    }

    private void OnDisable()
    {
        if (counter == null)
            return;

        counter.OnHit -= HandleHit;
        counter.OnCompleted -= HandleCompleted;
    }

    private void HandleHit(SpearImpactCounter source)
    {
        Debug.Log(
            $"Impatto intermedio: {source.CurrentHits}/{source.RequiredHits}",
            source
        );
    }

    private void HandleCompleted(SpearImpactCounter source)
    {
        Debug.Log(
            $"Contatore completato: {source.CurrentHits}/{source.RequiredHits}",
            source
        );
    }
}
