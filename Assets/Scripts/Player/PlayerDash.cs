using UnityEngine;

public class PlayerDash : MonoBehaviour
{
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
            _player.Rb.linearVelocity = new Vector2(direction * _player.DashStats.dashSpeed, 0f);

            if (_dashTimer <= 0) StopDash();
        }
    }

    private bool CanDash() => _player.HasControl && !IsDashing && !_player.Knockback.IsKnockedBack && _dashCooldownTimer <= 0 && (_player.Jump.IsGrounded() || _canAirDash);

    private void StartDash()
    {
        IsDashing = true;
        _dashTimer = _player.DashStats.dashDuration;
        _dashCooldownTimer = _player.DashStats.dashCooldown;
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
        _postDashProtectionTimer = _player.DashStats.postDashInvincibility;
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
                damageable.TakeDamage(_player.CombatStats.dashDamage, collision.contacts[0].point, transform.position);
                SFXManager.Instance?.PlaySFX(SFXType.HitDash);
                StopDash();
                _player.Knockback.ApplyKnockback(collision.transform.position, _player.CombatStats.dashKnockbackForce, _player.CombatStats.dashKnockbackDuration);
            }
        }
    }
}
