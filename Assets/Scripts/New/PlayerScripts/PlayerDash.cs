using UnityEngine;

public class PlayerDash : MonoBehaviour
{
    [Header("Dash Frames Settings")]
    [SerializeField] private int dashDurationFrames = 10;
    [SerializeField] private int dashCooldownFrames = 50; 
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float runSpeed = 16f; 
    [SerializeField] private int dashDamage = 1;
    [SerializeField] private Vector2 knockbackForce = new Vector2(12f, 6f);

    public bool IsDashing { get; private set; }
    public bool IsRunning { get; private set; } 
    public bool IsKnockedBack { get; private set; }

    private bool _canAirDash = true;
    private int _dashFrameCounter;
    private int _cooldownFrameCounter;
    
    private Rigidbody2D _rb;
    private PlayerInputHandler _input;
    private PlayerJump _jump;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _jump = GetComponent<PlayerJump>();
    }

    void Update()
    {
        if (_jump.IsGrounded() && !IsDashing)
        {
            _canAirDash = true;
        }

        if (_input.DashTriggered && CanDash())
        {
            StartDash();
        }

        if (!_input.IsDashHeld())
        {
            IsRunning = false;
        }
    }

    void FixedUpdate()
    {
        if (_cooldownFrameCounter > 0)
        {
            _cooldownFrameCounter--;
        }

        if (IsDashing)
        {
            _dashFrameCounter--;
            
            float direction = Mathf.Sign(transform.localScale.x);
            _rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);

            if (_dashFrameCounter <= 0) 
            {
                TransitionToRun();
            }
        }
        else if (IsRunning && _input.IsDashHeld())
        {
            HandleRunningMovement();
        }
    }

    private void HandleRunningMovement()
    {
        float moveInput = _input.MoveInput.x;

        // Se c'è input direzionale, segui l'input, altrimenti continua dritto
        if (Mathf.Abs(moveInput) > 0.1f)
        {
            float direction = Mathf.Sign(moveInput);
            _rb.linearVelocity = new Vector2(direction * runSpeed, _rb.linearVelocity.y);
            Flip(direction);
        }
        else
        {
            float currentFacing = Mathf.Sign(transform.localScale.x);
            _rb.linearVelocity = new Vector2(currentFacing * runSpeed, _rb.linearVelocity.y);
        }

        _rb.gravityScale = 5f; 
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

    private bool CanDash()
    {
        if (IsDashing || IsKnockedBack || _cooldownFrameCounter > 0) return false;
        if (!_jump.IsGrounded() && !_canAirDash) return false;
        return true;
    }

    private void StartDash()
    {
        IsDashing = true;
        IsRunning = true; 
        _dashFrameCounter = dashDurationFrames;
        _cooldownFrameCounter = dashCooldownFrames;

        if (!_jump.IsGrounded()) _canAirDash = false;

        _rb.gravityScale = 0f;
        _rb.linearVelocity = Vector2.zero;
    }

    private void TransitionToRun()
    {
        IsDashing = false;
        _rb.gravityScale = 5f;

        if (!_input.IsDashHeld())
        {
            IsRunning = false;
        }
    }

    private void StopDash()
    {
        IsDashing = false;
        IsRunning = false;
        _rb.gravityScale = 5f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsDashing && collision.gameObject.CompareTag("Enemy"))
        {
            Enemy enemy = collision.gameObject.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(dashDamage);
                _canAirDash = true;
                _cooldownFrameCounter = 0;
                ApplyKnockback(collision.transform.position);
            }
        }
    }

    private void ApplyKnockback(Vector3 enemyPosition)
    {
        StopDash();
        IsKnockedBack = true;

        float dir = transform.position.x < enemyPosition.x ? -1f : 1f;
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(new Vector2(knockbackForce.x * dir, knockbackForce.y), ForceMode2D.Impulse);

        Invoke(nameof(ResetKnockback), 0.25f);
    }

    private void ResetKnockback() => IsKnockedBack = false;
}