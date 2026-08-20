using UnityEngine;

// Questa classe è "abstract" perché non la assegnerai mai direttamente a un nemico.
// È un "modello" da cui gli altri nemici prenderanno le funzioni.
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public abstract class BaseEnemy : MonoBehaviour, IDamageable, IBounceable
{
    [SerializeField] protected EnemyData enemyData;
    protected int currentHealth;

    [HideInInspector] public bool isDead = false;

    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected HitFlash hitFlash;
    protected PlayerKnockback knockback;
    protected Animator animator;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitFlash = GetComponent<HitFlash>();
        knockback = GetComponent<PlayerKnockback>();
        animator = GetComponent<Animator>();
        currentHealth = enemyData.maxHealth;
    }

    public virtual void TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (knockback != null)
        {
            knockback.ApplyKnockback(damageSourcePosition, enemyData.knockbackForce, enemyData.knockbackDuration);
        }

        SFXManager.Instance.PlaySFX(SFXType.EnemyPierced);
        VFXManager.Instance.PlayVFX(VFXType.HitDash, hitPoint, (hitPoint - (Vector2)transform.position).normalized);

        animator.SetTrigger("TookHit");

        if (hitFlash != null) hitFlash.Flash();

        if (currentHealth <= 0) Die();
    }

    public virtual float GetBounceMultiplier() => 1f;

    public virtual void OnPogoBounce()
    {
        if (hitFlash != null) hitFlash.Flash();
    }

    protected virtual void Die()
    {
        Spear[] attachedSpears = GetComponentsInChildren<Spear>();
        foreach (Spear spear in attachedSpears) spear.OnEnemyDeath();

        isDead = true;
        Destroy(gameObject);
    }
}