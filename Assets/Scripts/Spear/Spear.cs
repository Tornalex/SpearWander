using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using SpearWander.Boss;

public class Spear : MonoBehaviour
{
    public enum SpearState
    {
        Flying,
        BouncedOff,
        Embedded,
        Returning,
        Dropped
    }

    public SpearState currentState = SpearState.Flying;

    public event Action<Spear> OnSpearReturned;

    [Header("Settings - Damage & Flight")]
    [SerializeField] private AnimationCurve returnCurve;
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private float maxReturnSpeed = 50f;

    private int _impactDamage;
    private int _recallDamage;

    [Header("Enemy Bounce")]
    [SerializeField] private float enemyBounceSpeed = 15f;
    [SerializeField] private float bounceAngle = 90f;

    [Header("Settings - Physics & Tags")]
    [Tooltip("Tag assigned to camera boundary triggers")]
    [SerializeField] private string cameraBoundsTag = "CameraBounds";

    [Tooltip("DirectionChanger dei nemici: la lancia deve ignorarli")]
    [SerializeField] private string directionChangerTag = "EnemyDirectionChanger";

    [Tooltip("Distance to catch the returning spear")]
    [SerializeField] private float catchDistance = 0.7f;

    [Tooltip("Raycast length to detect wall normal")]
    [SerializeField] private float tipRaycastLength = 1.5f;

    [Tooltip("How deep the spear embeds into walls")]
    [SerializeField] private float embedDepth = 0.15f;

    private float _ropeLength;

    [Header("Rope Settings")]
    [Tooltip("Local offset where the rope attaches (handle)")]
    [SerializeField] private Vector2 ropeAttachOffset = new Vector2(-1.25f, 0f);

    [Tooltip("Visual width of the rope")]
    [SerializeField] private float ropeWidth = 0.08f;

    [Tooltip("Material used for the rope line renderer")]
    [SerializeField] private Material ropeMaterial;

    public void SetDamage(int impact, int recall)
    {
        _impactDamage = impact;
        _recallDamage = recall;
    }

    public int RecallDamage => _recallDamage;

    private float _returnTimer;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private PlatformEffector2D _effector;

    private Collider2D _playerCollider;
    private Collider2D _ignoredEnvironmentCollider;

    private Transform _playerTransform;

    private int _spearLayer;

    private List<IDamageable> _enemiesHitDuringReturn =
        new List<IDamageable>();

    private List<IDamageable> _enemiesHitDuringThrow =
        new List<IDamageable>();

    private Vector2 _lastPosition;
    private Vector2 _lastVelocity;

    public bool HasHitEnemy { get; private set; }

    private GameObject _rope;

    private bool _canSpawnRope;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();

        _spearLayer = LayerMask.NameToLayer("Spear");

        if (_spearLayer < 0)
            _spearLayer = 6;

        gameObject.layer = _spearLayer;

        if (returnCurve == null || returnCurve.keys.Length == 0)
            returnCurve = AnimationCurve.Linear(0, 0, 1, 1);

