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

    private readonly HashSet<ISpearImpactReceiver> _impactReceiversHitDuringThrow =
        new HashSet<ISpearImpactReceiver>();

    private void ClearHitLists()
    {
        _enemiesHitDuringThrow.Clear();
        _enemiesHitDuringReturn.Clear();
    }

    private void InitializeCombatState()
    {
        HasHitEnemy = false;
        ClearHitLists();

        _impactReceiversHitDuringThrow.Clear();
    }

    private bool TryRegisterImpactReceiver(
        ISpearImpactReceiver receiver)
    {
        return _impactReceiversHitDuringThrow.Add(receiver);
    }

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
    Vector2 normal,
    bool hasValidSurfaceNormal = true)
{
    if (currentState == SpearState.Embedded)
        return;

    if (other.CompareTag(directionChangerTag))
        return;

    if (other.TryGetComponent(
        out IDamageable damageable))
    {
        if (!ProcessDamageableHit(damageable, hitPoint))
            return;

        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff)
        {
            BounceOffEnemy();
        }
    }
    else if ((currentState == SpearState.Flying ||
              currentState == SpearState.BouncedOff) &&
             other.GetComponentInParent<ISpearImpactReceiver>()
                 is ISpearImpactReceiver impactReceiver)
    {
        if (!TryRegisterImpactReceiver(impactReceiver))
            return;

        if (!impactReceiver.TryReceiveSpearImpact(
            this,
            hitPoint,
            normal))
            return;

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
            IEmbeddable embeddable =
                other.GetComponentInParent<IEmbeddable>();

            Component embeddableComponent =
                embeddable as Component;

            StickToTarget(
                embeddableComponent != null
                    ? embeddableComponent.transform
                    : null,
                hitPoint,
                other,
                normal,
                hasValidSurfaceNormal
            );

            if (embeddableComponent != null)
                _currentEmbeddable = embeddable;

            CreateRope();

            if (embeddableComponent != null)
            {
                embeddable.OnSpearEmbedded(
                    this,
                    hitPoint,
                    normal
                );
            }
        }
    }
}

    private bool ProcessDamageableHit(
        IDamageable damageable,
        Vector2 hitPoint)
    {
        if (currentState == SpearState.Returning)
        {
            if (_enemiesHitDuringReturn.Contains(damageable))
                return false;

            _enemiesHitDuringReturn.Add(damageable);

            bool recallDamageApplied = damageable.TakeDamageFromPlayer(
                _recallDamage,
                hitPoint,
                transform.position
            );

            if (recallDamageApplied)
                HasHitEnemy = true;

            return false;
        }

        if (_enemiesHitDuringThrow.Contains(damageable))
            return false;

        _enemiesHitDuringThrow.Add(damageable);

        bool impactDamageApplied = damageable.TakeDamageFromPlayer(
            _impactDamage,
            hitPoint,
            transform.position
        );

        if (impactDamageApplied)
            HasHitEnemy = true;

        return true;
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
        ClearRopeAndIgnoredWall();

        NotifyEmbeddableRemoved();

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

        RestoreFloorEmbeddedPlayerCollisionOverride();
    }
}
