using UnityEngine;

public class PlayerDash : MonoBehaviour
{
    [Header("Dash Settings")]
    [SerializeField] private float dashDuration = 0.17f;
    [SerializeField] private float dashCooldown = 0.83f; 
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private int dashDamage = 1;
    [SerializeField] private float postDashInvincibility = 0.05f;

    [Header("Knockback Settings")]
    [SerializeField] private Vector2 knockbackForce = new Vector2(10f, 5f);
    [SerializeField] private float knockbackDuration = 0.2f;

    public bool IsDashing { get; private set; }
    public bool HasPostDashProtection { get; private set; }

    private bool _canAirDash = true;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private float _postDashProtectionTimer;
    private float _originalGravityScale;
    
    private Player _player;

    void Awake()
    {
        _player = GetComponent<Player>();
        _originalGravityScale = _player.Rb.gravityScale;
    }

    void Update()
    {
        if (_player.Jump.IsGrounded() && !IsDashing) _canAirDash = true;
        if (_player.Input.DashTriggered && CanDash()) StartDash();
    }

    void FixedUpdate()
    {
        if (_dashCooldownTimer > 0) _dashCooldownTimer -= Time.deltaTime;

        if (_postDashProtectionTimer > 0)
        {
            _postDashProtectionTimer -= Time.deltaTime;
            if (_postDashProtectionTimer <= 0) HasPostDashProtection = false;
        }

        if (IsDashing)
        {
            _dashTimer -= Time.deltaTime;
            float direction = Mathf.Sign(transform.localScale.x);
            _player.Rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);

            if (_dashTimer <= 0) StopDash();
        }
    }

    private bool CanDash() => !IsDashing && !_player.Knockback.IsKnockedBack && _dashCooldownTimer <= 0 && (_player.Jump.IsGrounded() || _canAirDash);

    private void StartDash()
    {
        IsDashing = true;
        _dashTimer = dashDuration;
        _dashCooldownTimer = dashCooldown;
        if (!_player.Jump.IsGrounded()) _canAirDash = false;
        _player.Rb.gravityScale = 0f;
        _player.Rb.linearVelocity = Vector2.zero;

        _player.Animator.SetBool("IsDashing", true);
    }

    public void StopDash()
    {
        if (!IsDashing) return;
        IsDashing = false;
        _player.Rb.gravityScale = _originalGravityScale;
        _postDashProtectionTimer = postDashInvincibility;
        HasPostDashProtection = true;

        _player.Animator.SetBool("IsDashing", false);
    }

    public void ResetAirDash()
    {
        _canAirDash = true;
        _dashCooldownTimer = 0f;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsDashing && collision.gameObject.CompareTag("Enemy"))
        {
            IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(dashDamage, collision.contacts[0].point, transform.position);
                SFXManager.Instance?.PlaySFX(SFXType.HitDash);
                StopDash();
                _player.Knockback.ApplyKnockback(collision.transform.position, knockbackForce, knockbackDuration);
            }
        }
    }
}
