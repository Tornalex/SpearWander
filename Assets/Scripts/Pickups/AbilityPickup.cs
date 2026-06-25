using UnityEngine;

public class AbilityPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string componentName = "PlayerDash";
    [SerializeField] private bool enableOnPickup = true;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private string promptMessage = "Premi E per raccogliere";

    public void Interact(Player player)
    {
        var component = player.GetComponent(componentName) as Behaviour;
        if (component != null)
        {
            component.enabled = enableOnPickup;

            var interact = player.GetComponent<PlayerInteract>();
            if (interact != null)
                interact.RemoveInteractableFromList(this);

            if (destroyOnPickup)
                Destroy(gameObject);
        }
        else
        {
            Debug.LogError($"AbilityPickup: Component '{componentName}' not found on Player!");
        }
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }
}
