using UnityEngine;
using System;

public partial class Spear : MonoBehaviour
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

    public bool HasHitEnemy { get; private set; }

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private PlatformEffector2D _effector;

    private Collider2D _playerCollider;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _effector = GetComponent<PlatformEffector2D>();

        InitializePhysics();
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
}