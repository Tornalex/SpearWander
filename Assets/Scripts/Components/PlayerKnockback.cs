using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Knockback : MonoBehaviour
{
    public bool IsKnockedBack { get; private set; }

    private Rigidbody2D _rb;
    private float _knockbackTimer;

    private bool _persistentUntilGroundOrInput;
    private bool _controlLocked;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (!IsKnockedBack)
            return;

        // Per il knockback normale il timer determina
        // semplicemente la durata del knockback.
        if (!_persistentUntilGroundOrInput)
        {
            if (_knockbackTimer <= 0f)
                return;

            _knockbackTimer -= Time.fixedDeltaTime;

            if (_knockbackTimer <= 0f)
                IsKnockedBack = false;

            return;
        }

        // Knockback persistente:
        // il timer serve SOLO a bloccare il controllo.
        if (_controlLocked)
        {
            _knockbackTimer -= Time.fixedDeltaTime;

            if (_knockbackTimer <= 0f)
                _controlLocked = false;
        }
    }

    public void ApplyKnockback(
        Vector2 sourcePosition,
        Vector2 force,
        float duration)
    {
        ApplyKnockback(
            sourcePosition,
            force,
            duration,
            false
        );
    }

    public void ApplyKnockback(
        Vector2 sourcePosition,
        Vector2 force,
        float duration,
        bool persistentUntilGroundOrInput)
    {
        IsKnockedBack = true;

        _knockbackTimer = duration;
        _persistentUntilGroundOrInput =
            persistentUntilGroundOrInput;

        _controlLocked = persistentUntilGroundOrInput;

        float direction =
            transform.position.x < sourcePosition.x
                ? -1f
                : 1f;

        _rb.linearVelocity = Vector2.zero;

        _rb.AddForce(
            new Vector2(
                direction * force.x,
                force.y
            ),
            ForceMode2D.Impulse
        );
    }

    public bool IsControlLocked()
    {
        return _persistentUntilGroundOrInput &&
               _controlLocked;
    }

    public bool IsPersistentKnockback()
    {
        return _persistentUntilGroundOrInput;
    }

    public void EndKnockback()
    {
        IsKnockedBack = false;
        _knockbackTimer = 0f;
        _persistentUntilGroundOrInput = false;
        _controlLocked = false;
    }
}