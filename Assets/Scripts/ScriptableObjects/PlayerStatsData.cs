using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Stats")]
public class PlayerStatsData : ScriptableObject
{
    [Header("Movement")]
    public float walkSpeed = 10f;
    public float runSpeed = 16f;

    [Header("Jump")]
    public float jumpForce = 12f;
    public float jumpCutMultiplier = 0.5f;
    public float jumpBufferTime = 0.1f;
    public float coyoteTime = 0.1f;

    [Header("Health")]
    public int maxHealth = 3;
    public float iFramesDuration = 1f;
    public Vector2 damageForce = new Vector2(15f, 10f);
    public float damageKnockbackDuration = 0.42f;
}
