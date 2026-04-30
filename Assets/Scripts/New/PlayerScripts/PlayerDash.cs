using UnityEngine;

public class PlayerDash : MonoBehaviour
{
    [Header("Dash Frames Settings")]
    [SerializeField] private int dashDurationFrames = 10;
    [SerializeField] private int dashCooldownFrames = 50; 
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private int dashDamage = 1;
    [SerializeField] private Vector2 knockbackForce = new Vector2(12f, 6f);

    public bool IsDashing { get; private set; }
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
                StopDash();
            }
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
        _dashFrameCounter = dashDurationFrames;
        _cooldownFrameCounter = dashCooldownFrames;

        if (!_jump.IsGrounded()) _canAirDash = false;

        _rb.gravityScale = 0f;
        _rb.linearVelocity = Vector2.zero;
    }

    private void StopDash()
    {
        IsDashing = false;
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