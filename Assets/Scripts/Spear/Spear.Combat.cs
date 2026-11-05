using UnityEngine;
using System.Collections.Generic;

public partial class Spear : MonoBehaviour
{
    public enum HitClassificationType
    {
        None,
        Damage,
        ImpactReceiverConditional,
        Embed
    }

    public enum HitOutcome
    {
        None,
        Bounce,
        BounceIfAccepted,
        Embed
    }

    public readonly struct HitClassification
    {
        // Outcome for a direct route, or the accepted branch of a conditional receiver.
        public readonly HitClassificationType Type;
        public readonly HitOutcome Outcome;

        // Possible fallback only when a conditional receiver rejects the hit.
        public readonly HitOutcome RejectedReceiverOutcome;

        public HitClassification(
            HitClassificationType type,
            HitOutcome outcome,
            HitOutcome rejectedReceiverOutcome = HitOutcome.None)
        {
            Type = type;
            Outcome = outcome;
            RejectedReceiverOutcome = rejectedReceiverOutcome;
        }
    }

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
    if (other == null)
        return;

    if (currentState == SpearState.Embedded)
        return;

    // Query-based hit detection (tunneling sweep, tip raycast) does not
    // respect Physics2D.IgnoreCollision, so ignore pairs must be checked
    // here as well.
    if (IsCollisionIgnored(other))
        return;

    if (other.CompareTag(directionChangerTag))
        return;

    IDamageable damageable = null;
    ISpearImpactReceiver impactReceiver = null;
    bool damageableAlreadyHit = false;
    bool receiverAlreadyRegistered = false;

    if (other.TryGetComponent(
        out damageable))
    {
        damageableAlreadyHit =
            currentState == SpearState.Returning
                ? _enemiesHitDuringReturn.Contains(damageable)
                : _enemiesHitDuringThrow.Contains(damageable);
    }
    else if (currentState == SpearState.Flying ||
             currentState == SpearState.BouncedOff)
    {
        impactReceiver =
            other.GetComponentInParent<ISpearImpactReceiver>();

        receiverAlreadyRegistered =
            impactReceiver != null &&
            _impactReceiversHitDuringThrow.Contains(impactReceiver);
    }

    HitClassification classification = ClassifyHitCore(
        other,
        currentState,
        damageable,
        impactReceiver,
        damageableAlreadyHit,
        receiverAlreadyRegistered);

    if (classification.Type == HitClassificationType.None)
        return;

    if (classification.Type == HitClassificationType.Damage)
    {
        if (!ProcessDamageableHit(damageable, hitPoint))
            return;

        // Keep the existing post-damage state check: damage callbacks may
        // change gameplay state before the spear decides whether to bounce.
        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff)
        {
            BounceOffEnemy();
        }

        return;
    }

    if (classification.Type == HitClassificationType.ImpactReceiverConditional)
    {
        if (!TryRegisterImpactReceiver(impactReceiver))
            return;

        if (impactReceiver.TryReceiveSpearImpact(
            this,
            hitPoint,
            normal))
        {
            if (currentState == SpearState.Flying ||
                currentState == SpearState.BouncedOff)
            {
                BounceOffEnemy();
            }

            return;
        }

        // Reclassify the rejected receiver path against post-callback state.
        // This preserves the completed-counter/static-body fallback without
        // invoking receiver code during classification.
        classification = ClassifyHitCore(
            other,
            currentState,
            damageable,
            impactReceiver,
            damageableAlreadyHit,
            receiverAlreadyRegistered: true,
            receiverRejected: true);

        if (classification.Type != HitClassificationType.Embed)
            return;
    }

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

    /// <summary>
    /// Classifies the interaction route for a collider without invoking any
    /// gameplay callbacks or changing spear or target state.
    /// </summary>
    public HitClassification ClassifyHit(
        Collider2D other,
        SpearState state,
        bool damageableAlreadyHit,
        bool receiverAlreadyRegistered)
    {
        if (other == null)
        {
            return new HitClassification(
                HitClassificationType.None,
                HitOutcome.None);
        }

        IDamageable damageable =
            other.GetComponent<IDamageable>();

        ISpearImpactReceiver impactReceiver = null;
        if (damageable == null &&
            (state == SpearState.Flying ||
             state == SpearState.BouncedOff))
        {
            impactReceiver =
                other.GetComponentInParent<ISpearImpactReceiver>();
        }

        return ClassifyHitCore(
            other,
            state,
            damageable,
            impactReceiver,
            damageableAlreadyHit,
            receiverAlreadyRegistered);
    }

    private HitClassification ClassifyHitCore(
        Collider2D other,
        SpearState state,
        IDamageable damageable,
        ISpearImpactReceiver impactReceiver,
        bool damageableAlreadyHit,
        bool receiverAlreadyRegistered,
        bool receiverRejected = false)
    {
        if (receiverRejected)
        {
            // The original receiver callback has already rejected this hit.
            // Only a completed counter on a currently static attached body
            // falls through to embedding.
            if (ClassifyRejectedReceiverOutcome(
                    other,
                    state,
                    impactReceiver) == HitOutcome.Embed)
            {
                return new HitClassification(
                    HitClassificationType.Embed,
                    HitOutcome.Embed);
            }

            return new HitClassification(
                HitClassificationType.None,
                HitOutcome.None);
        }

        if (state == SpearState.Embedded ||
            other.CompareTag(directionChangerTag))
        {
            return new HitClassification(
                HitClassificationType.None,
                HitOutcome.None);
        }

        if (damageable != null)
        {
            if (damageableAlreadyHit)
            {
                return new HitClassification(
                    HitClassificationType.None,
                    HitOutcome.None);
            }

            bool willBounce =
                state == SpearState.Flying ||
                state == SpearState.BouncedOff;

            return new HitClassification(
                HitClassificationType.Damage,
                willBounce ? HitOutcome.Bounce : HitOutcome.None);
        }

        if ((state == SpearState.Flying ||
             state == SpearState.BouncedOff) &&
            impactReceiver != null)
        {
            if (receiverAlreadyRegistered)
            {
                return new HitClassification(
                    HitClassificationType.None,
                    HitOutcome.None);
            }

            return new HitClassification(
                HitClassificationType.ImpactReceiverConditional,
                HitOutcome.BounceIfAccepted,
                ClassifyRejectedReceiverOutcome(other, state, impactReceiver));
        }

        if (CanEmbedInState(state))
        {
            return new HitClassification(
                HitClassificationType.Embed,
                HitOutcome.Embed);
        }

        return new HitClassification(
            HitClassificationType.None,
            HitOutcome.None);
    }

    private static bool CanEmbedInState(SpearState state)
    {
        return state == SpearState.Flying ||
               state == SpearState.BouncedOff ||
               state == SpearState.Dropped;
    }

    private static HitOutcome ClassifyRejectedReceiverOutcome(
        Collider2D other,
        SpearState state,
        ISpearImpactReceiver impactReceiver)
    {
        SpearImpactCounter completedCounter =
            impactReceiver as SpearImpactCounter;

        if (completedCounter != null &&
            completedCounter.IsComplete &&
            other.attachedRigidbody != null &&
            other.attachedRigidbody.bodyType == RigidbodyType2D.Static &&
            CanEmbedInState(state))
        {
            return HitOutcome.Embed;
        }

        return HitOutcome.None;
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
