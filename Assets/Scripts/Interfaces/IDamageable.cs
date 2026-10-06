using UnityEngine;
public interface IDamageable
{
    /// <summary>
    /// Dispatches a damage amount and hit context. The implementation owns validation,
    /// invulnerability rules, health changes, feedback, and death behavior; the supplied
    /// amount is not guaranteed to equal the health lost.
    /// </summary>
    void TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition); 
}
