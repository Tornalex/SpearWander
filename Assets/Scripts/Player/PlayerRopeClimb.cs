using UnityEngine;

public class PlayerRopeClimb : MonoBehaviour
{
    [Header("Climb Settings")]
    [SerializeField] private float climbSpeed = 5f;
    [SerializeField] private float dismountJumpForce = 10f;
    [SerializeField] private float dismountCooldown = 0.5f;

    public bool IsClimbing { get; private set; }

    private Player _player;
    private Rope _currentRope;
    private float _originalGravityScale;
    private bool _wasMovementEnabled;
    private bool _wasDashEnabled;
    private bool _wasPogoEnabled;
    private float _dismountCooldownTimer;
    private bool _canAutoDismount;

    void Awake()
    {
        _player = GetComponent<Player>();
        _originalGravityScale = _player.Rb.gravityScale;
    }

    void Update()
    {
        if (_dismountCooldownTimer > 0)
            _dismountCooldownTimer -= Time.deltaTime;

        if (IsClimbing)
        {
            if (_currentRope == null)
            {
                StopClimbing();
                return;
            }

            if (_player.Input.JumpTriggered)
            {
                StopClimbing();
                _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, dismountJumpForce);
                return;
            }

            if (Mathf.Abs(_player.Input.MoveInput.x) > 0.5f && Mathf.Abs(_player.Input.MoveInput.y) < 0.1f)
            {
                StopClimbing();
                return;
            }

            float ropeTopY = _currentRope.transform.position.y;
            if (_canAutoDismount && transform.position.y >= ropeTopY - 0.15f)
            {
                _canAutoDismount = false;
                StopClimbing();
                _player.Rb.linearVelocity = new Vector2(_player.Rb.linearVelocity.x, dismountJumpForce);
                return;
            }
        }
    }

    void FixedUpdate()
    {
        if (!IsClimbing) return;

        if (_currentRope == null) return;

        float ropeTopY = _currentRope.transform.position.y;
        float ropeBottomY = _currentRope.GetBottomY();

        if (transform.position.y > ropeTopY)
        {
            _player.Rb.linearVelocity = Vector2.zero;
            Vector3 p = transform.position;
            p.y = ropeTopY;
            p.x = _currentRope.transform.position.x;
            transform.position = p;
            return;
        }

        _canAutoDismount = _player.Input.MoveInput.y > 0.1f;

        if (Mathf.Abs(_player.Input.MoveInput.y) > 0.1f)
        {
            _player.Rb.linearVelocity = new Vector2(0, _player.Input.MoveInput.y * climbSpeed);
        }
        else
        {
            _player.Rb.linearVelocity = Vector2.zero;
        }

        Vector3 pos = transform.position;
        pos.y = Mathf.Clamp(pos.y, ropeBottomY, ropeTopY);
        pos.x = _currentRope.transform.position.x;
        transform.position = pos;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (IsClimbing) return;

        Rope rope = other.GetComponent<Rope>();
        if (rope == null) return;

        _currentRope = rope;

        if (_dismountCooldownTimer > 0) return;
        if (transform.position.y >= rope.transform.position.y) return;
        if (_player.Input.MoveInput.y > 0.1f)
        {
            StartClimbing(rope);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        Rope rope = other.GetComponent<Rope>();
        if (rope != null && _currentRope == rope)
        {
            _currentRope = null;
            if (IsClimbing) StopClimbing();
        }
    }

    void StartClimbing(Rope rope)
    {
        if (IsClimbing) return;

        IsClimbing = true;
        _currentRope = rope;
        _canAutoDismount = false;

        _wasMovementEnabled = _player.Movement.enabled;
        _wasDashEnabled = _player.Dash.enabled;
        _wasPogoEnabled = _player.Pogo.enabled;

        _player.Movement.enabled = false;
        _player.Dash.enabled = false;
        _player.Pogo.enabled = false;

        _originalGravityScale = _player.Rb.gravityScale;
        _player.Rb.gravityScale = 0f;
        _player.Rb.linearVelocity = Vector2.zero;

        Vector3 pos = transform.position;
        if (pos.y > rope.transform.position.y)
            pos.y = rope.transform.position.y;
        pos.x = rope.transform.position.x;
        transform.position = pos;
    }

    void StopClimbing()
    {
        if (!IsClimbing) return;

        IsClimbing = false;
        _currentRope = null;
        _canAutoDismount = false;
        _dismountCooldownTimer = dismountCooldown;

        _player.Movement.enabled = _wasMovementEnabled;
        _player.Dash.enabled = _wasDashEnabled;
        _player.Pogo.enabled = _wasPogoEnabled;

        _player.Rb.gravityScale = _originalGravityScale;
    }
}
