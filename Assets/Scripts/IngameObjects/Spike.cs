using UnityEngine;

public class Spike : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamage(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision);
    }

    private void TryDamage(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null && !playerHealth.IsInvulnerable)
            {
                playerHealth.TakeDamage(1, transform.position);
            }
        }
    }
}
