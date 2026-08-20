using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    private int _currentHealth;
    private float _iFramesTimer;
    private bool _isTouchingEnemy;
    private Vector2 _lastEnemyPosition;

    private Player _player;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _player.PlayerStats.maxHealth;
    public bool IsInvulnerable => _iFramesTimer > 0;

    void Awake()
    {
        _player = GetComponent<Player>();
        _currentHealth = _player.PlayerStats.maxHealth;
    }

    void FixedUpdate()
    {
        if (_iFramesTimer > 0)
        {
            _iFramesTimer -= Time.deltaTime;
            float alpha = (Mathf.Floor(_iFramesTimer / 0.05f) % 2 == 0) ? 0.2f : 1f;
            _player.Sprite.color = new Color(1f, 1f, 1f, alpha);
        }
        else 
        {
            if (_player.Sprite.color.a < 1f) _player.Sprite.color = Color.white;

            if (_isTouchingEnemy && !_player.Dash.IsDashing && !_player.Dash.HasPostDashProtection && !_player.Pogo.IsPlunging && !_player.Pogo.HasPostPogoProtection)
            {
                TakeDamage(1, _lastEnemyPosition);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            _isTouchingEnemy = true;
            _lastEnemyPosition = collision.transform.position;

            if (!_player.Dash.IsDashing && !_player.Dash.HasPostDashProtection && _iFramesTimer <= 0 && !_player.Pogo.IsPlunging && !_player.Pogo.HasPostPogoProtection)
            {
                TakeDamage(1, _lastEnemyPosition);
            }
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            _isTouchingEnemy = true;
            _lastEnemyPosition = collision.transform.position;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            _isTouchingEnemy = false;
        }
    }

    public void TakeDamage(int amount, Vector2 sourcePos)
    {
        if (_currentHealth <= 0) return;
        _currentHealth -= amount;
        _iFramesTimer = _player.PlayerStats.iFramesDuration;

        if (_player.ImpulseSource != null)
        {
            _player.ImpulseSource.GenerateImpulse();
        }

        if (_player.Dash.IsDashing) _player.Dash.StopDash();

        _player.Knockback.ApplyKnockback(sourcePos, _player.PlayerStats.damageForce, _player.PlayerStats.damageKnockbackDuration);
        VFXManager.Instance?.PlayVFX(VFXType.HitGeneric, transform.position, Vector2.zero);
        if (_currentHealth <= 0)
        {
            _player.AddControlRequest(Player.ControlReason.Death);
            _player.Collider.enabled = false;
            _player.Rb.bodyType = RigidbodyType2D.Kinematic;
            _player.Rb.linearVelocity = Vector2.zero;
            GameOverUI ui = FindAnyObjectByType<GameOverUI>();
            if (ui != null) ui.Show();
        }
    }

    public void Heal(int amount)
    {
        _currentHealth = Mathf.Clamp(_currentHealth + amount, 0, _player.PlayerStats.maxHealth);
    }

    public void ResetState()
    {
        _currentHealth = _player.PlayerStats.maxHealth;
        _iFramesTimer = _player.PlayerStats.iFramesDuration;
        _isTouchingEnemy = false;
        _player.Sprite.color = Color.white;
        _player.Collider.enabled = true;
    }
}
