using UnityEngine;

public partial class Spear : MonoBehaviour
{
    private float _returnTimer;
    private bool _ignorePlayerCollisionsBeforeReturn;

    private Transform _playerTransform;

    [Header("Return Settings")]
    [SerializeField] private AnimationCurve returnCurve;
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private float maxReturnSpeed = 50f;
    [SerializeField] private float catchDistance = 0.7f;    

    private void ClearRopeAndIgnoredWall()
    {
        DestroyRope();
        ResetIgnoredWall();
    }

    private void InitializeReturnHitTracking()
    {
        ClearHitLists();

        Transform embeddedTarget =
            _embeddedTargetTransform != null
                ? _embeddedTargetTransform
                : transform.parent;

        if (embeddedTarget != null &&
            embeddedTarget.TryGetComponent(
                out IDamageable embeddedEnemy))
        {
            _enemiesHitDuringReturn.Add(
                embeddedEnemy
            );
        }

        _embeddedTargetTransform = null;
    }

    public void StartReturn(
        Transform player)
    {
        _ignorePlayerCollisionsBeforeReturn =
            _ignorePlayerCollisions;

        ClearRopeAndIgnoredWall();

        InitializeReturnHitTracking();

        _playerTransform =
            player;

        _returnTimer =
            0f;

        currentState =
            SpearState.Returning;

        _lastPosition =
            transform.position;

        NotifyEmbeddableRemoved();

        transform.SetParent(null);

        _rb.bodyType =
            RigidbodyType2D.Kinematic;

        _rb.linearDamping =
            0f;

        _collider.isTrigger =
            true;

        IgnoreAllPlayerColliders(true);
        RestoreFloorEmbeddedPlayerCollisionOverride();
    }

    public void AbortReturn()
    {
        ClearRopeAndIgnoredWall();

        currentState =
            SpearState.Dropped;

        _rb.bodyType =
            RigidbodyType2D.Dynamic;

        _rb.gravityScale =
            1.5f;

        _rb.linearDamping =
            2f;

        _collider.isTrigger =
            false;

        IgnoreAllPlayerColliders(
            _ignorePlayerCollisionsBeforeReturn
        );
    }

    private void MoveTowardsPlayer()
    {
        if (_playerTransform == null)
            return;

        _returnTimer +=
            Time.fixedDeltaTime;

        float timeNormalized =
            Mathf.Clamp01(
                _returnTimer /
                returnDuration
            );

        float currentSpeed =
            returnCurve.Evaluate(
                timeNormalized
            ) *
            maxReturnSpeed;

        Vector2 toPlayer =
            (Vector2)_playerTransform.position
            - (Vector2)transform.position;

        float distanceToPlayer =
            toPlayer.magnitude;

        if (distanceToPlayer <
            catchDistance)
        {
            OnSpearReturned?.Invoke(
                this
            );

            return;
        }

        Vector2 direction =
            toPlayer.normalized;

        _rb.linearVelocity =
            direction *
            currentSpeed;
    }
}
