using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] float enemySpeed = 0f;
    Vector2 enemyMovement = Vector2.right;
    Rigidbody2D enemyRb;
    void Awake()
    {
        enemyRb = GetComponent<Rigidbody2D>();
    }
    void FixedUpdate()
    {
        Vector2 enemyVelocity = new(enemyMovement.x * enemySpeed, enemyRb.velocity.y);
        enemyRb.velocity = enemyVelocity;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("EnemyDirectionChanger"))
        {
            enemyRb.velocity = -enemyRb.velocity;
            Debug.Log("Flippato nemico!");
        }
    }
}
