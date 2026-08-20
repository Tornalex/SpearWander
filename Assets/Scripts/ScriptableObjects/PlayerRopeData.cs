using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Rope")]
public class PlayerRopeData : ScriptableObject
{
    public float climbSpeed = 5f;
    public float dismountJumpForce = 10f;
    public float dismountCooldown = 0.5f;
    public float ropeLength = 5f;
}
