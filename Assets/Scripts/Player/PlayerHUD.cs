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

    [Header("Spears UI (Lance)")]
    [SerializeField] private TextMeshProUGUI spearsText;

    // Cache variables to optimize UI updates (prevent layout recalculations every frame)
    private int _lastHealth = -1;
    private int _lastMaxHealth = -1;
    private float _lastEssence = -1f;
    private float _lastMaxEssence = -1f;
    private int _lastSpears = -1;
    private int _lastMaxSpears = -1;

    void Start()
    {
        // Se non assegnato, cerca automaticamente il Player nella scena
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
        UpdateSpearsUI();
    }

    private void UpdateHealthUI()
    {
        if (player.Health == null) return;

        int currentHealth = player.Health.CurrentHealth;
        int maxHealth = player.Health.MaxHealth;

        // Aggiorna solo se i valori sono cambiati
        if (currentHealth != _lastHealth || maxHealth != _lastMaxHealth)
        {
            _lastHealth = currentHealth;
            _lastMaxHealth = maxHealth;

            // Aggiorna lo Slider
            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }

            // Aggiorna il testo
            if (healthText != null)
            {
                healthText.text = $"Vita: {currentHealth} / {maxHealth}";
            }
        }
    }

    private void UpdateEssenceUI()
    {
        if (player.Health == null) return;

        float currentEssence = player.Health.CurrentEssence;
        float maxEssence = player.Health.MaxEssence;

        // Aggiorna solo se i valori sono cambiati
        if (!Mathf.Approximately(currentEssence, _lastEssence) || !Mathf.Approximately(maxEssence, _lastMaxEssence))
        {
            _lastEssence = currentEssence;
            _lastMaxEssence = maxEssence;

            // Aggiorna lo Slider
            if (essenceSlider != null)
            {
                essenceSlider.maxValue = maxEssence;
                essenceSlider.value = currentEssence;
            }

            // Aggiorna il testo
            if (essenceText != null)
            {
                // Mostra la percentuale arrotondata
                int essencePercentage = Mathf.RoundToInt((currentEssence / maxEssence) * 100f);
                essenceText.text = $"Essenza: {essencePercentage}%";
            }
        }
    }

    private void UpdateSpearsUI()
    {
        if (player.Combat == null) return;

        int currentSpears = player.Combat.currentSpears;
        int maxSpears = player.Combat.MaxSpears;

        // Aggiorna solo se i valori sono cambiati
        if (currentSpears != _lastSpears || maxSpears != _lastMaxSpears)
        {
            _lastSpears = currentSpears;
            _lastMaxSpears = maxSpears;

            // Aggiorna il testo
            if (spearsText != null)
            {
                spearsText.text = $"Lance: {currentSpears} / {maxSpears}";
            }
        }
    }
}
