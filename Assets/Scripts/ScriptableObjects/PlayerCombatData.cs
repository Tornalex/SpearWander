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

    [Header("Pogo")]
    public int pogoDamage = 1;
}