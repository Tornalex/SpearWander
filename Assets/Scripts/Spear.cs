using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    [SerializeField] float spearSpeed = 0f;
    [SerializeField] float spearHeightForce = 0f;

    private Vector2 spearVector;
    private Rigidbody2D spearRb;
    void Awake()
    {
        spearRb = GetComponent<Rigidbody2D>();
        spearVector = new(1, 1);
    }

    public void SpearThrow()
    {
        Vector2 spearVelocity = new(spearSpeed * spearVector.x, spearHeightForce * spearVector.y);
        spearRb.AddForce(spearVelocity, ForceMode2D.Impulse);
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.transform.CompareTag("Wall")
            || collision.transform.CompareTag("Ground"))
        {
            Vector2 spearStop = new(0, 0);
            spearRb.velocity = spearStop;
            spearRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }
}
