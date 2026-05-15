using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerKnockback : MonoBehaviour
{
    public bool IsKnockedBack { get; private set; }
    private int _knockbackFrameCounter;
    private Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (_knockbackFrameCounter > 0)
        {
            _knockbackFrameCounter--;
            if (_knockbackFrameCounter <= 0) IsKnockedBack = false;
        }
    }

    public void ApplyKnockback(Vector2 sourcePosition, Vector2 force, int durationFrames)
    {
        IsKnockedBack = true;
        _knockbackFrameCounter = durationFrames;
        float dir = (transform.position.x < sourcePosition.x) ? -1f : 1f;
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(new Vector2(dir * force.x, force.y), ForceMode2D.Impulse);
    }
}