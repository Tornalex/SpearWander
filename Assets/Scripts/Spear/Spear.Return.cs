using UnityEngine;

public partial class Spear : MonoBehaviour
{
    private float _returnTimer;

    private Transform _playerTransform;

    [Header("Return Settings")]
    [SerializeField] private AnimationCurve returnCurve;
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private float maxReturnSpeed = 50f;
    [SerializeField] private float catchDistance = 0.7f;    
    public void StartReturn(
        Transform player)
    {
        DestroyRope();

        ResetIgnoredWall();

        _enemiesHitDuringThrow.Clear();
        _enemiesHitDuringReturn.Clear();

        if (transform.parent != null &&
            transform.parent.TryGetComponent(
                out IDamageable embeddedEnemy))
        {
            _enemiesHitDuringReturn.Add(
                embeddedEnemy
            );
        }

        _playerTransform =
            player;

        _returnTimer =
            0f;

        currentState =
            SpearState.Returning;

        _lastPosition =
            transform.position;

        transform.SetParent(null);

        _rb.bodyType =
            RigidbodyType2D.Kinematic;

        _rb.linearDamping =
            0f;

        _collider.isTrigger =
            true;

        IgnoreAllPlayerColliders(true);
    }

    public void AbortReturn()
    {
        DestroyRope();

        ResetIgnoredWall();

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