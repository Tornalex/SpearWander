using UnityEngine;
using UnityEngine.Events;
using SpearWander.Abilities;

namespace SpearWander.Boss
{
    /// <summary>
    /// Weak point component for the boss.
    /// Handles visual feedback, hit detection, and damage events.
    /// Always visible and always hittable.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class WeakPoint : MonoBehaviour, IDamageable
    {
        [Header("Visual Settings")]
        [SerializeField] private Color baseColor = new Color(1f, 0.2f, 0.2f, 1f); // Bright red
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseAmount = 0.2f;
        [SerializeField] private float hitFlashDuration = 0.1f;

        [Header("Damage Settings")]
        public float damageMultiplier = 2f;
        [SerializeField] private bool invulnerable = false; // Should always be false per design

        [Header("References")]
        [SerializeField] private Boss boss; // Reference to parent boss

        [Header("Events")]
        public UnityEvent OnHit;
        public UnityEvent<float> OnDamageTaken; // damage amount

        // Components
        private SpriteRenderer spriteRenderer;
        private Collider2D col;
        private Material originalMaterial;
        private Color originalColor;

        #pragma warning disable 0414
        private float pulseTimer = 0f;
        private float hitFlashTimer = 0f;
        private bool isFlashing = false;
        #pragma warning restore 0414

        // Properties
        public bool IsInvulnerable => invulnerable;
        public float DamageMultiplier => damageMultiplier;
        public Collider2D Collider => col;
        public Boss Boss { get => boss; set => boss = value; }

        public event System.Action<float, Vector2, Vector2> OnDamageReceived; // damage, hitPoint, sourcePosition

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();

            // Ensure collider is a trigger
            col.isTrigger = true;

            // Cache original material and color
            originalMaterial = spriteRenderer.material;
            originalColor = spriteRenderer.color;

            // Ensure we're on the correct layer for enemy weak points
            gameObject.layer = LayerMask.NameToLayer("Enemy");

            // Force invulnerable to false - weak point should always be hittable per design
            invulnerable = false;

            // Auto-find boss if not assigned
            if (boss == null)
            {
                boss = GetComponentInParent<Boss>();
                if (boss == null)
                {
                    Debug.LogError("[WeakPoint] Boss component not found in parent hierarchy! Damage will not be forwarded.");
                }
                else
                {
                    Debug.Log("[WeakPoint] Successfully found Boss reference.");
                }
            }
        }

        private void Update()
        {
            // Pulse effect - always visible and pulsing
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount + (1f - pulseAmount);
            Color pulsedColor = originalColor * pulse;
            spriteRenderer.color = new Color(pulsedColor.r, pulsedColor.g, pulsedColor.b, originalColor.a);

            // Handle hit flash
            if (isFlashing)
            {
                hitFlashTimer -= Time.deltaTime;
                if (hitFlashTimer <= 0f)
                {
                    isFlashing = false;
                    spriteRenderer.material = originalMaterial;
                    spriteRenderer.color = originalColor;
                }
            }
        }

        /// <summary>
        /// Called when the weak point is hit by any damage source directly (not via IDamageable).
        /// Returns the actual damage dealt after multiplier.
        /// </summary>
        public float TakeDamageDirect(int baseDamage, Vector2 hitPoint, Vector2 damageSourcePosition)
        {
            if (invulnerable) return 0;

            int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);

            // Visual feedback
            FlashHit();

            // Invoke events
            OnHit?.Invoke();
            OnDamageTaken?.Invoke(finalDamage);
            OnDamageReceived?.Invoke(finalDamage, hitPoint, damageSourcePosition);

            return finalDamage;
        }

        private void FlashHit()
        {
            isFlashing = true;
            hitFlashTimer = hitFlashDuration;
            spriteRenderer.material = new Material(Shader.Find("Sprites/Default")); // Use default sprite shader for flash
            spriteRenderer.color = hitFlashColor;
        }

        /// <summary>
        /// Check if a world position hits this weak point.
        /// </summary>
        public bool ContainsPoint(Vector2 worldPoint)
        {
            return col.OverlapPoint(worldPoint);
        }

        /// <summary>
        /// Enable/disable the weak point (for phases, etc.)
        /// </summary>
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw weak point bounds
            Gizmos.color = Color.magenta;
            if (col != null)
            {
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }

        #region IDamageable Implementation

        /// <summary>
        /// Implements IDamageable interface for spear collision detection.
        /// Forwards damage to the boss with weak point multiplier.
        /// </summary>
        public void TakeDamage(int damage, Vector2 hitPoint, Vector2 damageSourcePosition)
        {
            // This is called by the spear when it hits the weak point collider
            // Forward to the internal method with multiplier
            TakeDamageInternal(damage, hitPoint, damageSourcePosition);
        }

        /// <summary>
        /// Internal method that applies damage with weak point multiplier.
        /// Called by both IDamageable interface and direct calls.
        /// Forwards damage to the boss.
        /// </summary>
        private void TakeDamageInternal(int baseDamage, Vector2 hitPoint, Vector2 damageSourcePosition)
        {
            if (invulnerable) return;

            int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);

            // Visual feedback
            FlashHit();

            // Invoke events
            OnHit?.Invoke();
            OnDamageTaken?.Invoke(finalDamage);
            OnDamageReceived?.Invoke(finalDamage, hitPoint, damageSourcePosition);

            // Forward damage to the boss
            if (boss != null)
            {
                boss.TakeDamage(finalDamage, hitPoint, damageSourcePosition);
            }
        }

        #endregion
    }
}