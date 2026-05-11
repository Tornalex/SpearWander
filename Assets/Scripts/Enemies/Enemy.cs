using UnityEngine;

public class Enemy : MonoBehaviour, IDamageable
{
    [Header("Enemy Stats")]
    [SerializeField] private int enemyLife = 3;
    [SerializeField] private float enemySpeed = 2f;

    
    [Header("Knockback Settings")]
    [SerializeField] private Vector2 knockbackForce = new Vector2(10f, 5f);
    [SerializeField] private int knockbackFrames = 12;

    [HideInInspector] public bool isDead = false;
    private Vector2 enemyMovement = Vector2.right;
    private SpriteRenderer enemySprite;
    private Rigidbody2D enemyRb;
    private HitFlash _hitFlash;
    private PlayerKnockback _knockback;

    void Awake()
    {
        enemyRb = GetComponent<Rigidbody2D>();
        enemySprite = GetComponent<SpriteRenderer>();
        _hitFlash = GetComponent<HitFlash>();
        _knockback = GetComponent<PlayerKnockback>();
    }

    void FixedUpdate()
    {
        if (isDead || _knockback.IsKnockedBack) return;
        
        Vector2 enemyVelocity = new Vector2(enemyMovement.x * enemySpeed, enemyRb.linearVelocity.y);
        enemyRb.linearVelocity = enemyVelocity;
    }

    public void TakeDamage(int damage, Vector2 hitPoint)
    {
        enemyLife -= damage;

        GameObject player = GameObject.FindWithTag("Player");
        
        if (player != null && _knockback != null)
        {
            _knockback.ApplyKnockback(player.transform.position, knockbackForce, knockbackFrames);
        }
        SFXManager.Instance.PlaySFX(SFXType.EnemyPierced);

        Vector2 bounceDirection = (hitPoint - (Vector2)transform.position).normalized;
        VFXManager.Instance.PlayVFX(VFXType.HitDash, hitPoint, bounceDirection);
        if (_hitFlash != null)
        {
            _hitFlash.Flash();
        }
        if (enemyLife <= 0 && !isDead)
        {
            Die();
        }
    }

private void Die()
{
    Spear[] attachedSpears = GetComponentsInChildren<Spear>();

    foreach (Spear spear in attachedSpears)
    {
        spear.OnEnemyDeath();
    }

    isDead = true;
    Destroy(gameObject, 0.1f);
}

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.transform.CompareTag("EnemyDirectionChanger"))
        {
            FlipEnemy();
        }
    }
    private void FlipEnemy()
    {
        enemyMovement = -enemyMovement;
        enemySprite.flipX = !enemySprite.flipX;
    }
}
