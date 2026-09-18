using UnityEngine;
using SpearWander.Abilities;

// Questa classe è "abstract" perché non la assegnerai mai direttamente a un nemico.
// È un "modello" da cui gli altri nemici prenderanno le funzioni.
[RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer))]
public abstract class BaseEnemy : MonoBehaviour, IDamageable, IBounceable
{
    [SerializeField] protected EnemyData enemyData;
    protected int currentHealth;

    [Header("Persistent Death Tracking (Auto-Assigned)")]
    [SerializeField] public int enemyIndex;
    [SerializeField] public string roomName;

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

        protected virtual void Start()
    {
        // Auto-register room and get index
        if (GameSceneManager.Instance != null)
        {
            roomName = GameSceneManager.Instance.CurrentRoom;
            DeadEnemyTracker.RegisterRoom(roomName);
        }
        else
        {
            roomName = "Unknown";
        }

        // Check if this enemy should be dead
        if (DeadEnemyTracker.IsDead(roomName, enemyIndex))
        {
            Destroy(gameObject);
        }
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

        if (animator != null) animator.SetTrigger("TookHit");

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
        Debug.Log($"[BaseEnemy.Die] {name} - roomName='{roomName}', enemyIndex={enemyIndex}");
        DeadEnemyTracker.MarkDead(roomName, enemyIndex);
        Destroy(gameObject);
    }
}