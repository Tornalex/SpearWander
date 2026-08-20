using UnityEngine;

[CreateAssetMenu(menuName = "Spear Wander/Player/Essence")]
public class PlayerEssenceData : ScriptableObject
{
    public float maxEssence = 100f;
    public float essencePerBaseCatch = 20f;
    public float essenceCostPerHeal = 30f;
    public int healthPerHeal = 1;
}
