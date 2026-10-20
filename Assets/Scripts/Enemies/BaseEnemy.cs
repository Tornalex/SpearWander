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
    [SerializeField, HideInInspector] private string stableIdentity;
    [SerializeField] public string roomName;

    [HideInInspector] public bool isDead = false;

    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected HitFlash hitFlash;
    protected Knockback knockback;
    protected Animator animator;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitFlash = GetComponent<HitFlash>();
        knockback = GetComponent<Knockback>();
        animator = GetComponent<Animator>();

        if (enemyData != null)
        {
            currentHealth = enemyData.maxHealth;
        }
        else
        {
            Debug.LogError($"[BaseEnemy] {name} has no EnemyData assigned.", this);
            currentHealth = 1;
        }
    }

        protected virtual void Start()
    {
        // Set the room used for persistent death tracking.
        if (GameSceneManager.Instance != null)
        {
            roomName = GameSceneManager.Instance.CurrentRoom;
        }
        else
        {
            roomName = "Unknown";
        }

        // Check if this enemy should be dead.
        // Enemies without a stable identity cannot be tracked individually:
        // treating an empty identity as a key would make every untracked
        // enemy in the room disappear after a single kill.
        if (!string.IsNullOrEmpty(stableIdentity) &&
            DeadEnemyTracker.IsDead(roomName, stableIdentity))
        {
            Destroy(gameObject);
        }
    }

    public virtual bool TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
    {
        if (isDead) return false;

        currentHealth -= damage;

        if (enemyData != null)
        {
            knockback?.ApplyKnockback(damageSourcePosition, enemyData.knockbackForce, enemyData.knockbackDuration);
        }

        SFXManager.Instance?.PlaySFX(SFXType.EnemyPierced);
        VFXManager.Instance?.PlayVFX(VFXType.HitDash, hitPoint, (hitPoint - (Vector2)transform.position).normalized);

        if (animator != null) animator.SetTrigger("TookHit");

        if (hitFlash != null) hitFlash.Flash();

        if (currentHealth <= 0) Die();

        return damage > 0;
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
        Debug.Log($"[BaseEnemy.Die] {name} - roomName='{roomName}', stableIdentity='{stableIdentity}'");

        // Only persistent identities can be restored after a room reload.
        if (!string.IsNullOrEmpty(stableIdentity))
            DeadEnemyTracker.MarkDead(roomName, stableIdentity);

        Destroy(gameObject);
    }
}
