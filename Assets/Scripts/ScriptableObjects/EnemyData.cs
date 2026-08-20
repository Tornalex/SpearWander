using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Enemy/Data")]
public class EnemyData : ScriptableObject
{
    public int maxHealth = 3;
    public float speed = 2f;
    public Vector2 knockbackForce = new Vector2(10f, 5f);
    public float knockbackDuration = 0.2f;
}
