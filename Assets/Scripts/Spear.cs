using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    [SerializeField] float spearSpeed = 0f;
    [SerializeField] float spearHeightForce = 0f;

    private Vector2 spearVector;
    private Rigidbody2D rb;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spearVector = new(1, 1);
    }

    public void SpearThrow()
    {
        Vector2 spearVelocity = new(spearSpeed * spearVector.x, spearHeightForce * spearVector.y);
        rb.AddForce(spearVelocity, ForceMode2D.Impulse);
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.transform.tag == "Ground" || collision.transform.tag == "Wall")
        {
            Vector2 spearStop = new(0, 0);
            rb.velocity = spearStop;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }    
    }
}
