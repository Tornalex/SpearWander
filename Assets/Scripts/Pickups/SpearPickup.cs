using UnityEngine;

public class SpearPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private int spearsCapacityToBuild = 1;
    [SerializeField] private string promptMessage = "Premi E per raccogliere la Lancia";

    public void Interact(Player player)
    {
        if (player.Combat != null)
        {
            player.Combat.IncreaseMaxSpears(spearsCapacityToBuild);

            PlayerInteract playerInteract = player.GetComponent<PlayerInteract>();
            if (playerInteract != null)
            {
                playerInteract.RemoveInteractableFromList(this);
            }

            Destroy(gameObject);
        }
        else
        {
            Debug.LogError("Il componente PlayerCombat non è stato trovato sul Player!");
        }
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }
}