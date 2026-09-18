using UnityEngine;

namespace SpearWander.Boss
{
    public class BossProjectile : MonoBehaviour
    {
        private int damage;
        private Vector2 direction;
        private float lifeTime = 5f;
        private float timer = 0f;
        private Rigidbody2D rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void Initialize(int damage, Vector2 direction)
        {
            this.damage = damage;
            this.direction = direction.normalized;
            
            // Orient projectile
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer >= lifeTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(damage, transform.position, transform.position);
                Destroy(gameObject);
            }
            else if (other.gameObject.layer == LayerMask.NameToLayer("Ground") || 
                     other.gameObject.layer == LayerMask.NameToLayer("Wall"))
            {
                Destroy(gameObject);
            }
        }
    }
}