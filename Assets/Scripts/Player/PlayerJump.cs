using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;

    private Player _player;
    private PlayerFeet _feet;
    private bool _isJumping;

    void Awake()
    {
        _player = GetComponent<Player>();
        _feet = GetComponentInChildren<PlayerFeet>();
    }

    void Update()
    {
        if (_player.Dash != null && (_player.Dash.IsDashing || _player.Knockback.IsKnockedBack)) return;
        _player.Animator.SetFloat("yVelocity", _player.Rb.linearVelocity.y);
        _player.Animator.SetBool("IsGrounded", IsGrounded());

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
        _player.Animator.SetTrigger("JumpTrigger");
    }

    public bool IsGrounded()
    {
        return _feet != null && _feet.IsGrounded();
    }
}