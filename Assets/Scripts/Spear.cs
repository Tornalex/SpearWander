using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    [SerializeField] float headWeight = 0f;
    [HideInInspector] public Rigidbody2D spearRb;
    GameObject head;
    void Awake()
    {
        spearRb = GetComponent<Rigidbody2D>();
        head = FindObjectOfType<GameObject>();
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
            spearRb.isKinematic = true;
            transform.parent = collision.transform;
        }
    }
}
