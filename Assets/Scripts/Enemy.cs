using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] private int enemyLife = 3;
    [SerializeField] private float enemySpeed = 2f;

    [HideInInspector] public bool isDead = false;
    private Vector2 enemyMovement = Vector2.right;
    private SpriteRenderer enemySprite;
    private Rigidbody2D enemyRb;
    private HitFlash _hitFlash;

    void Awake()
    {
        enemyRb = GetComponent<Rigidbody2D>();
        enemySprite = GetComponent<SpriteRenderer>();
        _hitFlash = GetComponent<HitFlash>();
    }

    void FixedUpdate()
    {
        if (!isDead)
        {
            Vector2 enemyVelocity = new Vector2(enemyMovement.x * enemySpeed, enemyRb.linearVelocity.y);
            enemyRb.linearVelocity = enemyVelocity;
        }
    }

    public void TakeDamage(int damage)
    {
        enemyLife -= damage;
        if (_hitFlash != null)
        {
            FlipEnemy();
            _hitFlash.Flash();
        }
        if (enemyLife <= 0 && !isDead)
        {
            Die();
        }
    }

private void Die()
{
    isDead = true;
    Spear[] attachedSpears = GetComponentsInChildren<Spear>();

    foreach (Spear spear in attachedSpears)
    {
        spear.OnEnemyDeath();
    }

    Destroy(gameObject);
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
