using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Unity.VisualScripting;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int iFramesDuration = 60;

    [Header("Damage Knockback")]
    [SerializeField] private Vector2 damageForce = new Vector2(15f, 10f);
    [SerializeField] private int damageFrames = 25;

    private int _currentHealth;
    private int _iFramesCounter;
    private bool _isTouchingEnemy;
    private Vector2 _lastEnemyPosition;
    
    private PlayerDash _dash;
    private PlayerPogo _pogo;
    private PlayerKnockback _knockback;
    private SpriteRenderer _sprite;
    private CinemachineImpulseSource _impulseSource;

    void Awake()
    {
        _currentHealth = maxHealth;
        _dash = GetComponent<PlayerDash>();
        _pogo = GetComponent<PlayerPogo>();
        _knockback = GetComponent<PlayerKnockback>();
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _impulseSource = GetComponent<CinemachineImpulseSource>();
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
}