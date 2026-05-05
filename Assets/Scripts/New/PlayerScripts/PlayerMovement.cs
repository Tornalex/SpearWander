using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 10f;
    [SerializeField] private float runSpeed = 16f;

    private Rigidbody2D _rb;
    private PlayerInputHandler _input;
    private PlayerDash _dash;
    private PlayerKnockback _knockback;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _dash = GetComponent<PlayerDash>();
        _knockback = GetComponent<PlayerKnockback>();
    }

    void FixedUpdate()
    {
        if (_knockback.IsKnockedBack || _dash.IsDashing) return;

        float moveInput = _input.MoveInput.x;
        float targetSpeed = (_input.IsDashHeld() && Mathf.Abs(moveInput) > 0.1f) ? runSpeed : walkSpeed;

        _rb.linearVelocity = new Vector2(moveInput * targetSpeed, _rb.linearVelocity.y);

        if (Mathf.Abs(moveInput) > 0.1f)
        {
            Flip(Mathf.Sign(moveInput));
        }
    }

    private void Flip(float direction)
    {
        if ((direction > 0 && transform.localScale.x < 0) || (direction < 0 && transform.localScale.x > 0))
        {
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
    }
}