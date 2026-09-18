using UnityEngine;
using SpearWander.Abilities;

namespace SpearWander.Abilities
{
    public class AbilityPickup : MonoBehaviour, IInteractable
    {
        [Header("Ability Configuration")]
        [SerializeField] private AbilityData abilityData;

        [Header("Optional Override")]
        [SerializeField] private string overridePrompt;

        public void Interact(Player player)
        {
            if (abilityData == null)
            {
                Debug.LogError("[AbilityPickup] No AbilityData assigned!");
                return;
            }

            if (AbilityManager.Instance != null && AbilityManager.Instance.IsUnlocked(abilityData.abilityType))
            {
                Debug.Log($"[AbilityPickup] Ability {abilityData.abilityType} already unlocked");
                return;
            }

            // Unlock the ability
            if (AbilityManager.Instance != null)
            {
                AbilityManager.Instance.UnlockAbility(abilityData.abilityType, player);
            }
            else
            {
                abilityData.Unlock(player);
            }

            // Remove from interactables
            var playerInteract = player.GetComponent<PlayerInteract>();
            if (playerInteract != null)
            {
                playerInteract.RemoveInteractableFromList(this);
            }

            // Destroy pickup if configured
            if (abilityData.destroyOnPickup)
            {
                Destroy(gameObject);
            }
        }

        public string GetInteractPrompt()
        {
            if (!string.IsNullOrEmpty(overridePrompt)) return overridePrompt;
            return abilityData != null ? abilityData.promptMessage : "Premi E per interagire";
        }

        void OnValidate()
        {
            if (abilityData != null && string.IsNullOrEmpty(overridePrompt))
            {
                // Could auto-set prompt from ability data
            }
        }
    }
}