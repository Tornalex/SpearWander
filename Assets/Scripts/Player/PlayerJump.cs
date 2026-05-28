using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;

    private Player _player;
    private bool _isJumping;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void Update()
    {
        if (_player.Dash != null && (_player.Dash.IsDashing || _player.Knockback.IsKnockedBack)) return;

        if (_player.Input.JumpTriggered && IsGrounded())
        {
            Jump();
        }

        if (!_player.Input.IsJumpHeld() && _player.Rb.linearVelocity.y > 0 && _isJumping)
        {
            _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, _player.Rb.linearVelocity.y * jumpCutMultiplier);
            _isJumping = false;
        }

        if (_player.Rb.linearVelocity.y <= 0)
        {
            _isJumping = false;
        }
    }

    private void Jump()
    {
        _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, jumpForce);
        _isJumping = true;
    }

    public bool IsGrounded()
    {
        return Mathf.Abs(_player.Rb.linearVelocity.y) < 0.05f;
    }
}