using UnityEngine;
using SpearWander.Abilities;

public class PlayerMovement : MonoBehaviour
{
    private Player _player;

    private bool _isPlayerKnockedBack;
    private float _knockbackControlTimer;

    public bool IsForcedWalk { get; set; }

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void FixedUpdate()
    {
        if (IsForcedWalk) return;

        float moveInput = _player.Input.MoveInput.x;

        // ---------------------------------------------------------
        // PLAYER DAMAGE KNOCKBACK
        // ---------------------------------------------------------
        if (_isPlayerKnockedBack)
        {
            // Il periodo di controllo bloccato è ancora attivo.
            if (_knockbackControlTimer > 0f)
            {
                _knockbackControlTimer -= Time.fixedDeltaTime;

                _player.Animator.SetFloat("Speed", 0f);

                // NON modificare la velocity.
                // Il Rigidbody continua a muoversi grazie
                // all'impulso del knockback e alla gravità.
                return;
            }

            // Dopo il periodo di blocco, il giocatore può
            // interrompere il knockback iniziando a muoversi.
            if (Mathf.Abs(moveInput) > 0.1f)
            {
                _isPlayerKnockedBack = false;
            }
            // Se è a terra, il knockback è terminato.
            else if (_player.Feet.IsGrounded())
            {
                _isPlayerKnockedBack = false;
            }
            else
            {
                // Ancora in aria e nessun input:
                // lasciare completamente libera la velocity.
                _player.Animator.SetFloat("Speed", 0f);
                return;
            }
        }

        // ---------------------------------------------------------
        // GENERIC KNOCKBACK
        // ---------------------------------------------------------
        // Questo serve per il knockback del Dash:
        //
        // Dash → Knockback.ApplyKnockback()
        //
        // Non permettiamo al normale movimento di sovrascrivere
        // la velocity durante quei 0.2 secondi.
        if (_player.Knockback.IsKnockedBack)
        {
            _player.Animator.SetFloat("Speed", 0f);
            return;
        }

        // ---------------------------------------------------------
        // NORMAL MOVEMENT
        // ---------------------------------------------------------
        if (!_player.HasControl || _player.Dash.IsDashing)
        {
            _player.Animator.SetFloat("Speed", 0f);

            if (!_player.HasControl)
            {
                _player.Rb.linearVelocity =
                    new Vector2(
                        0f,
                        _player.Rb.linearVelocity.y
                    );
            }

            return;
        }

        bool canRun =
            AbilityManager.Instance != null &&
            AbilityManager.Instance.IsUnlocked(AbilityType.Dash);

        float targetSpeed =
            (canRun &&
             _player.Input.IsDashHeld() &&
             Mathf.Abs(moveInput) > 0.1f)
                ? _player.PlayerStats.runSpeed
                : _player.PlayerStats.walkSpeed;

        _player.Animator.SetFloat(
            "Speed",
            Mathf.Abs(moveInput * targetSpeed)
        );

        _player.Rb.linearVelocity =
            new Vector2(
                moveInput * targetSpeed,
                _player.Rb.linearVelocity.y
            );

        if (Mathf.Abs(moveInput) > 0.1f)
        {
            Flip(Mathf.Sign(moveInput));
        }
    }

    public void StartPlayerKnockback(float controlDuration)
    {
        _isPlayerKnockedBack = true;
        _knockbackControlTimer = controlDuration;
    }

    private void Flip(float direction)
    {
        if ((direction > 0 && transform.localScale.x < 0) ||
            (direction < 0 && transform.localScale.x > 0))
        {
            Vector3 scale = transform.localScale;
            scale.x *= -1;
            transform.localScale = scale;
        }
    }
}