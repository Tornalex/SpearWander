using UnityEngine;
using SpearWander.Abilities;
using System.Collections;

namespace SpearWander.Boss
{
    [RequireComponent(typeof(Rigidbody2D), typeof(SpriteRenderer), typeof(Animator))]
    public class Boss : MonoBehaviour, IDamageable
    {
        [Header("Configuration")]
        [SerializeField] private BossData bossData;

        [Header("References")]
        [SerializeField] private WeakPoint weakPoint;
        [SerializeField] private Transform graphicsTransform; // The visual part that rotates
        [SerializeField] private Transform meleeAttackPoint;
        [SerializeField] private Transform rangedAttackPoint;
        [SerializeField] private Transform antiAirAttackPoint;

        [Header("Debug")]
#pragma warning disable 0414
        [SerializeField] private bool debugMode = false;
#pragma warning restore 0414

        // Components
        protected Rigidbody2D rb;
        protected SpriteRenderer spriteRenderer;
        protected Animator animator;
        protected HitFlash hitFlash;

        // State
        protected int currentHealth;
        protected Transform playerTransform;
        protected bool isDead = false;

        // Facing direction
        protected bool facingRight = true;

        // Attack state
        protected bool isAttacking = false;
        protected float meleeAttackCooldownTimer = 0f;
        protected float rangedAttackCooldownTimer = 0f;
        protected float antiAirCooldownTimer = 0f;

        // Stun/knockback
        protected bool isStunned = false;
        protected float stunTimer = 0f;

        // Components
        protected HitFlash hitFlashComponent;
        protected PlayerKnockback knockbackComponent;

        public bool IsDead => isDead;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => bossData?.maxHealth ?? 10;
        public WeakPoint WeakPoint => weakPoint;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            hitFlashComponent = GetComponent<HitFlash>();
            knockbackComponent = GetComponent<PlayerKnockback>();

            if (bossData != null)
            {
                currentHealth = bossData.maxHealth;
            }
            else
            {
                Debug.LogError("[Boss] BossData not assigned!");
                currentHealth = 10;
            }

            // Sync weak point damage multiplier from BossData
            if (weakPoint != null && bossData != null)
            {
                weakPoint.damageMultiplier = bossData.weakPointDamageMultiplier;
            }

            // Subscribe to weak point damage event
            if (weakPoint != null)
            {
                weakPoint.OnDamageReceived += OnWeakPointDamaged;
            }
        }

        protected virtual void Start()
        {
            // Find player
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
            else
            {
                Debug.LogError("[Boss] Player with tag 'Player' not found! Boss will not rotate or move towards player.");
            }

            // Ensure weak point references boss
            if (weakPoint != null && weakPoint.Boss == null)
            {
                weakPoint.Boss = this;
            }
        }

        protected virtual void OnDestroy()
        {
            if (weakPoint != null)
            {
                weakPoint.OnDamageReceived -= OnWeakPointDamaged;
            }
        }

        protected virtual void FixedUpdate()
        {
            if (isDead) return;

            HandleStun();

            if (isStunned || isAttacking) return;

            HandleRotation();
            HandleMovement();
            HandleAttacks();
        }

        protected virtual void Update()
        {
            if (isDead) return;

            HandleAttackCooldowns();
        }

        #region Rotation & Movement

        protected virtual void HandleRotation()
        {
            if (playerTransform == null) return;

            float directionToPlayer = playerTransform.position.x - transform.position.x;
            bool shouldFaceRight = directionToPlayer > 0;

            if (shouldFaceRight != facingRight)
            {
                RotateTowardsPlayer(shouldFaceRight);
            }
        }

        protected virtual void RotateTowardsPlayer(bool faceRight)
        {
            facingRight = faceRight;

            if (graphicsTransform != null)
            {
                // Smooth rotation
                Vector3 targetScale = graphicsTransform.localScale;
                targetScale.x = Mathf.Abs(targetScale.x) * (facingRight ? 1 : -1);
                graphicsTransform.localScale = targetScale;

                // Update weak point position after rotation
                UpdateWeakPointPosition();
            }
        }

