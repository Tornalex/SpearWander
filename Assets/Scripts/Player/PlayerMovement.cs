using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 10f;
    [SerializeField] private float runSpeed = 16f;

    private Player _player;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void FixedUpdate()
    {
        if (_player.Knockback.IsKnockedBack || _player.Dash.IsDashing) 
        {
            _player.Animator.SetFloat("Speed", 0f);
            return;
        }

        float moveInput = _player.Input.MoveInput.x;
        float targetSpeed = (_player.Input.IsDashHeld() && Mathf.Abs(moveInput) > 0.1f) ? runSpeed : walkSpeed;
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