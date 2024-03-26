using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] int enemyLife = 3;
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
            enemyMovement = -enemyMovement;
        }
        if(collision.CompareTag("Spear"))
        {
            enemyLife --;
            Debug.Log("nemico colpito");
        }
        if (enemyLife < 1)
        {
            Destroy(gameObject);
            Debug.Log("nemico ucciso");
        }
    }
}
