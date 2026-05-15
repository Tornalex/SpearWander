using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class PlayerPogo : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float plungeSpeed = 20f;
    [SerializeField] private float bounceForce = 15f;
    
    [Header("Timers")]
    [SerializeField] private int postPogoIFrames = 10;
    [SerializeField] private int pogoStunFrames = 8;

    private Rigidbody2D _rb;
    private PlayerInputHandler _input;
    private PlayerDash _dashScript;
    private PlayerFeet _feet;
    private CinemachineImpulseSource _impulse;

    public bool IsPlunging { get; private set; }
    public bool HasPostPogoProtection { get; private set; }
    public bool IsPogoStunned { get; private set; }

    private bool _canPlunge = true;
    private bool _waitingForInputRelease;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _dashScript = GetComponent<PlayerDash>();
        _feet = GetComponentInChildren<PlayerFeet>();
        _impulse = GetComponent<CinemachineImpulseSource>();
    }

    void Update()
    {
        if (!_input.DownInputHeld)
        {
            _waitingForInputRelease = false;
        }

        if (!_feet.IsGrounded() && (_input.DownInputHeld || _input.DownTriggered) && _canPlunge && !IsPlunging && !_waitingForInputRelease && !IsPogoStunned)
        {
            StartPlunge();
        }

        if (_feet.IsGrounded())
        {
            IsPlunging = false;
            _canPlunge = true;
        }
    }

    private void StartPlunge()
    {
        IsPlunging = true;
        _canPlunge = false;
        _waitingForInputRelease = true;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -plungeSpeed);
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
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, finalForce);

        if (_dashScript != null) _dashScript.ResetAirDash();
        
        StartCoroutine(PostPogoProtectionRoutine());
        StartCoroutine(PogoStunRoutine());

        IDamageable damageable = hitObj.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(1, transform.position);
        }

        if (_impulse != null) _impulse.GenerateImpulse();
        SFXManager.Instance.PlaySFX(SFXType.PogoHit);
    }

    private IEnumerator PostPogoProtectionRoutine()
    {
        HasPostPogoProtection = true;
        for (int i = 0; i < postPogoIFrames; i++) yield return new WaitForFixedUpdate();
        HasPostPogoProtection = false;
    }

    private IEnumerator PogoStunRoutine()
    {
        IsPogoStunned = true;
        for (int i = 0; i < pogoStunFrames; i++) yield return new WaitForFixedUpdate();
        IsPogoStunned = false;
    }
}