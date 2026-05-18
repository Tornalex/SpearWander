using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int iFramesDuration = 60;

    [Header("Damage Knockback")]
    [SerializeField] private Vector2 damageForce = new Vector2(15f, 10f);
    [SerializeField] private int damageFrames = 25;

    [Header("Essence Settings")]
    [SerializeField] private float maxEssence = 100f;
    [SerializeField] private float essencePerBaseCatch = 20f;
    [SerializeField] private float essenceCostPerHeal = 30f;
    [SerializeField] private int healthPerHeal = 1;

    private int _currentHealth;
    private int _iFramesCounter;
    private bool _isTouchingEnemy;
    private Vector2 _lastEnemyPosition;
    private float _currentEssence;
    
    private PlayerDash _dash;
    private PlayerPogo _pogo;
    private PlayerKnockback _knockback;
    private SpriteRenderer _sprite;
    private CinemachineImpulseSource _impulseSource;
    private PlayerInputHandler _input;

    public float CurrentEssence => _currentEssence;

    void Awake()
    {
        _currentHealth = maxHealth;
        _currentEssence = 0f;
        _dash = GetComponent<PlayerDash>();
        _pogo = GetComponent<PlayerPogo>();
        _knockback = GetComponent<PlayerKnockback>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        _input = GetComponent<PlayerInputHandler>();
    }

    void Update()
    {
        if (_input.HealTriggered)
        {
            TryHeal();
        }
    }

    void FixedUpdate()
    {
        if (_iFramesCounter > 0)
        {
            _iFramesCounter--;
            float alpha = (_iFramesCounter % 6 < 3) ? 0.2f : 1f;
            _sprite.color = new Color(1f, 1f, 1f, alpha);
        }
        else 
        {
            if (_sprite.color.a < 1f) _sprite.color = Color.white;

            if (_isTouchingEnemy && !_dash.IsDashing && !_dash.HasPostDashProtection && !_pogo.IsPlunging && !_pogo.HasPostPogoProtection)
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

            if (!_dash.IsDashing && !_dash.HasPostDashProtection && _iFramesCounter <= 0 && !_pogo.IsPlunging && !_pogo.HasPostPogoProtection)
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
        _iFramesCounter = iFramesDuration;
        _impulseSource.GenerateImpulse();

        if (_dash.IsDashing) _dash.StopDash();

        _knockback.ApplyKnockback(sourcePos, damageForce, damageFrames);
        VFXManager.Instance.PlayVFX(VFXType.HitGeneric, transform.position, Vector2.zero);
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
            //SFXManager.Instance.PlaySFX(SFXType.PlayerHeal);
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