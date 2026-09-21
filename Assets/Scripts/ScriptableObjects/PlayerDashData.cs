using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Dash")]
public class PlayerDashData : ScriptableObject
{
    [Header("Dash")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.17f;
    public float dashCooldown = 0.83f;
    public float postDashInvincibility = 0.05f;

    [Header("Knockback")]
    public Vector2 knockbackForce = new Vector2(10f, 5f);
    public float knockbackDuration = 0.2f;
}