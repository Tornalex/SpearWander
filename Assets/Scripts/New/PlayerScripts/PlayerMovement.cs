using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 8f;

    private Rigidbody2D _rb;
    private PlayerInputHandler _input;
    private Animator _anim;
    private PlayerDash _dash;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _anim = GetComponentInChildren<Animator>();
        _dash = GetComponent<PlayerDash>();
    }

    void FixedUpdate()
    {
        if (_dash != null && (_dash.IsDashing || _dash.IsKnockedBack)) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        float moveX = _input.MoveInput.x;
        _rb.linearVelocity = new Vector2(moveX * speed, _rb.linearVelocity.y);
        
        _anim.SetFloat("Speed", Mathf.Abs(moveX));
        HandleFlip(moveX);
    }

    private void HandleFlip(float moveX)
    {
        if (moveX > 0) transform.localScale = new Vector3(1, 1, 1);
        else if (moveX < 0) transform.localScale = new Vector3(-1, 1, 1);
    }
}