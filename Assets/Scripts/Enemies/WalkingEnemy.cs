using UnityEngine;

public class WalkingEnemy : BaseEnemy
{
    // speed preso da enemyData.speed
    private Vector2 movementDirection = Vector2.right;

    void FixedUpdate()
    {
        if (isDead || (knockback != null && knockback.IsKnockedBack)) return;
        
        rb.linearVelocity = new Vector2(movementDirection.x * enemyData.speed, rb.linearVelocity.y);
        animator.SetFloat("Speed", Mathf.Abs(movementDirection.x * enemyData.speed));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.transform.CompareTag("EnemyDirectionChanger")) 
        {
            FlipEnemy();
        }
    }

    private void FlipEnemy()
    {
        movementDirection = -movementDirection;
        spriteRenderer.flipX = !spriteRenderer.flipX;
    }
}