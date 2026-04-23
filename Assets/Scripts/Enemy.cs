using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] int enemyLife = 3;
    [SerializeField] float enemySpeed = 0f;

    [HideInInspector] public bool isDead = false;
    Vector2 enemyMovement = Vector2.right;
    SpriteRenderer enemySprite;
    Rigidbody2D enemyRb;

    void Awake()
    {
        enemyRb = GetComponent<Rigidbody2D>();
        enemySprite = GetComponent<SpriteRenderer>();
    }

    void FixedUpdate()
    {
        Vector2 enemyVelocity = new(enemyMovement.x * enemySpeed, enemyRb.linearVelocity.y);
        enemyRb.linearVelocity = enemyVelocity;
    }

    public void TakeDamage(int damage)
    {
        enemyLife -= damage;
        if (enemyLife <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Spear[] attachedSpears = GetComponentsInChildren<Spear>();

        foreach (Spear spear in attachedSpears)
        {
            spear.transform.SetParent(null);
            
            Rigidbody2D spearRb = spear.GetComponent<Rigidbody2D>();
            if (spearRb != null)
            {
                spearRb.bodyType = RigidbodyType2D.Dynamic;
                spearRb.gravityScale = 1; 
                spearRb.AddForce(new Vector2(Random.Range(-2, 2), 5), ForceMode2D.Impulse);
            }
            
            spear.currentState = Spear.SpearState.Embedded;
        }

        isDead = true;
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.transform.CompareTag("EnemyDirectionChanger"))
        {
            enemyMovement = -enemyMovement;
            enemySprite.flipX = !enemySprite.flipX;
        }
    }
}