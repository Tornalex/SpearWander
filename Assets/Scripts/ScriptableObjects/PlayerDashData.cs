using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Dash")]
public class PlayerDashData : ScriptableObject
{
    public float dashSpeed = 25f;
    public float dashDuration = 0.17f;
    public float dashCooldown = 0.83f;
    public float postDashInvincibility = 0.05f;
}
