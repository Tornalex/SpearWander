using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Boss/Data")]
public class BossData : ScriptableObject
{
    [Header("Health")]
    public int maxHealth = 10;

    [Header("Movement")]
    public float rotationSpeed = 360f; // degrees per second
    public float moveSpeed = 2f;
    public float chaseDistance = 10f; // distance at which boss starts following

    [Header("Attacks - Melee")]
    public float meleeAttackRange = 2f;
    public float meleeAttackCooldown = 2f;
    public int meleeDamage = 2;
    public float meleeKnockbackForce = 15f;

    [Header("Attacks - Ranged")]
    public float rangedAttackRange = 8f;
    public float rangedAttackCooldown = 3f;
    public int rangedDamage = 1;
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;

    [Header("Attacks - Anti-Air")]
    public float antiAirRange = 4f;
    public float antiAirCooldown = 2.5f;
    public int antiAirDamage = 2;
    public float antiAirVerticalForce = 20f;

    [Header("Weak Point")]
    public float weakPointDamageMultiplier = 2f; // damage multiplier when hitting weak point

    [Header("Knockback")]
    public Vector2 knockbackForce = new Vector2(15f, 10f);
    public float knockbackDuration = 0.3f;

    [Header("VFX/SFX")]
    public VFXType hitVfxType = VFXType.HitGeneric;
    public SFXType hitSfxType = SFXType.EnemyPierced;
    public VFXType deathVfxType = VFXType.EnemyDeath;
    public SFXType deathSfxType = SFXType.EnemyPierced;
}