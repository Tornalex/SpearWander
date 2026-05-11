using UnityEngine;
using System.Collections;

public class PlayerPogo : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float plungeSpeed = 20f;
    [SerializeField] private float bounceForce = 15f;
    [SerializeField] private LayerMask pogoLayer;
    [SerializeField] private int postPogoIFrames = 10;

    private Rigidbody2D _rb;
    private PlayerInputHandler _input;
    private PlayerDash _dashScript;
    private PlayerFeet _feet;

    public bool IsPlunging { get; private set; }
    public bool HasPostPogoProtection { get; private set; }
    private bool _canPlunge = true;
    private bool _waitingForInputRelease;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _dashScript = GetComponent<PlayerDash>();
        _feet = GetComponentInChildren<PlayerFeet>();
    }

    void Update()
    {
        if (!_input.DownInputHeld)
        {
            _waitingForInputRelease = false;
        }

        if (!_feet.IsGrounded() && _input.DownInputHeld && _canPlunge && !IsPlunging && !_waitingForInputRelease)
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
        if (((1 << hit.gameObject.layer) & pogoLayer) != 0)
        {
            ExecuteBounce(hit);
        }
    }

    private void ExecuteBounce(Collision2D hit)
    {
        IsPlunging = false;
        _canPlunge = true;

        transform.position = new Vector2(transform.position.x, transform.position.y + 0.2f);
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, bounceForce);

        if (_dashScript != null) _dashScript.ResetAirDash();
        
        StartCoroutine(PostPogoProtectionRoutine());

        IDamageable damageable = hit.gameObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(1, transform.position);
        }

        SFXManager.Instance.PlaySFX(SFXType.PogoHit);
    }

    private IEnumerator PostPogoProtectionRoutine()
    {
        HasPostPogoProtection = true;
        for (int i = 0; i < postPogoIFrames; i++) yield return new WaitForFixedUpdate();
        HasPostPogoProtection = false;
    }
}