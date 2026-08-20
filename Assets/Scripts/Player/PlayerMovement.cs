using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Player _player;

    public bool IsForcedWalk { get; set; }

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void FixedUpdate()
    {
        if (IsForcedWalk) return;

        if (!_player.HasControl || _player.Knockback.IsKnockedBack || _player.Dash.IsDashing)
        {
            _player.Animator.SetFloat("Speed", 0f);
            if (!_player.HasControl) _player.Rb.linearVelocity = new Vector2(0f, _player.Rb.linearVelocity.y);
            return;
        }

        float moveInput = _player.Input.MoveInput.x;
        float targetSpeed = (_player.Input.IsDashHeld() && Mathf.Abs(moveInput) > 0.1f) ? _player.PlayerStats.runSpeed : _player.PlayerStats.walkSpeed;
        _player.Animator.SetFloat("Speed", Mathf.Abs(moveInput * targetSpeed));

        _player.Rb.linearVelocity = new Vector2(moveInput * targetSpeed, _player.Rb.linearVelocity.y);

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