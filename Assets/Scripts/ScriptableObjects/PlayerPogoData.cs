using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Pogo")]
public class PlayerPogoData : ScriptableObject
{
    public float plungeSpeed = 20f;
    public float bounceForce = 15f;
    public float postPogoInvincibility = 0.17f;
    public float pogoStunDuration = 0.13f;
}
