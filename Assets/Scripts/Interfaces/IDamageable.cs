using UnityEngine;
public interface IDamageable
{
    /// <summary>
    /// Dispatches a damage amount and hit context. The implementation owns validation,
    /// invulnerability rules, health changes, feedback, and death behavior. Returns true
    /// only when positive damage was accepted and applied.
    /// </summary>
    bool TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition);
}

public interface IPlayerDamageReceiver : IDamageable
{
    /// <summary>
    /// Applies damage that is known to originate from the player. Implementations
    /// can use this as an explicit player-damage notification without changing
    /// the normal IDamageable contract used by other sources.
    /// </summary>
    bool TakeDamageFromPlayer(int damage, Vector2 hitPoint, Vector2 damageSourcePosition);
}

public static class DamageableExtensions
{
    /// <summary>
    /// Applies player damage when the target supports it, otherwise falls back
    /// to the normal damage path unchanged.
    /// </summary>
    public static bool TakeDamageFromPlayer(
        this IDamageable damageable,
        int damage,
        Vector2 hitPoint,
        Vector2 damageSourcePosition)
    {
        if (damageable is IPlayerDamageReceiver playerDamageReceiver)
            return playerDamageReceiver.TakeDamageFromPlayer(damage, hitPoint, damageSourcePosition);

        return damageable.TakeDamage(damage, hitPoint, damageSourcePosition);
    }
}
