using UnityEngine;

public class Bouncer : MonoBehaviour, IBounceable
{
    [SerializeField] private float bounceMultiplier = 1.5f;
    public float GetBounceMultiplier()
    {
        return bounceMultiplier;
    }
    public void OnPogoBounce()
    {
        //to be implemented
    }
}
