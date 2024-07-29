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
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("EnemyDirectionChanger"))
        {
            enemyMovement = -enemyMovement;
            enemySprite.flipX = !enemySprite.flipX;

        }
        if (collision.CompareTag("Spear"))
        {
            enemyLife--;
            if (enemyLife < 1)
            {
                /*for (int i = 1; i <= transform.childCount; i++)
                {
                    Instantiate(spearObject, transform.position, Quaternion.identity);
                }*/
                isDead = true;
                Destroy(gameObject);
            }
        }
    }
}
