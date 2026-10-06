using UnityEngine;
using SpearWander.Abilities;

public class AbilityPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private string componentName = "PlayerDash";
    [SerializeField] private bool enableOnPickup = true;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private string promptMessage = "E";

    void Start()
    {
        var abilityManager = AbilityManager.Instance;
        var spearAbility = abilityManager != null
            ? abilityManager.GetAbilityData(AbilityType.Spear)
            : null;
        if (spearAbility != null &&
            spearAbility.componentName == componentName &&
            spearAbility.enableComponent == enableOnPickup &&
            abilityManager.IsUnlocked(AbilityType.Spear))
        {
            Destroy(gameObject);
        }
    }

    public void Interact(Player player)
    {
        var component = player.GetComponent(componentName) as Behaviour;
        if (component != null)
        {
            component.enabled = enableOnPickup;

            var abilityManager = AbilityManager.Instance;
            var spearAbility = abilityManager != null
                ? abilityManager.GetAbilityData(AbilityType.Spear)
                : null;
            if (spearAbility != null &&
                spearAbility.componentName == componentName &&
                spearAbility.enableComponent == enableOnPickup)
            {
                abilityManager.UnlockAbility(AbilityType.Spear, player);
            }

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
