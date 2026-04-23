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
            List<Spear> spears = new();
            foreach (Transform child in transform)
            {
                Spear spear = child.GetComponent<Spear>();
                if (spear != null) spears.Add(spear);
            }
            foreach (Spear spear in spears)
            {;
                //spear.spearRb.linearVelocity = Vector2.zero;
                //spear.spearRb.simulated = true;
            }
            isDead = true;
            transform.DetachChildren();
            Destroy(gameObject);
        }
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