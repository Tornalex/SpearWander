using UnityEngine;

public class SpearPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string promptMessage = "Premi E per raccogliere la Lancia";

    public void Interact(Player player)
    {
        if (player.Combat != null)
        {
            // V2: Single spear system - pickup just enables the spear ability
            // Could add upgrades here later (e.g., longer rope, faster return, etc.)

            PlayerInteract playerInteract = player.GetComponent<PlayerInteract>();
            if (playerInteract != null)
            {
                playerInteract.RemoveInteractableFromList(this);
            }

            Destroy(gameObject);
        }
        else
        {
            Debug.LogError("Il componente PlayerCombatV2 non è stato trovato sul Player!");
        }
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }
}