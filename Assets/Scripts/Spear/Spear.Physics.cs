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

    private Vector2 _lastPosition;
    private Vector2 _lastVelocity;

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

                ProcessHit(
                    hit.collider,
                    hit.point,
                    hit.normal
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

        if (target == null)
        {
            bool isWall =
                Mathf.Abs(surfaceNormal.x) >
                Mathf.Abs(surfaceNormal.y);

            if (isWall)
            {
                embedDirection =
                    surfaceNormal;

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
            else
            {
                embedDirection =
                    surfaceNormal;
            }
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
            transform.SetParent(
                target
            );

            _collider.isTrigger =
                true;
        }
        else
        {
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
    }

    private void IgnoreAllPlayerColliders(
        bool ignore)
    {
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