using UnityEngine;
using System.Collections.Generic;

public partial class Spear : MonoBehaviour
{
    private struct PlayerCollisionPairState
    {
        public Collider2D spearCollider;
        public Collider2D playerCollider;
        public bool wasIgnored;
    }

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
    private bool _embeddedTargetUsesRigidbody;
    private Vector3 _embeddedTargetLocalPosition;
    private Quaternion _embeddedTargetLocalRotation;

    private Vector2 _lastPosition;
    private Vector2 _lastVelocity;
    private bool _ignorePlayerCollisions;
    private bool _floorEmbeddedPlayerCollisionOverride;
    private readonly List<PlayerCollisionPairState> _floorEmbeddedPlayerCollisionPairs = new();

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

            Collider2D[] processedColliders =
                new Collider2D[hits.Length];
            int processedColliderCount = 0;

            while (true)
            {
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

                    if (currentState == SpearState.Returning &&
                        !hit.collider.TryGetComponent(
                            out IDamageable _))
                    {
                        continue;
                    }

                    bool colliderAlreadyProcessed = false;
                    for (int i = 0; i < processedColliderCount; i++)
                    {
                        if (processedColliders[i] == hit.collider)
                        {
                            colliderAlreadyProcessed = true;
                            break;
                        }
                    }

                    if (colliderAlreadyProcessed)
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

                if (nearestHit.collider == null)
                    break;

                processedColliders[processedColliderCount++] =
                    nearestHit.collider;

                if (currentState == SpearState.Returning)
                {
                    ProcessHit(
                        nearestHit.collider,
                        nearestHit.point,
                        nearestHit.normal
                    );

                    continue;
                }

                if (nearestHit.collider.TryGetComponent(
                        out IDamageable damageable) &&
                    _enemiesHitDuringThrow.Contains(damageable))
                {
                    continue;
                }

                ProcessHit(
                    nearestHit.collider,
                    nearestHit.point,
                    nearestHit.normal
                );

                _lastPosition =
                    currentPosition;

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
        Vector2 surfaceNormal,
        bool hasValidSurfaceNormal)
    {
        _embeddedTargetTransform = target;
        _embeddedTargetRigidbody = null;

        bool isUpwardFloorSurface =
            hasValidSurfaceNormal &&
            surfaceNormal.sqrMagnitude > 0.0001f &&
            surfaceNormal.y > Mathf.Abs(surfaceNormal.x);

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

        BoxCollider2D boxCollider =
            _collider as BoxCollider2D;

        Vector2 colliderOffset;
        Vector2 spearHalfLength;

        if (boxCollider != null)
        {
            colliderOffset =
                transform.TransformVector(
                    (Vector3)boxCollider.offset
                );

            spearHalfLength =
                transform.TransformVector(
                    Vector3.right *
                    (boxCollider.size.x * 0.5f)
                );
        }
        else
        {
            // Preserve the previous bounds-based placement for non-box colliders.
            colliderOffset = Vector2.zero;
            spearHalfLength =
                transform.right *
                (_collider.bounds.size.x * 0.5f);
        }

        Vector2 targetPosition =
            hitPoint
            - colliderOffset
            - spearHalfLength
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
                _embeddedTargetUsesRigidbody =
                    true;

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

        if (isUpwardFloorSurface)
            IgnorePlayerCollisionWithAllSpearColliders();
    }

    private void MoveEmbeddedWithTarget()
    {
        if (!_embeddedTargetUsesRigidbody)
            return;

        if (_embeddedTargetTransform == null ||
            !_embeddedTargetTransform.gameObject.activeInHierarchy ||
            _embeddedTargetRigidbody == null ||
            !_embeddedTargetRigidbody.simulated)
        {
            DetachFromEmbeddedTarget();
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
        _embeddedTargetUsesRigidbody = false;
    }

    private void OnDisable()
    {
        if (currentState == SpearState.Embedded &&
            transform.parent != null &&
            !transform.parent.gameObject.activeInHierarchy)
        {
            DetachFromEmbeddedTarget();
        }
    }

    private void DetachFromEmbeddedTarget()
    {
        ClearRopeAndIgnoredWall();
        NotifyEmbeddableRemoved();
        transform.SetParent(null, true);

        currentState =
            SpearState.Dropped;

        _rb.bodyType =
            RigidbodyType2D.Dynamic;
        _rb.gravityScale =
            1.5f;
        _rb.linearDamping =
            2f;
        _rb.linearVelocity =
            Vector2.zero;
        _rb.angularVelocity =
            0f;

        _collider.isTrigger =
            false;

        IgnoreAllPlayerColliders(false);
        RestoreFloorEmbeddedPlayerCollisionOverride();
    }

    private void IgnorePlayerCollisionWithAllSpearColliders()
    {
        if (_floorEmbeddedPlayerCollisionOverride ||
            _playerCollider == null)
        {
            return;
        }

        Collider2D[] spearColliders =
            GetComponentsInChildren<Collider2D>();
        Collider2D[] playerColliders =
            _playerCollider.GetComponentsInChildren<Collider2D>();

        _floorEmbeddedPlayerCollisionPairs.Clear();

        foreach (Collider2D spearCollider in spearColliders)
        {
            foreach (Collider2D playerCollider in playerColliders)
            {
                if (spearCollider == null || playerCollider == null)
                    continue;

                _floorEmbeddedPlayerCollisionPairs.Add(
                    new PlayerCollisionPairState
                    {
                        spearCollider = spearCollider,
                        playerCollider = playerCollider,
                        wasIgnored = Physics2D.GetIgnoreCollision(
                            spearCollider,
                            playerCollider)
                    });

                Physics2D.IgnoreCollision(
                    spearCollider,
                    playerCollider,
                    true);
            }
        }

        _floorEmbeddedPlayerCollisionOverride = true;
    }

    private void RestoreFloorEmbeddedPlayerCollisionOverride()
    {
        if (!_floorEmbeddedPlayerCollisionOverride)
            return;

        foreach (PlayerCollisionPairState pair in _floorEmbeddedPlayerCollisionPairs)
        {
            if (pair.spearCollider == null || pair.playerCollider == null)
                continue;

            bool shouldIgnore =
                pair.spearCollider == _collider
                    ? _ignorePlayerCollisions
                    : pair.wasIgnored;

            Physics2D.IgnoreCollision(
                pair.spearCollider,
                pair.playerCollider,
                shouldIgnore);
        }

        _floorEmbeddedPlayerCollisionPairs.Clear();
        _floorEmbeddedPlayerCollisionOverride = false;
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
