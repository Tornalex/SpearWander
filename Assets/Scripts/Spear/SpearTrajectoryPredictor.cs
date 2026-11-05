using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Predicts a fresh spear throw without moving a Rigidbody2D or invoking hit
/// callbacks. The preview samples targets at their current positions and
/// integrates the trajectory at the project's fixed timestep.
/// </summary>
public sealed class SpearTrajectoryPredictor : MonoBehaviour
{
    private const int MaximumSteps = 180;
    private readonly Vector2[] _points = new Vector2[MaximumSteps + 1];
    private readonly List<RaycastHit2D> _rootHits = new(256);
    private readonly List<RaycastHit2D> _tipHits = new(256);
    private ContactFilter2D _rootContactFilter;
    private ContactFilter2D _tipContactFilter;

    private Spear _spearTemplate;
    private Rigidbody2D _spearBody;
    private BoxCollider2D _tipBox;
    private Collider2D _playerCollider;
    private bool _hasTipTrigger;
    private Vector2 _tipCenterInRootSpace;
    private Vector2 _tipSize;
    private float _tipAngleInRootSpace;

    public Vector2[] Points => _points;

    public void Configure(GameObject spearPrefab, Collider2D playerCollider)
    {
        _spearTemplate = spearPrefab != null
            ? spearPrefab.GetComponentInChildren<Spear>(true)
            : null;
        _spearBody = spearPrefab != null
            ? spearPrefab.GetComponent<Rigidbody2D>()
            : null;
        _playerCollider = playerCollider;

        _rootContactFilter = new ContactFilter2D();
        _rootContactFilter.SetLayerMask(Physics2D.AllLayers);
        _rootContactFilter.useTriggers = Physics2D.queriesHitTriggers;
        _tipContactFilter = _rootContactFilter;
        _tipContactFilter.useTriggers = true;

        SpearTip tip = spearPrefab != null
            ? spearPrefab.GetComponentInChildren<SpearTip>(true)
            : null;
        _tipBox = tip != null
            ? tip.GetComponent<BoxCollider2D>()
            : null;
        _hasTipTrigger = tip != null && tip.enabled &&
                         _tipBox != null && _tipBox.enabled;

        if (_tipBox != null && _spearTemplate != null)
        {
            Transform root = _spearTemplate.transform;
            Transform tipTransform = _tipBox.transform;

            _tipCenterInRootSpace = root.InverseTransformPoint(
                tipTransform.TransformPoint(_tipBox.offset));
            _tipSize = Vector2.Scale(
                _tipBox.size,
                Abs(tipTransform.lossyScale));
            _tipAngleInRootSpace =
                (Quaternion.Inverse(root.rotation) * tipTransform.rotation)
                .eulerAngles.z;
        }
    }

    /// <summary>
    /// Fills Points with the launch point and subsequent predicted root
    /// positions. The returned count is the number of valid points.
    /// </summary>
    public int Predict(
        Vector2 launchPosition,
        Vector2 launchDirection,
        float impulseMagnitude)
    {
        if (_spearTemplate == null ||
            _spearBody == null ||
            launchDirection.sqrMagnitude <= 0.0001f ||
            _spearBody.mass <= 0f)
        {
            return 0;
        }

        Vector2 direction = launchDirection.normalized;
        Vector2 velocity = direction * (impulseMagnitude / _spearBody.mass);
        Vector2 rootPosition = launchPosition;
        float deltaTime = Time.fixedDeltaTime;
        if (deltaTime <= 0f)
            return 0;

        _points[0] = rootPosition;
        int pointCount = 1;
        Vector2 spearFacing = direction;
        Vector2 lastStepDisplacement = Vector2.zero;

        for (int step = 0; step < MaximumSteps; step++)
        {
            // UpdateRotation runs after CheckForTunneling and before physics
            // moves the spear. Keep the same threshold and retain its previous
            // facing when the live spear does not update its rotation.
            if (velocity.sqrMagnitude > 0.5f)
                spearFacing = velocity.normalized;

            Vector2 velocityDirection = velocity.sqrMagnitude > 0.01f
                ? velocity.normalized
                : lastStepDisplacement.normalized;

            Vector2 nextVelocity = velocity +
                Physics2D.gravity * (_spearBody.gravityScale * deltaTime);

            // The spear prefab currently has zero linear damping. This
            // implicit damping approximation also tracks non-zero prefab
            // settings without altering the live Rigidbody.
            if (_spearBody.linearDamping > 0f)
            {
                nextVelocity /= 1f + _spearBody.linearDamping * deltaTime;
            }

            Vector2 nextRootPosition = rootPosition + nextVelocity * deltaTime;
            float rootCheckDistance = Mathf.Max(
                lastStepDisplacement.magnitude,
                velocity.magnitude * deltaTime);
            Vector2 rootRayOrigin = rootPosition - lastStepDisplacement;
            RaycastHit2D rootHit = default;
            bool rootContact = rootCheckDistance > 0.01f &&
                TryFindRootContact(
                    rootRayOrigin,
                    velocityDirection,
                    rootCheckDistance + 0.1f,
                    out rootHit);

            Vector2 currentTipCenter = GetTipWorldCenter(
                rootPosition,
                spearFacing);
            Vector2 nextTipCenter = GetTipWorldCenter(
                nextRootPosition,
                spearFacing);
            Vector2 tipDisplacement = nextTipCenter - currentTipCenter;
            RaycastHit2D tipHit = default;
            bool tipContact = _hasTipTrigger && TryFindTipContact(
                currentTipCenter,
                tipDisplacement,
                spearFacing,
                out tipHit);

            if (rootContact || tipContact)
            {
                // CheckForTunneling runs in FixedUpdate before the upcoming
                // physics step can produce the tip trigger callback.
                bool rootIsFirst = rootContact;

                Vector2 contactPosition;
                if (rootIsFirst)
                {
                    contactPosition = rootHit.point;

                    // The live ray starts at _lastPosition, which is one
                    // simulated position behind rootPosition after the first
                    // fixed step. Replace the current endpoint for a hit on
                    // that segment; preserve the ray's 0.1 look-ahead beyond it.
                    if (lastStepDisplacement.sqrMagnitude > 0.000001f &&
                        rootHit.distance <= lastStepDisplacement.magnitude)
                    {
                        _points[pointCount - 1] = contactPosition;
                        return pointCount;
                    }
                }
                else
                {
                    float tipFraction = tipDisplacement.sqrMagnitude > 0.000001f
                        ? Mathf.Clamp01(tipHit.distance / tipDisplacement.magnitude)
                        : 0f;
                    contactPosition = Vector2.Lerp(
                        rootPosition,
                        nextRootPosition,
                        tipFraction);
                }

                _points[pointCount++] = contactPosition;
                return pointCount;
            }

            lastStepDisplacement = nextRootPosition - rootPosition;
            rootPosition = nextRootPosition;
            velocity = nextVelocity;
            _points[pointCount++] = rootPosition;
        }

        return pointCount;
    }

