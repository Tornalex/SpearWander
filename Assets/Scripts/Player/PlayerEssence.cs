using UnityEngine;

public class PlayerEssence : MonoBehaviour
{
    private float _currentEssence;
    private Player _player;

    public float CurrentEssence => _currentEssence;
    public float MaxEssence => _player.EssenceStats.maxEssence;

    void Awake()
    {
        _currentEssence = 0f;
        _player = GetComponent<Player>();
    }

    void Update()
    {
        if (_player.Input.HealTriggered)
        {
            TryHeal();
        }
    }

    public void AddEssenceFromCatch(bool perfectCatch)
    {
        float amountToAdd = perfectCatch ? 40f : _player.EssenceStats.essencePerBaseCatch;
        _currentEssence = Mathf.Clamp(_currentEssence + amountToAdd, 0f, _player.EssenceStats.maxEssence);
    }

    public bool ConsumeEssence(float amount)
    {
        if (_currentEssence >= amount)
        {
            _currentEssence -= amount;
            return true;
        }
        return false;
    }

    private void TryHeal()
    {
        if (_player.Health.CurrentHealth < _player.Health.MaxHealth && _currentEssence >= _player.EssenceStats.essenceCostPerHeal)
        {
            _currentEssence -= _player.EssenceStats.essenceCostPerHeal;
            _player.Health.Heal(_player.EssenceStats.healthPerHeal);
        }
    }

    public void Refill()
    {
        _currentEssence = _player.EssenceStats.maxEssence;
    }

    public void ResetState()
    {
        _currentEssence = 0f;
    }
}
