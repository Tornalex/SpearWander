using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
public class PlayerActions : MonoBehaviour
{
    [Header("Player Movement Stats")]
    [SerializeField] float speed = 0f;
    [SerializeField] float jumpHeight = 0f;
    [SerializeField] float coyoteTimeReset = 0f;

    [Header("Components")]
    [SerializeField] ThrowAndRecallSpears spearThrow;
    [SerializeField] PlayerFeet playerFeet;
    [SerializeField] Rigidbody2D playerRb;

    float coyoteTime;
    [HideInInspector] public bool hasCoyoteJumped;
    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool isFacingRight = true;
    [HideInInspector] public Vector2 moveInput;
    [HideInInspector] public Vector2 fireInputVector;
    public PlayerInput playerInputActions;
    private void Awake()
    {
        isDead = false;
        playerInputActions = new PlayerInput();
        playerInputActions.Enable();
    }

    private void Update()
    {
        CoyoteJump();
        if (transform.position.y < -15)
        {
            SceneManager.LoadScene(0);
        }
        spearThrow.AimWithController(playerInputActions.Player.Aim.ReadValue<Vector2>());
        fireInputVector = playerInputActions.Player.Aim.ReadValue<Vector2>();

    }
    private void FixedUpdate()
    {
        Move();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.transform.tag == "Enemy")
        {
            isDead = true;
        }
    }

    void Move()
    {
        Vector2 moveInputVector = playerInputActions.Player.Move.ReadValue<Vector2>();
        playerRb.velocity = new Vector2(moveInputVector.x * speed, playerRb.velocity.y);
        if (isDead)
        {
            playerRb.velocity = Vector2.zero;
        }
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead && coyoteTime >= 0 && !hasCoyoteJumped)
        {
            playerRb.AddForce(Vector2.up * jumpHeight, ForceMode2D.Impulse);
            coyoteTime = 0;
            hasCoyoteJumped = true;
        }
        if (context.canceled)
        {
            playerRb.velocity = new(playerRb.velocity.x, playerRb.velocity.y * .35f);
        }
    }

    public void Fire(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead && context.control.device is Keyboard)
        {
            spearThrow.FireWithMouse();
        }
        if (context.performed && !isDead && context.control.device is Gamepad)
        {
            spearThrow.FireWithController();
        }
    }
    public void Recall(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead)
        {
            spearThrow.Recall();
        }
    }
    public void Restart(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead)
        {
            SceneManager.LoadScene(0);
        }
    }
    public void CoyoteJump()
    {
        if (playerFeet.isGrounded)
        {
            coyoteTime = coyoteTimeReset;
        }
        coyoteTime -= Time.deltaTime;
    }
}