using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Player Reference")]
    [SerializeField] private Player player;

    [Header("Health UI (Vita)")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Essence UI (Essenza)")]
    [SerializeField] private Slider essenceSlider;
    [SerializeField] private TextMeshProUGUI essenceText;

    [Header("Spear UI (Lancia)")]
    [SerializeField] private TextMeshProUGUI spearStateText;

    private int _lastHealth = -1;
    private int _lastMaxHealth = -1;
    private float _lastEssence = -1f;
    private float _lastMaxEssence = -1f;
    private PlayerCombatV2.SpearUIState _lastSpearState = PlayerCombatV2.SpearUIState.Ready;

    void Start()
    {
        if (player == null)
        {
#if UNITY_2023_1_OR_NEWER
            player = FindAnyObjectByType<Player>();
#else
            player = FindObjectOfType<Player>();
#endif
        }

        if (player == null)
        {
            Debug.LogError("PlayerHUD: Nessun componente 'Player' trovato nella scena!");
        }
    }

    void Update()
    {
        if (player == null) return;

        UpdateHealthUI();
        UpdateEssenceUI();
        UpdateSpearUI();
    }

    private void UpdateHealthUI()
    {
        if (player.Health == null) return;

        int currentHealth = player.Health.CurrentHealth;
        int maxHealth = player.Health.MaxHealth;

        if (currentHealth != _lastHealth || maxHealth != _lastMaxHealth)
        {
            _lastHealth = currentHealth;
            _lastMaxHealth = maxHealth;

            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }

            if (healthText != null)
            {
                healthText.text = $"Life: {currentHealth} / {maxHealth}";
            }
        }
    }

    private void UpdateEssenceUI()
    {
        if (player.Health == null) return;

        float currentEssence = player.Health.CurrentEssence;
        float maxEssence = player.Health.MaxEssence;

        if (!Mathf.Approximately(currentEssence, _lastEssence) || !Mathf.Approximately(maxEssence, _lastMaxEssence))
        {
            _lastEssence = currentEssence;
            _lastMaxEssence = maxEssence;

            if (essenceSlider != null)
            {
                essenceSlider.maxValue = maxEssence;
                essenceSlider.value = currentEssence;
            }

            if (essenceText != null)
            {
                int essencePercentage = Mathf.RoundToInt((currentEssence / maxEssence) * 100f);
                essenceText.text = $"Essence: {essencePercentage}%";
            }
        }
    }

    private void UpdateSpearUI()
    {
        if (player.Combat == null) return;

        var currentState = player.Combat.CurrentSpearUIState;

        if (currentState != _lastSpearState)
        {
            _lastSpearState = currentState;

            if (spearStateText != null)
            {
                spearStateText.text = currentState switch
                {
                    PlayerCombatV2.SpearUIState.Ready => "Spear: Ready",
                    PlayerCombatV2.SpearUIState.Thrown => "Spear: Thrown",
                    PlayerCombatV2.SpearUIState.Returning => "Spear: Returning",
                    _ => "Spear: Ready"
                };
            }
        }
    }
}
