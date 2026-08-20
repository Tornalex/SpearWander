using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Combat")]
public class PlayerCombatData : ScriptableObject
{
    [Header("Spear")]
    public float shootForce = 20f;
    public float throwCooldown = 0.42f;
    public int spearImpactDamage = 1;
    public int spearRecallDamage = 1;

    [Header("Dash")]
    public int dashDamage = 1;
    public Vector2 dashKnockbackForce = new Vector2(10f, 5f);
    public float dashKnockbackDuration = 0.2f;

    [Header("Pogo")]
    public int pogoDamage = 1;
}
