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

    [Header("Recoil Settings")]
    [SerializeField] private Vector2 recoilForce = new Vector2(10f, 5f);
    [SerializeField] private int recoilFrames = 12;

    public bool IsDashing { get; private set; }
    public bool HasPostDashProtection { get; private set; }

    private bool _canAirDash = true;
    private int _dashFrameCounter;
    private int _cooldownFrameCounter;
    
    private Rigidbody2D _rb;
    private PlayerInputHandler _input;
    private PlayerJump _jump;
    private PlayerKnockback _knockback;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _jump = GetComponent<PlayerJump>();
        _knockback = GetComponent<PlayerKnockback>();
    }

    void Update()
    {
        if (_jump.IsGrounded() && !IsDashing) _canAirDash = true;
        if (_input.DashTriggered && CanDash()) StartDash();
    }

    void FixedUpdate()
    {
        if (_cooldownFrameCounter > 0) _cooldownFrameCounter--;
        
        if (IsDashing)
        {
            _dashFrameCounter--;
            float direction = Mathf.Sign(transform.localScale.x);
            _rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);

            if (_dashFrameCounter <= 0) StopDash();
        }
    }

    private bool CanDash() => !IsDashing && !_knockback.IsKnockedBack && _cooldownFrameCounter <= 0 && (_jump.IsGrounded() || _canAirDash);

    private void StartDash()
    {
        IsDashing = true;
        _dashFrameCounter = dashDurationFrames;
        _cooldownFrameCounter = dashCooldownFrames;
        if (!_jump.IsGrounded()) _canAirDash = false;
        _rb.gravityScale = 0f;
        _rb.linearVelocity = Vector2.zero;
    }

    public void StopDash()
    {
        if (!IsDashing) return;
        IsDashing = false;
        _rb.gravityScale = 5f;
        StartCoroutine(PostDashProtectionRoutine());
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
            Enemy enemy = collision.gameObject.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(dashDamage);
                VFXManager.Instance.PlayVFX(VFXType.HitDash, collision.contacts[0].point);
                SFXManager.Instance.PlaySFX(SFXType.HitDash);
                _canAirDash = true;
                _cooldownFrameCounter = 0;
                StopDash();
                _knockback.ApplyKnockback(collision.transform.position, recoilForce, recoilFrames);
            }
        }
    }
}