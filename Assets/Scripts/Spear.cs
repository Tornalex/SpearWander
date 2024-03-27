using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    public Rigidbody2D spearRb;
    void Awake()
    {
        spearRb = GetComponent<Rigidbody2D>();
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.transform.CompareTag("Wall")
            || collision.transform.CompareTag("Ground")
            || collision.transform.CompareTag("Enemy"))
        {
            spearRb.velocity = Vector2.zero;
            spearRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }
}
