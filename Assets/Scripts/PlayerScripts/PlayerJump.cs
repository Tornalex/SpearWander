using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;

    private Rigidbody2D _rb;
    private PlayerInputHandler _input;
    private PlayerDash _dash;
    private PlayerKnockback _knockback;
    private bool _isJumping;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _dash = GetComponent<PlayerDash>();
        _knockback = GetComponent<PlayerKnockback>();
    }

    void Update()
    {
        if (_dash != null && (_dash.IsDashing || _knockback.IsKnockedBack)) return;

        if (_input.JumpTriggered && IsGrounded())
        {
            Jump();
        }

        if (!_input.IsJumpHeld() && _rb.linearVelocity.y > 0 && _isJumping)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * jumpCutMultiplier);
            _isJumping = false;
        }

        if (_rb.linearVelocity.y <= 0)
        {
            _isJumping = false;
        }
    }

    private void Jump()
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        _isJumping = true;
    }

    public bool IsGrounded()
    {
        return Mathf.Abs(_rb.linearVelocity.y) < 0.05f;
    }
}