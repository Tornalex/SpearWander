using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    [Header("Spear Stats")]
    [SerializeField] int spearSpeed;

    [Header("Components")]
    [SerializeField] Vector3 mousePos;
    
    [HideInInspector] public Rigidbody2D spearRb;
    Enemy enemy;

    void Awake()
    {
        spearRb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (enemy == null)
        {
            spearRb.isKinematic = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.transform.tag == "Ground")
        {
            spearRb.velocity = Vector2.zero;
            spearRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        if (collision.transform.tag == "Enemy")
        {
            transform.parent = collision.transform;
            enemy = collision.gameObject.GetComponent<Enemy>();
            spearRb.isKinematic = true;
            spearRb.velocity = Vector3.zero;
        }
    }
}
