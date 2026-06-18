using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float iFramesDuration = 1f;

    [Header("Damage Knockback")]
    [SerializeField] private Vector2 damageForce = new Vector2(15f, 10f);
    [SerializeField] private float damageKnockbackDuration = 0.42f;

    [Header("Essence Settings")]
    [SerializeField] private float maxEssence = 100f;
    [SerializeField] private float essencePerBaseCatch = 20f;
    [SerializeField] private float essenceCostPerHeal = 30f;
    [SerializeField] private int healthPerHeal = 1;

    private int _currentHealth;
    private float _currentEssence;
    private float _iFramesTimer;
    private bool _isTouchingEnemy;
    private Vector2 _lastEnemyPosition;
    
    private Player _player;
    public float CurrentEssence => _currentEssence;
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public float MaxEssence => maxEssence;
    public bool IsInvulnerable => _iFramesTimer > 0;


    void Awake()
    {
        _currentHealth = maxHealth;
        _currentEssence = 0f;
        _player = GetComponent<Player>();
    }

    void Update()
    {
        if (_player.Input.HealTriggered)
        {
            TryHeal();
        }
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
        _currentHealth -= amount;
        _iFramesTimer = iFramesDuration;

        if (_player.ImpulseSource != null)
        {
            _player.ImpulseSource.GenerateImpulse();
        }

        if (_player.Dash.IsDashing) _player.Dash.StopDash();

        _player.Knockback.ApplyKnockback(sourcePos, damageForce, damageKnockbackDuration);
        VFXManager.Instance?.PlayVFX(VFXType.HitGeneric, transform.position, Vector2.zero);
        if (_currentHealth <= 0) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void AddEssenceFromCatch(bool perfectCatch)
    {
        float amountToAdd = perfectCatch ? 40f : essencePerBaseCatch;
        _currentEssence = Mathf.Clamp(_currentEssence + amountToAdd, 0f, maxEssence);
    }

    private void TryHeal()
    {
        if (_currentHealth < maxHealth && _currentEssence >= essenceCostPerHeal)
        {
            _currentEssence -= essenceCostPerHeal;
            _currentHealth = Mathf.Clamp(_currentHealth + healthPerHeal, 0, maxHealth);
        }
    }

    public bool ConsumeEssence(float amount)
    {
        if (_currentEssence >= amount)
        {
            _currentEssence -= amount;
            return true;
        }
        return false;
    }
}