        protected virtual void UpdateWeakPointPosition()
        {
            if (weakPoint == null || weakPoint.transform == null || graphicsTransform == null) return;

            // Weak point should be on the back side (opposite to facing direction)
            float backOffset = facingRight ? -1f : 1f;
            Vector3 localPos = weakPoint.transform.localPosition;
            localPos.x = Mathf.Abs(localPos.x) * backOffset;
            weakPoint.transform.localPosition = localPos;

            // Also flip the weak point sprite if needed
            if (weakPoint.TryGetComponent(out SpriteRenderer sr))
            {
                sr.flipX = !facingRight;
            }
        }

        protected virtual void HandleMovement()
        {
            if (playerTransform == null || bossData == null) return;

            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            // Only move towards player if within chase distance
            if (distanceToPlayer > bossData.chaseDistance)
            {
                // Move towards player
                Vector2 direction = (playerTransform.position - transform.position).normalized;
                rb.linearVelocity = new Vector2(direction.x * bossData.moveSpeed, rb.linearVelocity.y);
            }
            else
            {
                // Stop horizontal movement when close enough
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }

        #endregion

        #region Attacks

        protected virtual void HandleAttacks()
        {
            if (playerTransform == null || bossData == null) return;

            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
            bool playerAbove = playerTransform.position.y > transform.position.y + 1f;

            // Anti-air attack (priority when player is above)
            if (playerAbove && distanceToPlayer <= bossData.antiAirRange && antiAirCooldownTimer <= 0f)
            {
                StartAntiAirAttack();
                return;
            }

            // Melee attack
            if (distanceToPlayer <= bossData.meleeAttackRange && meleeAttackCooldownTimer <= 0f)
            {
                StartMeleeAttack();
                return;
            }

            // Ranged attack
            if (distanceToPlayer <= bossData.rangedAttackRange && rangedAttackCooldownTimer <= 0f)
            {
                StartRangedAttack();
                return;
            }
        }

        protected virtual void HandleAttackCooldowns()
        {
            if (meleeAttackCooldownTimer > 0f) meleeAttackCooldownTimer -= Time.deltaTime;
            if (rangedAttackCooldownTimer > 0f) rangedAttackCooldownTimer -= Time.deltaTime;
            if (antiAirCooldownTimer > 0f) antiAirCooldownTimer -= Time.deltaTime;
        }

        protected virtual void StartMeleeAttack()
        {
            if (bossData == null) return;

            isAttacking = true;
            meleeAttackCooldownTimer = bossData.meleeAttackCooldown;

            if (animator != null)
            {
                animator.SetTrigger("MeleeAttack");
            }

            // Deal damage after a short delay (animation event would be better)
            StartCoroutine(DealMeleeDamageAfterDelay(0.3f));
        }

        private IEnumerator DealMeleeDamageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (meleeAttackPoint != null)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(meleeAttackPoint.position, 0.5f);
                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.TakeDamage(bossData.meleeDamage, meleeAttackPoint.position, transform.position);
                    }
                }
            }

            isAttacking = false;
        }

        protected virtual void StartRangedAttack()
        {
            if (bossData == null || bossData.projectilePrefab == null || rangedAttackPoint == null) return;

            isAttacking = true;
            rangedAttackCooldownTimer = bossData.rangedAttackCooldown;

            if (animator != null)
            {
                animator.SetTrigger("RangedAttack");
            }

            // Fire projectile
            Vector2 direction = (playerTransform.position - rangedAttackPoint.position).normalized;
            GameObject projectile = Instantiate(bossData.projectilePrefab, rangedAttackPoint.position, Quaternion.identity);

            Rigidbody2D projRb = projectile.GetComponent<Rigidbody2D>();
            if (projRb != null)
            {
                projRb.linearVelocity = direction * bossData.projectileSpeed;
            }

            // Add damage component to projectile
            var damageDealer = projectile.GetComponent<BossProjectile>();
            if (damageDealer == null)
            {
                damageDealer = projectile.AddComponent<BossProjectile>();
            }
            damageDealer.Initialize(bossData.rangedDamage, direction);

            isAttacking = false;
        }

        protected virtual void StartAntiAirAttack()
        {
            if (bossData == null || antiAirAttackPoint == null) return;

            isAttacking = true;
            antiAirCooldownTimer = bossData.antiAirCooldown;

            if (animator != null)
            {
                animator.SetTrigger("AntiAirAttack");
            }

            // Create vertical hitbox above boss
            StartCoroutine(DealAntiAirDamageAfterDelay(0.2f));
        }

        private IEnumerator DealAntiAirDamageAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (antiAirAttackPoint != null)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(antiAirAttackPoint.position, bossData.antiAirRange);
                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.TakeDamage(bossData.antiAirDamage, antiAirAttackPoint.position, transform.position);

                        // Apply vertical knockback
                        if (damageable is MonoBehaviour mb && mb.TryGetComponent(out Rigidbody2D rb))
                        {
                            rb.linearVelocity = new Vector2(rb.linearVelocity.x, bossData.antiAirVerticalForce);
                        }
                    }
                }
            }

            isAttacking = false;
        }

        #endregion

        #region Damage & Death

        public virtual void TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
        {
            if (isDead) return;

            // Check if hit was on weak point
            bool hitWeakPoint = false;
            if (weakPoint != null && weakPoint.Collider != null)
            {
                hitWeakPoint = weakPoint.Collider.OverlapPoint(hitPoint);
            }

            // Apply damage (weak point multiplier is already applied by WeakPoint component if hit there)
            int healthBefore = currentHealth;
            currentHealth -= damage;

            // Notify weak point for visual feedback
            if (hitWeakPoint && weakPoint != null)
            {
                weakPoint.TakeDamageDirect(damage, hitPoint, damageSourcePosition);
            }

            if (knockbackComponent != null && !isDead)
            {
                knockbackComponent.ApplyKnockback(damageSourcePosition, bossData.knockbackForce, bossData.knockbackDuration);
            }

            SFXManager.Instance?.PlaySFX(bossData.hitSfxType);
            VFXManager.Instance?.PlayVFX(bossData.hitVfxType, hitPoint, (hitPoint - (Vector2)transform.position).normalized);

            if (animator != null) animator.SetTrigger("TookHit");
            if (hitFlashComponent != null) hitFlashComponent.Flash();

            // Apply knockback
            if (knockbackComponent != null)
            {
                knockbackComponent.ApplyKnockback(damageSourcePosition, bossData.knockbackForce, bossData.knockbackDuration);
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        protected virtual void Die()
        {
            isDead = true;

            SFXManager.Instance?.PlaySFX(bossData.deathSfxType);
            VFXManager.Instance?.PlayVFX(bossData.deathVfxType, transform.position, Vector2.zero);

            if (animator != null) animator.SetTrigger("Death");

            // Disable colliders
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            foreach (var col in colliders) col.enabled = false;

            // Disable physics
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;

            // Disable scripts
            enabled = false;
        }

        #endregion

        #region Stun

        protected virtual void HandleStun()
        {
            if (stunTimer > 0f)
            {
                stunTimer -= Time.fixedDeltaTime;
                if (stunTimer <= 0f)
                {
                    isStunned = false;
                }
            }
        }

        public void ApplyStun(float duration)
        {
            isStunned = true;
            stunTimer = duration;
            rb.linearVelocity = Vector2.zero;
        }

        #endregion

        #region Weak Point Events

        private void OnWeakPointDamaged(float damage, Vector2 hitPoint, Vector2 sourcePosition)
        {
            // Additional visual/audio feedback when weak point is hit
            if (hitFlashComponent != null) hitFlashComponent.Flash();
            if (animator != null) animator.SetTrigger("WeakPointHit");

            // Camera shake
            if (CameraShakeManager.Instance != null)
            {
                CameraShakeManager.Instance.Shake(CameraShakeType.Medium);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (bossData == null) return;

            // Melee range
            Gizmos.color = Color.red;
            if (meleeAttackPoint != null)
            {
                Gizmos.DrawWireSphere(meleeAttackPoint.position, 0.5f);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, bossData.meleeAttackRange);
            }

            // Ranged range
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, bossData.rangedAttackRange);

            // Anti-air range
            Gizmos.color = Color.yellow;
            if (antiAirAttackPoint != null)
            {
                Gizmos.DrawWireSphere(antiAirAttackPoint.position, bossData.antiAirRange);
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, bossData.antiAirRange);
            }

            // Chase distance
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, bossData.chaseDistance);

            // Weak point
            if (weakPoint != null && weakPoint.transform != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(weakPoint.transform.position, 0.5f);
            }
        }

        #endregion
    }
}