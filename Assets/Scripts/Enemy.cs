using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] int enemyLife = 3;
    [SerializeField] float enemySpeed = 0f;

    [Header("Components")]
    [SerializeField] GameObject spearObject;
    
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
        Vector2 enemyVelocity = new(enemyMovement.x * enemySpeed, enemyRb.velocity.y);
        enemyRb.velocity = enemyVelocity;
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.transform.tag == "EnemyDirectionChanger")
        {
            enemyMovement = -enemyMovement;
            enemySprite.flipX = !enemySprite.flipX;

        }
        if (collision.transform.tag == "Spear")
        {
            enemyLife--;
            if (enemyLife == 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    transform.DetachChildren();
                }
                isDead = true;
                Destroy(gameObject);
            }
        }
    }
}
