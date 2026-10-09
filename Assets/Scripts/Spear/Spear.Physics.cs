using UnityEngine;

public partial class Spear : MonoBehaviour
{
    [Header("Physics Settings")]
    [Tooltip("Tag assigned to camera boundary triggers")]
    [SerializeField] private string cameraBoundsTag = "CameraBounds";

    [Tooltip("DirectionChanger dei nemici: la lancia deve ignorarli")]
    [SerializeField] private string directionChangerTag = "EnemyDirectionChanger";

    [Tooltip("Raycast length to detect wall normal")]
    [SerializeField] private float tipRaycastLength = 1.5f;

    [Tooltip("How deep the spear embeds into walls")]
    [SerializeField] private float embedDepth = 0.15f;

    private int _spearLayer;

    private Collider2D _ignoredEnvironmentCollider;
    private IEmbeddable _currentEmbeddable;
    private Transform _embeddedTargetTransform;
    private Rigidbody2D _embeddedTargetRigidbody;
    private Vector3 _embeddedTargetLocalPosition;
    private Quaternion _embeddedTargetLocalRotation;

    private Vector2 _lastPosition;
    private Vector2 _lastVelocity;
    private bool _ignorePlayerCollisions;

    private void InitializePhysics()
    {
        _spearLayer =
            LayerMask.NameToLayer("Spear");

        if (_spearLayer < 0)
            _spearLayer = 6;

        gameObject.layer =
            _spearLayer;

        if (embedDepth <= 0f)
            embedDepth = 0.15f;
    }

    private void UpdateRotation()
    {
        if (_rb.linearVelocity.sqrMagnitude > 0.5f)
        {
            transform.right =
                _rb.linearVelocity;
        }
    }

    private void CheckForTunneling()
    {
        Vector2 currentPosition =
            transform.position;

        Vector2 direction =
            currentPosition - _lastPosition;

        float distance =
            direction.magnitude;

        Vector2 velocityDirection =
            _lastVelocity.sqrMagnitude > 0.01f
                ? _lastVelocity.normalized
                : direction.normalized;

        float checkDistance =
            Mathf.Max(
                distance,
                _lastVelocity.magnitude *
                Time.fixedDeltaTime
            );

        if (checkDistance > 0.01f)
        {
            float raycastDistance =
                checkDistance + 0.1f;

            RaycastHit2D[] hits =
                Physics2D.RaycastAll(
                    _lastPosition,
                    velocityDirection,
                    raycastDistance
                );

            RaycastHit2D nearestHit = default;
            float nearestHitDistance = float.PositiveInfinity;
            EntityId nearestHitColliderId = default;

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (hit.collider == _collider)
                    continue;

                if (hit.collider.gameObject.layer == _spearLayer)
                    continue;

                if (hit.transform.IsChildOf(transform))
                    continue;

                if (_playerCollider != null &&
                    hit.collider.transform.IsChildOf(
                        _playerCollider.transform))
                {
                    continue;
                }

                if (hit.collider.CompareTag(cameraBoundsTag))
                    continue;

                if (hit.collider.CompareTag(directionChangerTag))
                    continue;

                EntityId colliderId = hit.collider.GetEntityId();

                if (hit.distance < nearestHitDistance ||
                    (hit.distance == nearestHitDistance &&
                     colliderId.CompareTo(nearestHitColliderId) < 0))
                {
                    nearestHit = hit;
                    nearestHitDistance = hit.distance;
                    nearestHitColliderId = colliderId;
                }
            }

            if (nearestHit.collider != null)
            {
                ProcessHit(
                    nearestHit.collider,
                    nearestHit.point,
                    nearestHit.normal
                );

                return;
            }
        }

