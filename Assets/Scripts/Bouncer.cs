using UnityEngine;

public class Bouncer : MonoBehaviour, IBounceable
{
    [SerializeField] private float bounceMultiplier = 1.5f; // Example bounce multiplier
    public float GetBounceMultiplier()
    {
        return bounceMultiplier;
    }
    public void OnPogoBounce()
    {
        // Implement any additional behavior when a pogo bounce occurs, if needed
    }
}
