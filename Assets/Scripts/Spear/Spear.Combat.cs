using UnityEngine;
using System.Collections.Generic;

public partial class Spear : MonoBehaviour
{
    [Header("Enemy Bounce")]
    [SerializeField] private float enemyBounceSpeed = 15f;
    [SerializeField] private float bounceAngle = 90f;

    private int _impactDamage;
    private int _recallDamage;

    private List<IDamageable> _enemiesHitDuringReturn =
        new List<IDamageable>();

    private List<IDamageable> _enemiesHitDuringThrow =
        new List<IDamageable>();

    public void SetDamage(
        int impact,
        int recall)
    {
        _impactDamage = impact;
        _recallDamage = recall;
    }

    public int RecallDamage =>
        _recallDamage;

private void ProcessHit(
    Collider2D other,
    Vector2 hitPoint,
    Vector2 normal)
{
    if (currentState == SpearState.Embedded)
        return;

    if (other.CompareTag(directionChangerTag))
        return;

    if (other.TryGetComponent(
        out IDamageable damageable))
    {
        if (currentState == SpearState.Returning)
        {
            if (_enemiesHitDuringReturn.Contains(damageable))
                return;

            _enemiesHitDuringReturn.Add(damageable);

            HasHitEnemy = true;

            damageable.TakeDamage(
                _recallDamage,
                hitPoint,
                transform.position
            );

            return;
        }

        if (_enemiesHitDuringThrow.Contains(damageable))
            return;

        _enemiesHitDuringThrow.Add(damageable);

        HasHitEnemy = true;

        damageable.TakeDamage(
            _impactDamage,
            hitPoint,
            transform.position
        );

        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff)
        {
            BounceOffEnemy();
        }
    }
    else
    {
        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff ||
            currentState == SpearState.Dropped)
        {
            StickToTarget(
                null,
                hitPoint,
                other,
                normal
            );

            CreateRope();
        }
    }
}

    private void BounceOffEnemy()
    {
        // bounceAngle represents the TOTAL width of the bounce cone.
        // Example:
        // 90 degrees = -45 to +45 degrees from straight up.

        float halfAngle =
            bounceAngle * 0.5f;

        float randomAngle =
            UnityEngine.Random.Range(
                -halfAngle,
                halfAngle
            );

        float angleFromUp =
            randomAngle * Mathf.Deg2Rad;

        Vector2 randomDirection =
            new Vector2(
                Mathf.Sin(angleFromUp),
                Mathf.Cos(angleFromUp)
            ).normalized;

        _rb.bodyType =
            RigidbodyType2D.Dynamic;

        _rb.linearVelocity =
            randomDirection *
            enemyBounceSpeed;

        currentState =
            SpearState.BouncedOff;

        _collider.isTrigger =
            true;

        transform.right =
            randomDirection;
    }

    public void OnEnemyDeath()
    {
        DestroyRope();

        ResetIgnoredWall();

        transform.SetParent(null);

        currentState =
            SpearState.Dropped;

        _rb.bodyType =
            RigidbodyType2D.Dynamic;

        _rb.gravityScale =
            1.5f;

        _rb.linearVelocity =
            Vector2.zero;

        _rb.AddForce(
            new Vector2(
                UnityEngine.Random.Range(
                    -6f,
                    6f
                ),
                10f
            ),
            ForceMode2D.Impulse
        );

        _collider.isTrigger =
            false;
    }
}