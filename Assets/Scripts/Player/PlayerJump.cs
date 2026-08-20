using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    private Player _player;
    private PlayerFeet _feet;
    private bool _isJumping;
    private bool _wasGrounded;
    private bool _hasJumped;
    private float _coyoteTimer;
    private float _jumpBufferTimer;

    void Awake()
    {
        _player = GetComponent<Player>();
        _feet = GetComponentInChildren<PlayerFeet>();
    }

    void Update()
    {
        if (!_player.HasControl) return;
        if (_player.Dash != null && (_player.Dash.IsDashing || _player.Knockback.IsKnockedBack)) return;

        bool grounded = IsGrounded();

        _player.Animator.SetFloat("yVelocity", _player.Rb.linearVelocity.y);
        _player.Animator.SetBool("IsGrounded", grounded);

        if (_wasGrounded && !grounded && !_hasJumped)
            _coyoteTimer = _player.PlayerStats.coyoteTime;
        else if (!grounded)
            _coyoteTimer -= Time.deltaTime;

        if (grounded && !_wasGrounded)
            _hasJumped = false;

        _wasGrounded = grounded;

        if (_player.Input.JumpTriggered) _jumpBufferTimer = _player.PlayerStats.jumpBufferTime;
        else _jumpBufferTimer -= Time.deltaTime;

        if (_jumpBufferTimer > 0 && (grounded || _coyoteTimer > 0))
        {
            Jump();
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
        }

        if (!_player.Input.IsJumpHeld() && _player.Rb.linearVelocity.y > 0 && _isJumping)
        {
            _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, _player.Rb.linearVelocity.y * _player.PlayerStats.jumpCutMultiplier);
            _isJumping = false;
        }

        if (_player.Rb.linearVelocity.y <= 0)
        {
            _isJumping = false;
        }
    }

    private void Jump()
    {
        _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, _player.PlayerStats.jumpForce);
        _isJumping = true;
        _hasJumped = true;
        _player.Animator.SetTrigger("JumpTrigger");
    }

    public bool IsGrounded()
    {
        return _feet != null && _feet.IsGrounded();
    }
}