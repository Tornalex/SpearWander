using UnityEngine;

// Questa classe è "abstract" perché non la assegnerai mai direttamente a un nemico.
// È un "modello" da cui gli altri nemici prenderanno le funzioni.
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public abstract class BaseEnemy : MonoBehaviour, IDamageable, IBounceable
{
    [Header("Base Stats")]
    [SerializeField] protected int maxHealth = 3;
    protected int currentHealth;

    [Header("Knockback Settings")]
    [SerializeField] protected Vector2 knockbackForce = new Vector2(10f, 5f);
    [SerializeField] protected float knockbackDuration = 0.2f;

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
        currentHealth = maxHealth;
    }

    public virtual void TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (knockback != null)
        {
            knockback.ApplyKnockback(damageSourcePosition, knockbackForce, knockbackDuration);
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
        SpearV2[] attachedSpearsV2 = GetComponentsInChildren<SpearV2>();
        foreach (SpearV2 spear in attachedSpearsV2) spear.OnEnemyDeath();

        isDead = true;
        Destroy(gameObject);
    }
}