        if (embedDepth <= 0f)
            embedDepth = 0.15f;
    }

    void OnDestroy()
    {
        DestroyRope();
    }

    public void Initialize(
        Collider2D playerCol,
        bool canSpawnRope,
        float ropeLen = 5f)
    {
        ResetIgnoredWall();

        _playerCollider = playerCol;
        _playerTransform = playerCol.transform;

        _collider.isTrigger = true;

        HasHitEnemy = false;

        _enemiesHitDuringThrow.Clear();
        _enemiesHitDuringReturn.Clear();

        _lastPosition = transform.position;
        _lastVelocity = Vector2.zero;

        _canSpawnRope = canSpawnRope;
        _ropeLength = ropeLen;

        IgnoreAllPlayerColliders(true);
    }

    void FixedUpdate()
    {
        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff ||
            currentState == SpearState.Returning)
        {
            _lastVelocity = _rb.linearVelocity;
        }

        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff ||
            currentState == SpearState.Returning)
        {
            CheckForTunneling();
        }

        if (currentState == SpearState.Flying ||
            currentState == SpearState.BouncedOff ||
            currentState == SpearState.Returning ||
            currentState == SpearState.Dropped)
        {
            UpdateRotation();
        }

        if (currentState == SpearState.Returning)
        {
            MoveTowardsPlayer();
        }
    }

    void Update()
    {
        // No physics in Update - all physics in FixedUpdate.
    }

    private void UpdateRotation()
    {
        if (_rb.linearVelocity.sqrMagnitude > 0.5f)
        {
            transform.right = _rb.linearVelocity;
        }
    }

    private void CheckForTunneling()
    {
        Vector2 currentPosition = transform.position;

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

        _lastPosition = currentPosition;
    }

    public void OnTipHit(Collider2D other)
    {
        if (other.gameObject.layer == _spearLayer)
            return;

        if (other.transform.IsChildOf(transform))
            return;

        if (currentState == SpearState.Embedded)
            return;

        if (other.CompareTag(cameraBoundsTag))
            return;

        if (other.CompareTag(directionChangerTag))
            return;

        if (_playerCollider != null &&
            other.transform.IsChildOf(
                _playerCollider.transform))
        {
            return;
        }

        Vector2 hitPoint =
            other.ClosestPoint(transform.position);

        Vector2 flyDir =
            _lastVelocity.sqrMagnitude > 0.01f
                ? _lastVelocity.normalized
                : (Vector2)transform.right;

        RaycastHit2D hit =
            Physics2D.Raycast(
                hitPoint - flyDir * 0.5f,
                flyDir,
                tipRaycastLength,
                1 << other.gameObject.layer
            );

        Vector2 normal =
            hit.collider != null
                ? hit.normal
                : Vector2.up;

        ProcessHit(
            other,
            hitPoint,
            normal
        );
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState != SpearState.Flying)
            return;

        Boss boss =
            collision.collider.GetComponentInParent<Boss>();

        if (boss != null)
        {
            WeakPoint weakPoint =
                collision.collider.GetComponent<WeakPoint>();

            if (weakPoint != null)
                return;

            BounceOffBoss(collision);
        }
    }

    private void BounceOffBoss(Collision2D collision)
    {
        Vector2 normal =
            collision.contacts[0].normal;

        Vector2 velocity =
            _rb.linearVelocity;

        float dot =
            Vector2.Dot(
                velocity,
                normal
            );

        Vector2 bounceDirection =
            velocity - 2 * dot * normal;

        float bounceFactor = 0.6f;

        Vector2 bounceVelocity =
            bounceDirection * bounceFactor;

        _rb.linearVelocity =
            bounceVelocity;

        if (bounceVelocity.sqrMagnitude > 0.01f)
        {
            transform.right =
                bounceVelocity.normalized;
        }

        _rb.bodyType =
            RigidbodyType2D.Dynamic;

        _rb.gravityScale = 1f;

        _collider.isTrigger = false;

        currentState =
            SpearState.Dropped;

        if (collision.collider != null &&
            _collider != null)
        {
            Physics2D.IgnoreCollision(
                _collider,
                collision.collider,
                true
            );

            StartCoroutine(
                ReenableCollisionWithBoss(
                    collision.collider,
                    0.5f
                )
            );
        }
    }

    private IEnumerator ReenableCollisionWithBoss(
        Collider2D bossCollider,
        float delay)
    {
        yield return new WaitForSeconds(delay);

        if (bossCollider != null &&
            _collider != null)
        {
            Physics2D.IgnoreCollision(
                _collider,
                bossCollider,
                false
            );
        }
    }

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
            if (currentState == SpearState.Flying ||
                currentState == SpearState.BouncedOff)
            {
                if (_enemiesHitDuringThrow.Contains(
                    damageable))
                {
                    return;
                }

                _enemiesHitDuringThrow.Add(
                    damageable
                );

                HasHitEnemy = true;

                damageable.TakeDamage(
                    _impactDamage,
                    hitPoint,
                    transform.position
                );

                BounceOffEnemy();
            }
            else if (currentState == SpearState.Returning)
            {
                if (!_enemiesHitDuringReturn.Contains(
                    damageable))
                {
                    _enemiesHitDuringReturn.Add(
                        damageable
                    );

                    HasHitEnemy = true;

                    damageable.TakeDamage(
                        _recallDamage,
                        hitPoint,
                        transform.position
                    );
                }
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

        _collider.isTrigger = true;

        transform.right =
            randomDirection;
    }

    private void CreateRope()
    {
        if (_rope != null)
            return;

        if (!_canSpawnRope)
            return;

        _rope =
            new GameObject("Rope");

        _rope.transform.SetParent(
            transform,
            false
        );

        _rope.transform.localPosition =
            ropeAttachOffset;

        _rope.transform.rotation =
            Quaternion.identity;

        _rope.layer =
            _spearLayer;

        LineRenderer lr =
            _rope.AddComponent<LineRenderer>();

        lr.useWorldSpace = false;

        lr.positionCount = 2;

        lr.SetPosition(
            0,
            Vector3.zero
        );

        lr.SetPosition(
            1,
            Vector3.down *
            _ropeLength
        );

        lr.startWidth =
            ropeWidth;

        lr.endWidth =
            ropeWidth;

        if (ropeMaterial != null)
            lr.sharedMaterial =
                ropeMaterial;

        lr.startColor =
            new Color(
                0.5f,
                0.35f,
                0.15f
            );

        lr.endColor =
            new Color(
                0.5f,
                0.35f,
                0.35f
            );

        EdgeCollider2D ec =
            _rope.AddComponent<EdgeCollider2D>();

        ec.points =
            new Vector2[]
            {
                Vector2.zero,
                Vector2.down *
                _ropeLength
            };

        ec.isTrigger = true;

        _rope.AddComponent<Rope>();
    }

    private void DestroyRope()
    {
        if (_rope != null)
        {
            if (Application.isPlaying)
                Destroy(_rope);
            else
                DestroyImmediate(_rope);

            _rope = null;
        }
    }

    void StickToTarget(
        Transform target,
        Vector2 hitPoint,
        Collider2D surfaceCollider,
        Vector2 surfaceNormal)
    {
        Vector2 flyDirection =
            _lastVelocity.sqrMagnitude > 0.01f
                ? _lastVelocity.normalized
                : (Vector2)transform.right;

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
                    (surfaceNormal.x > 0)
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

            IgnoreAllPlayerColliders(
                false
            );
        }
    }

    public void StartReturn(Transform player)
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

        IgnoreAllPlayerColliders(
            true
        );
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

    void MoveTowardsPlayer()
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
            _playerCollider
                .GetComponentsInChildren<
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