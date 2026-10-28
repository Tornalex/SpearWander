
using UnityEngine;

public class SpearImpactReceiverTest : MonoBehaviour, ISpearImpactReceiver
{
    [SerializeField] private bool acceptImpact = true;

    private int acceptedHits;

    public bool TryReceiveSpearImpact(
        Spear spear,
        Vector2 hitPoint,
        Vector2 surfaceNormal)
    {
        if (!acceptImpact)
        {
            Debug.Log($"[{name}] Impatto rifiutato.", this);
            return false;
        }

        acceptedHits++;

        Debug.Log(
            $"[{name}] Impatto accettato #{acceptedHits}. " +
            $"Punto: {hitPoint}, Normale: {surfaceNormal}",
            this
        );

        return true;
    }
}
