using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float speed = 8f;
    [SerializeField] float jumpForce = 12f;
    [SerializeField] float coyoteTimeDuration = 0.15f;

    private Rigidbody2D _rb;
    private PlayerInputHandler _input;
    private PlayerFeet _feet;
    private float _coyoteTimer;

    void Awake() 
    {
        _rb = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _feet = GetComponentInChildren<PlayerFeet>();
    }

    void Update()
    {
        HandleCoyoteTime();
        if (_input.JumpTriggered && _coyoteTimer > 0  && _feet.isGrounded) Jump();
        
        if (!_input.IsJumpHeld() && _rb.linearVelocity.y > 0)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * 0.5f);
    }

    void FixedUpdate()
    {
        _rb.linearVelocity = new Vector2(_input.MoveInput.x * speed, _rb.linearVelocity.y);
    }

    void HandleCoyoteTime()
    {
        if (_feet.isGrounded) _coyoteTimer = coyoteTimeDuration;
        else _coyoteTimer -= Time.deltaTime;
    }

    void Jump()
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
        _coyoteTimer = 0;
    }
}