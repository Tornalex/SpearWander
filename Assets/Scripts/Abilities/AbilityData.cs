using UnityEngine;
using SpearWander.Abilities;

namespace SpearWander.Abilities
{
    [CreateAssetMenu(fileName = "New Ability", menuName = "Spear Wander/Ability Data")]
    public class AbilityData : ScriptableObject
    {
        [Header("Identity")]
        public AbilityType abilityType;
        public string displayName;
        [TextArea] public string description;

        [Header("Interaction")]
        public string promptMessage = "Premi E per interagire";
        public bool destroyOnPickup = true;

        [Header("Unlock Behavior")]
        public string componentName;
        public bool enableComponent = true;

        [Header("Optional: Additional Setup")]
        public bool requiresPlayerCombat = false;
        public bool requiresRopeClimb = false;

        public void Unlock(Player player)
        {
            if (player == null) return;

            // Enable the target component
            if (!string.IsNullOrEmpty(componentName))
            {
                var component = player.GetComponent(componentName) as Behaviour;
                if (component != null)
                {
                    component.enabled = enableComponent;
                }
                else
                {
                    Debug.LogWarning($"[AbilityData] Component '{componentName}' not found on Player for ability {abilityType}");
                }
            }

            // Special handling for specific abilities
            switch (abilityType)
            {
                case AbilityType.Spear:
                    // PlayerCombat is enabled by default in the new system
                    break;
                case AbilityType.Rope:
                    if (player.RopeClimb != null)
                        player.RopeClimb.enabled = true;
                    break;
            }
        }

        public bool IsUnlocked(Player player)
        {
            if (player == null || string.IsNullOrEmpty(componentName)) return false;
            var component = player.GetComponent(componentName) as Behaviour;
            return component != null && component.enabled == enableComponent;
        }
    }
}