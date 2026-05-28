using UnityEngine;
using System.Collections;

public class PlayerDash : MonoBehaviour
{
    [Header("Dash Settings")]
    [SerializeField] private int dashDurationFrames = 10;
    [SerializeField] private int dashCooldownFrames = 50; 
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private int dashDamage = 1;
    [SerializeField] private int postDashIFrames = 3;

    [Header("Knockback Settings")]
    [SerializeField] private Vector2 knockbackForce = new Vector2(10f, 5f);
    [SerializeField] private int knockbackFrames = 12;

    public bool IsDashing { get; private set; }
    public bool HasPostDashProtection { get; private set; }

    private bool _canAirDash = true;
    private int _dashFrameCounter;
    private int _cooldownFrameCounter;
    
    private Player _player;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void Update()
    {
        if (_player.Jump.IsGrounded() && !IsDashing) _canAirDash = true;
        if (_player.Input.DashTriggered && CanDash()) StartDash();
    }

    void FixedUpdate()
    {
        if (_cooldownFrameCounter > 0) _cooldownFrameCounter--;
        
        if (IsDashing)
        {
            _dashFrameCounter--;
            float direction = Mathf.Sign(transform.localScale.x);
            _player.Rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);

            if (_dashFrameCounter <= 0) StopDash();
        }
    }

    private bool CanDash() => !IsDashing && !_player.Knockback.IsKnockedBack && _cooldownFrameCounter <= 0 && (_player.Jump.IsGrounded() || _canAirDash);

    private void StartDash()
    {
        IsDashing = true;
        _dashFrameCounter = dashDurationFrames;
        _cooldownFrameCounter = dashCooldownFrames;
        if (!_player.Jump.IsGrounded()) _canAirDash = false;
        _player.Rb.gravityScale = 0f;
        _player.Rb.linearVelocity = Vector2.zero;
    }

    public void StopDash()
    {
        if (!IsDashing) return;
        IsDashing = false;
        _player.Rb.gravityScale = 5f;
        StartCoroutine(PostDashProtectionRoutine());
    }

    public void ResetAirDash()
    {
        _canAirDash = true;
        _cooldownFrameCounter = 0;
    }

    private IEnumerator PostDashProtectionRoutine()
    {
        HasPostDashProtection = true;
        for (int i = 0; i < postDashIFrames; i++) yield return new WaitForFixedUpdate();
        HasPostDashProtection = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsDashing && collision.gameObject.CompareTag("Enemy"))
        {
            IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(dashDamage, collision.contacts[0].point);
                SFXManager.Instance.PlaySFX(SFXType.HitDash);
                //_cooldownFrameCounter = 0;
                StopDash();
                _player.Knockback.ApplyKnockback(collision.transform.position, knockbackForce, knockbackFrames);
            }
        }
    }
}