    private bool TryFindRootContact(
        Vector2 origin,
        Vector2 direction,
        float distance,
        out RaycastHit2D nearestInteraction)
    {
        nearestInteraction = default;
        float nearestDistance = float.PositiveInfinity;
        EntityId nearestEntityId = default;

        _rootContactFilter.useTriggers = Physics2D.queriesHitTriggers;
        _rootHits.Clear();
        int hitCount = Physics2D.Raycast(
            origin,
            direction,
            _rootContactFilter,
            _rootHits,
            distance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _rootHits[i];
            Collider2D candidate = hit.collider;

            if (candidate == null ||
                !_spearTemplate.IsTunnelingCandidate(
                    candidate,
                    _playerCollider,
                    Spear.SpearState.Flying))
            {
                continue;
            }

            Spear.HitClassification classification =
                _spearTemplate.ClassifyHit(
                    candidate,
                    Spear.SpearState.Flying,
                    damageableAlreadyHit: false,
                    receiverAlreadyRegistered: false);

            if (classification.Type == Spear.HitClassificationType.None)
                continue;

            EntityId candidateId = candidate.GetEntityId();
            if (hit.distance < nearestDistance ||
                (hit.distance == nearestDistance &&
                 candidateId.CompareTo(nearestEntityId) < 0))
            {
                nearestInteraction = hit;
                nearestDistance = hit.distance;
                nearestEntityId = candidateId;
            }
        }

        return nearestInteraction.collider != null;
    }

    private bool TryFindTipContact(
        Vector2 origin,
        Vector2 displacement,
        Vector2 flightDirection,
        out RaycastHit2D nearestInteraction)
    {
        // OnTriggerEnter2D is generated by Unity's physics-step overlap
        // simulation. Sweeping only the actual child tip box over this step
        // approximates that contact without moving the spear or firing callbacks;
        // it does not model sub-step rotation or moving-target motion.
        nearestInteraction = default;
        if (displacement.sqrMagnitude <= 0.000001f)
            return false;

        float castAngle = GetTipWorldAngle(flightDirection);
        _tipHits.Clear();
        int hitCount = Physics2D.BoxCast(
            origin,
            _tipSize,
            castAngle,
            displacement.normalized,
            _tipContactFilter,
            _tipHits,
            displacement.magnitude);

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _tipHits[i];
            Collider2D candidate = hit.collider;

            if (candidate == null ||
                Physics2D.GetIgnoreLayerCollision(
                    _tipBox.gameObject.layer,
                    candidate.gameObject.layer) ||
                !_spearTemplate.IsTipTriggerCandidate(
                    candidate,
                    _playerCollider))
            {
                continue;
            }

            Spear.HitClassification classification =
                _spearTemplate.ClassifyHit(
                    candidate,
                    Spear.SpearState.Flying,
                    damageableAlreadyHit: false,
                    receiverAlreadyRegistered: false);

            if (classification.Type != Spear.HitClassificationType.None &&
                hit.distance < nearestDistance)
            {
                nearestInteraction = hit;
                nearestDistance = hit.distance;
            }
        }

        return nearestInteraction.collider != null;
    }

    private Vector2 GetTipWorldCenter(
        Vector2 rootPosition,
        Vector2 facing)
    {
        Quaternion rootRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
        Vector2 scaledLocalCenter = Vector2.Scale(
            _tipCenterInRootSpace,
            (Vector2)_spearTemplate.transform.localScale);
        return rootPosition + (Vector2)(rootRotation * scaledLocalCenter);
    }

    private float GetTipWorldAngle(Vector2 flightDirection)
    {
        float rootAngle = Mathf.Atan2(flightDirection.y, flightDirection.x) *
            Mathf.Rad2Deg;
        return rootAngle + _tipAngleInRootSpace;
    }

    private static Vector2 Abs(Vector3 value)
    {
        return new Vector2(Mathf.Abs(value.x), Mathf.Abs(value.y));
    }
}
