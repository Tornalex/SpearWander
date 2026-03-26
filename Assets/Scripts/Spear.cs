using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] public Transform spriteTransform;

    [HideInInspector] public Rigidbody2D spearRb;
    [HideInInspector] public bool keepRotation = false;

    void Awake()
    {
        spearRb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (spearRb.linearVelocity.magnitude > 0.1f && !keepRotation)
        {
            float angle = Mathf.Atan2(spearRb.linearVelocity.y, spearRb.linearVelocity.x) * Mathf.Rad2Deg;
            spriteTransform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void LateUpdate()
    {
        if (!keepRotation)
            transform.rotation = Quaternion.identity;
    }

private void OnCollisionEnter2D(Collision2D collision)
{
    if (collision.transform.CompareTag("Enemy"))
    {
        int damage = 1;
        transform.parent = collision.transform;
        spearRb.simulated = false;
        Enemy enemy = collision.gameObject.GetComponent<Enemy>();
        if (enemy != null)
            enemy.TakeDamage(damage);

        return;
    }

    if (!collision.transform.CompareTag("Player") 
        && !collision.transform.CompareTag("Spear"))
    {    
        foreach (ContactPoint2D contact in collision.contacts)
        {
            Vector2 normal = contact.normal;

            // --- PAVIMENTO ---
            if (Vector2.Dot(normal, Vector2.up) > 0.5f)
            {
                spearRb.linearVelocity = Vector2.zero;
                spearRb.constraints = RigidbodyConstraints2D.FreezeAll;
                return;
            }

            // --- PARETE VERTICALE ---
            if (Mathf.Abs(normal.x) > 0.5f)
            {
                spearRb.linearVelocity = Vector2.zero;
                spearRb.constraints = RigidbodyConstraints2D.FreezeAll;

                keepRotation = true;
                transform.rotation = Quaternion.identity;
                spriteTransform.localRotation = Quaternion.identity;

                return;
            }
        }
    }
}
}