using UnityEngine;
using System.Collections.Generic;
using SpearWander.Abilities;

namespace SpearWander.Abilities
{
    public class AbilityManager : MonoBehaviour
    {
        public static AbilityManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private List<AbilityData> allAbilities = new List<AbilityData>();

        private HashSet<AbilityType> _unlockedAbilities = new HashSet<AbilityType>();

        public IReadOnlyCollection<AbilityType> UnlockedAbilities => _unlockedAbilities;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            // Initialize from Player state on start
            InitializeFromPlayer();
        }

        private void InitializeFromPlayer()
        {
            var player = FindAnyObjectByType<Player>();
            if (player == null) return;

            foreach (var ability in allAbilities)
            {
                if (ability.IsUnlocked(player))
                {
                    _unlockedAbilities.Add(ability.abilityType);
                }
            }
        }

        public void UnlockAbility(AbilityType type, Player player)
        {
            if (_unlockedAbilities.Contains(type)) return;

            var abilityData = allAbilities.Find(a => a.abilityType == type);
            if (abilityData != null)
            {
                abilityData.Unlock(player);
                _unlockedAbilities.Add(type);
                Debug.Log($"[AbilityManager] Unlocked ability: {type}");
            }
            else
            {
                Debug.LogWarning($"[AbilityManager] No AbilityData found for type: {type}");
            }
        }

        public bool IsUnlocked(AbilityType type) => _unlockedAbilities.Contains(type);

        public AbilityData GetAbilityData(AbilityType type) => allAbilities.Find(a => a.abilityType == type);

        public void ResetAll(Player player = null)
        {
            if (player != null)
            {
                foreach (var ability in allAbilities)
                {
                    if (!string.IsNullOrEmpty(ability.componentName))
                    {
                        var component = player.GetComponent(ability.componentName) as Behaviour;
                        if (component != null)
                        {
                            component.enabled = !ability.enableComponent;
                        }
                    }
                }
            }
            _unlockedAbilities.Clear();
        }

        public void RestoreAbilities(Player player)
        {
            if (player == null) return;
            foreach (var type in _unlockedAbilities)
            {
                var abilityData = allAbilities.Find(a => a.abilityType == type);
                if (abilityData != null)
                {
                    abilityData.Unlock(player);
                }
            }
        }
    }
}