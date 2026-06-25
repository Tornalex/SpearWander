using UnityEngine;

public class RopePickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string promptMessage = "Premi E per ottenere la corda";

    public void Interact(Player player)
    {
        var ropeClimb = player.GetComponent<PlayerRopeClimb>();
        if (ropeClimb != null)
        {
            ropeClimb.enabled = true;

            var interact = player.GetComponent<PlayerInteract>();
            if (interact != null)
                interact.RemoveInteractableFromList(this);

            Destroy(gameObject);
        }
        else
        {
            Debug.LogError("RopePickup: Component PlayerRopeClimb not found on Player!");
        }
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }
}
