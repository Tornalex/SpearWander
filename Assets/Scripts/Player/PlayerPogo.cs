using UnityEngine;

public class PlayerPogo : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float plungeSpeed = 20f;
    [SerializeField] private float bounceForce = 15f;
    
    [Header("Timers")]
    [SerializeField] private float postPogoInvincibility = 0.17f;
    [SerializeField] private float pogoStunDuration = 0.13f;

    private Player _player;
    private PlayerFeet _feet;
    private float _postPogoProtectionTimer;
    private float _pogoStunTimer;

    public bool IsPlunging { get; private set; }
    public bool HasPostPogoProtection { get; private set; }
    public bool IsPogoStunned { get; private set; }

    private bool _canPlunge = true;
    private bool _waitingForInputRelease;

    void Awake()
    {
        _player = GetComponent<Player>();
        _feet = GetComponentInChildren<PlayerFeet>();
    }

    void Update()
    {
        if (!_player.Input.DownInputHeld)
        {
            _waitingForInputRelease = false;
        }

        if (!_feet.IsGrounded() && (_player.Input.DownInputHeld || _player.Input.DownTriggered) && _canPlunge && !IsPlunging && !_waitingForInputRelease && !IsPogoStunned)
        {
            StartPlunge();
        }

        if (_feet.IsGrounded())
        {
            IsPlunging = false;
            _canPlunge = true;
        }
    }

    void FixedUpdate()
    {
        if (_postPogoProtectionTimer > 0)
        {
            _postPogoProtectionTimer -= Time.deltaTime;
            if (_postPogoProtectionTimer <= 0) HasPostPogoProtection = false;
        }

        if (_pogoStunTimer > 0)
        {
            _pogoStunTimer -= Time.deltaTime;
            if (_pogoStunTimer <= 0) IsPogoStunned = false;
        }
    }

    private void StartPlunge()
    {
        IsPlunging = true;
        _canPlunge = false;
        _waitingForInputRelease = true;
        _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, -plungeSpeed);
    }

    public void OnPogoHit(Collision2D hit)
    {
        IBounceable bounceable = hit.collider.GetComponent<IBounceable>();

        if (bounceable != null)
        {
            ExecuteBounce(bounceable, hit.collider.gameObject);
        }
    }

    private void ExecuteBounce(IBounceable bounceable, GameObject hitObj)
    {
        IsPlunging = false;
        _canPlunge = true;

        bounceable.OnPogoBounce();

        transform.position = new Vector2(transform.position.x, transform.position.y + 0.2f);
        
        float finalForce = bounceForce * bounceable.GetBounceMultiplier();
        _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, finalForce);

        if (_player.Dash != null) _player.Dash.ResetAirDash();

        _postPogoProtectionTimer = postPogoInvincibility;
        HasPostPogoProtection = true;
        _pogoStunTimer = pogoStunDuration;
        IsPogoStunned = true;

        IDamageable damageable = hitObj.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(1, transform.position, transform.position);
        }

        if (_player.ImpulseSource != null) _player.ImpulseSource.GenerateImpulse();
        SFXManager.Instance?.PlaySFX(SFXType.PogoHit);
    }
}
