using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    [Header("Spear Stats")]
    [SerializeField] float headWeight = 0f;

    [Header("Cmponents")]
    [SerializeField] GameObject head;
    
    [HideInInspector] public Rigidbody2D spearRb;
    Enemy enemy;

    void Awake()
    {
        spearRb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (enemy != null && enemy.isDead)
        {
            spearRb.isKinematic = false;
        }
    }
    
    private void FixedUpdate()
    {
        spearRb.AddForceAtPosition(-transform.up * headWeight, head.transform.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.transform.tag == "Wall"
           || collision.transform.tag == "Ground")
        {
            spearRb.velocity = Vector2.zero;
            spearRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        if (collision.transform.tag == "Enemy")
        {
            enemy = collision.gameObject.GetComponent<Enemy>();
            spearRb.isKinematic = true;
            transform.parent = collision.transform;   
        }
    }
}