        _lastPosition =
            currentPosition;
    }

    private void StickToTarget(
        Transform target,
        Vector2 hitPoint,
        Collider2D surfaceCollider,
        Vector2 surfaceNormal)
    {
        _embeddedTargetTransform = target;
        _embeddedTargetRigidbody = null;

        currentState =
            SpearState.Embedded;

        _rb.bodyType =
            RigidbodyType2D.Kinematic;

        _rb.linearVelocity =
            Vector2.zero;

        _rb.angularVelocity =
            0f;

        Vector2 embedDirection =
            surfaceNormal;

        bool isWall =
            Mathf.Abs(surfaceNormal.x) >
            Mathf.Abs(surfaceNormal.y);

        if (isWall)
        {
            float targetAngle =
                surfaceNormal.x > 0
                    ? 180f
                    : 0f;

            transform.rotation =
                Quaternion.Euler(
                    0,
                    0,
                    targetAngle
                );
        }

        Vector2 spearForward =
            transform.right;

        float spearHalfLength =
            _collider.bounds.size.x *
            0.5f;

        Vector2 targetPosition =
            hitPoint
            - spearForward * spearHalfLength
            + embedDirection * embedDepth;

        transform.position =
            targetPosition;

        if (_effector != null)
        {
            float currentAngle =
                transform.eulerAngles.z;

            if (currentAngle < 0)
                currentAngle += 360;

            _effector.rotationalOffset =
                (currentAngle > 90 &&
                 currentAngle < 270)
                    ? 180
                    : 0;
        }

        if (target != null)
        {
            Rigidbody2D targetRigidbody =
                target.GetComponentInParent<Rigidbody2D>();

            if (targetRigidbody != null &&
                targetRigidbody.bodyType != RigidbodyType2D.Static)
            {
                Transform targetBodyTransform =
                    targetRigidbody.transform;

                _embeddedTargetRigidbody =
                    targetRigidbody;

                _embeddedTargetLocalPosition =
                    targetBodyTransform.InverseTransformPoint(
                        transform.position
                    );

                _embeddedTargetLocalRotation =
                    Quaternion.Inverse(targetBodyTransform.rotation) *
                    transform.rotation;

                // Avoid nesting this Rigidbody2D under the moving target body.
                transform.SetParent(null, true);
            }
            else
            {
                transform.SetParent(
                    target
                );
            }
        }

        _collider.isTrigger =
            false;

        if (surfaceCollider != null)
        {
            _ignoredEnvironmentCollider =
                surfaceCollider;

            Physics2D.IgnoreCollision(
                _collider,
                _ignoredEnvironmentCollider,
                true
            );
        }

        IgnoreAllPlayerColliders(false);
    }

    private void MoveEmbeddedWithTarget()
    {
        if (_embeddedTargetRigidbody == null ||
            !_embeddedTargetRigidbody.simulated)
        {
            return;
        }

        float fixedDeltaTime =
            Time.fixedDeltaTime;

        Transform targetTransform =
            _embeddedTargetRigidbody.transform;

        float angularStep =
            _embeddedTargetRigidbody.angularVelocity *
            fixedDeltaTime;

        Quaternion targetRotationStep =
            Quaternion.Euler(0f, 0f, angularStep);

        Vector2 currentTargetOffset =
            (Vector2)targetTransform.TransformPoint(
                _embeddedTargetLocalPosition
            ) -
            _embeddedTargetRigidbody.position;

        Vector2 nextPosition =
            _embeddedTargetRigidbody.position +
            _embeddedTargetRigidbody.linearVelocity *
            fixedDeltaTime +
            (Vector2)(targetRotationStep * currentTargetOffset);

        Quaternion nextRotation =
            targetRotationStep *
            targetTransform.rotation *
            _embeddedTargetLocalRotation;

        _rb.MovePosition(nextPosition);
        _rb.MoveRotation(nextRotation.eulerAngles.z);
    }

    private void NotifyEmbeddableRemoved()
    {
        IEmbeddable embeddable =
            _currentEmbeddable;

        _currentEmbeddable = null;

        if (embeddable != null)
        {
            Component embeddableComponent =
                embeddable as Component;

            if (embeddableComponent != null)
                embeddable.OnSpearRemoved(this);
        }

        _embeddedTargetTransform = null;
        _embeddedTargetRigidbody = null;
    }

    private void IgnoreAllPlayerColliders(
        bool ignore)
    {
        _ignorePlayerCollisions = ignore;

        if (_playerCollider == null)
            return;

        Physics2D.IgnoreCollision(
            _collider,
            _playerCollider,
            ignore
        );

        Collider2D[] children =
            _playerCollider.GetComponentsInChildren<
                Collider2D>();

        foreach (Collider2D c in children)
        {
            if (c != _playerCollider)
            {
                Physics2D.IgnoreCollision(
                    _collider,
                    c,
                    ignore
                );
            }
        }
    }

    private void ResetIgnoredWall()
    {
        if (_ignoredEnvironmentCollider != null)
        {
            Physics2D.IgnoreCollision(
                _collider,
                _ignoredEnvironmentCollider,
                false
            );

            _ignoredEnvironmentCollider =
                null;
        }
    }
}
