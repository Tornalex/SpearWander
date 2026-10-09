using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class MovingPlatform : MonoBehaviour, IEmbeddable
{
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField, Min(0f)] private float speed = 2f;
    [SerializeField, Min(0f)] private float waitTime;

    private Rigidbody2D _rb;
    private Vector2 _startPosition;
    private Vector2 _endPosition;
    private bool _movingToEndPoint = true;
    private float _waitTimer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;

        if (startPoint == null || endPoint == null ||
            IsInPlatformHierarchy(startPoint) ||
            IsInPlatformHierarchy(endPoint))
        {
            Debug.LogError(
                "MovingPlatform requires Start Point and End Point Transforms outside its hierarchy.",
                this);
            enabled = false;
            return;
        }

        _startPosition = startPoint.position;
        _endPosition = endPoint.position;
        _rb.position = _startPosition;
    }

    private void FixedUpdate()
    {
        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
            return;
        }

        if (speed <= 0f)
            return;

        Vector2 destination = _movingToEndPoint
            ? _endPosition
            : _startPosition;
        Vector2 toDestination = destination - _rb.position;
        float distance = toDestination.magnitude;
        float step = speed * Time.fixedDeltaTime;

        if (distance <= step)
        {
            _rb.MovePosition(destination);
            _movingToEndPoint = !_movingToEndPoint;
            _waitTimer = Mathf.Max(0f, waitTime);
            return;
        }

        _rb.MovePosition(_rb.position + toDestination / distance * step);
    }

    private bool IsInPlatformHierarchy(Transform point)
    {
        return point == transform || point.IsChildOf(transform);
    }

    public void OnSpearEmbedded(Spear spear, Vector2 hitPoint, Vector2 surfaceNormal)
    {
    }

    public void OnSpearRemoved(Spear spear)
    {
    }
}
