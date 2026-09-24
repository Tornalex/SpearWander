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
        if (_player.Jump.IsGrounded() && !IsDashing)
            _canAirDash = true;

        if (_player.Input.DashTriggered && CanDash())
            StartDash();
    }

    void FixedUpdate()
    {
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.fixedDeltaTime;

        // ---------------------------------------------------------
        // DASH KNOCKBACK
        // ---------------------------------------------------------

        if (_player.Knockback.IsPersistentKnockback())
        {
            HasPostDashProtection = true;

            if (_player.Knockback.IsControlLocked())
                return;

            float moveInput =
                _player.Input.MoveInput.x;

            if (Mathf.Abs(moveInput) > 0.1f)
            {
                EndDashKnockback();
                return;
            }

            if (_player.Feet.IsGrounded())
            {
                EndDashKnockback();
                return;
            }

            return;
        }

        // ---------------------------------------------------------
        // POST DASH PROTECTION
        // ---------------------------------------------------------

        if (_postDashProtectionTimer > 0f)
        {
            _postDashProtectionTimer -= Time.fixedDeltaTime;

            if (_postDashProtectionTimer <= 0f)
                HasPostDashProtection = false;
        }

        // ---------------------------------------------------------
        // DASH
        // ---------------------------------------------------------

        if (IsDashing)
        {
            _dashTimer -= Time.fixedDeltaTime;

            float direction =
                Mathf.Sign(transform.localScale.x);

            _player.Rb.linearVelocity =
                new Vector2(
                    direction *
                    _player.DashStats.dashSpeed,
                    0f
                );

            if (_dashTimer <= 0f)
                StopDash();
        }
    }

    private bool CanDash() =>
        _player.HasControl &&
        !IsDashing &&
        !_player.Knockback.IsKnockedBack &&
        _dashCooldownTimer <= 0f &&
        (_player.Jump.IsGrounded() || _canAirDash);

    private void StartDash()
    {
        IsDashing = true;

        _dashTimer =
            _player.DashStats.dashDuration;

        _dashCooldownTimer =
            _player.DashStats.dashCooldown;

        if (!_player.Jump.IsGrounded())
            _canAirDash = false;

        _player.Rb.gravityScale = 0f;
        _player.Rb.linearVelocity = Vector2.zero;

        _player.Animator.SetBool(
            "IsDashing",
            true
        );
    }

    public void StopDash()
    {
        if (!IsDashing)
            return;

        IsDashing = false;

        _player.Rb.gravityScale =
            _originalGravityScale;

        _postDashProtectionTimer =
            _player.DashStats.postDashInvincibility;

        HasPostDashProtection = true;

        _player.Animator.SetBool(
            "IsDashing",
            false
        );
    }

    public void ResetAirDash()
    {
        _canAirDash = true;
        _dashCooldownTimer = 0f;
    }

    private void OnCollisionEnter2D(
        Collision2D collision)
    {
        if (!IsDashing ||
            !collision.gameObject.CompareTag("Enemy"))
        {
            return;
        }

        IDamageable damageable =
            collision.gameObject.GetComponent<IDamageable>();

        if (damageable == null)
            return;

        // ---------------------------------------------------------
        // SPEAR NON EQUIPAGGIATA
        // ---------------------------------------------------------

        if (!_player.Combat.HasSpear)
        {
            // Il Dash non può danneggiare il nemico.
            // Il Player subisce invece il normale danno
            // da contatto e viene respinto.
            _player.Health.TakeDamage(
                1,
                collision.transform.position
            );

            return;
        }

        // ---------------------------------------------------------
        // DASH CON SPEAR
        // ---------------------------------------------------------

        StopDash();

        HasPostDashProtection = true;

        damageable.TakeDamage(
            _player.CombatStats.dashDamage,
            collision.contacts[0].point,
            transform.position
        );

        SFXManager.Instance?.PlaySFX(
            SFXType.HitDash
        );

        _player.Knockback.ApplyKnockback(
            collision.transform.position,
            _player.DashStats.knockbackForce,
            _player.DashStats.knockbackDuration,
            true
        );
    }

    private void EndDashKnockback()
    {
        _player.Knockback.EndKnockback();

        HasPostDashProtection = false;
        _postDashProtectionTimer = 0f;
    }
}