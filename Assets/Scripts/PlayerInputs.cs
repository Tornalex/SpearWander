using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerInputs : MonoBehaviour
{
    [Header("Player Movement Stats")]
    [SerializeField] float speed = 0f;
    [SerializeField] float jumpHeight = 0f;
    [SerializeField] float coyoteTimeReset = 0f;

    [Header("Spear Stats")]
    public int equippedSpears;
    public int spearSpeed;
    [HideInInspector] public Queue<GameObject> thrownSpearsQueue = new Queue<GameObject>();

    [Header("Components")]
    [SerializeField] Transform spearIndicator;
    [SerializeField] FireSpears fireSpears;
    [SerializeField] RecallSpears recallSpears;
    [SerializeField] PlayerFeet playerFeet;
    [SerializeField] Rigidbody2D playerRb;
    [SerializeField] Animator movementAnim;
    [SerializeField] Transform spriteTransform;
    public PlayerInputActions playerInputActions;

    float coyoteTime;
    [HideInInspector] public bool hasCoyoteJumped;
    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool isFacingRight = true;
    private Camera mainCam;
    private bool facingRight = false;

    private void Awake()
    {
        isDead = false;
        playerInputActions = new PlayerInputActions();
        playerInputActions.Enable();
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }

    private void Update()
    {
        CoyoteJump();
        FacingRightCheck();

        if (transform.position.y < -15)
        {
            SceneManager.LoadScene(0);
        }

        float speedThreshold = 0.05f;
        float horizontalSpeed = Mathf.Abs(playerRb.linearVelocity.x);
        movementAnim.SetFloat("Speed", horizontalSpeed > speedThreshold ? horizontalSpeed : 0f);
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
        Vector2 moveDirection = playerInputActions.Player.Move.ReadValue<Vector2>();
        playerRb.linearVelocity = new Vector2(moveDirection.x * speed, playerRb.linearVelocity.y);
        if (isDead)
        {
            playerRb.linearVelocity = Vector2.zero;
        }
    }

    public void AimWithMouse(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead && context.control.device is Mouse)
        {
            Vector3 mousePos = mainCam.ScreenToWorldPoint(playerInputActions.Player.AimWithMouse.ReadValue<Vector2>());
            mousePos.z = 0f;

            Vector3 spearDirection = mousePos - transform.position;
            float rotZ = Mathf.Atan2(spearDirection.y, spearDirection.x) * Mathf.Rad2Deg;
            spearIndicator.rotation = Quaternion.Euler(0, 0, rotZ);
        }
    }

    public void AimWithGamepad(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead && context.control.device is Gamepad)
        {
            Vector3 spearDirection = playerInputActions.Player.AimWithController.ReadValue<Vector2>();
            float rotZ = Mathf.Atan2(spearDirection.y, spearDirection.x) * Mathf.Rad2Deg;
            spearIndicator.rotation = Quaternion.Euler(0, 0, rotZ);
        }
    }

    public void Fire(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead && context.control.device is Mouse)
        {
            Vector3 mousePos = mainCam.ScreenToWorldPoint(playerInputActions.Player.AimWithMouse.ReadValue<Vector2>());
            mousePos.z = 0f;

            fireSpears.FireWithMouse(mousePos);
            
        }
        if (context.performed && !isDead && context.control.device is Gamepad)
        {
            fireSpears.FireWithGamepad(playerInputActions.Player.AimWithController.ReadValue<Vector2>());
        }
    }

    public void Recall(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead)
        {
            recallSpears.Recall();
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
        if (context.canceled && playerRb.linearVelocity.y > 0f)
    {
        playerRb.linearVelocity = new(playerRb.linearVelocity.x, playerRb.linearVelocity.y * 0.35f);
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

    private void FacingRightCheck()
    {
        float moveDirection = playerInputActions.Player.Move.ReadValue<Vector2>().x;
        if (moveDirection > 0f && !facingRight)
        {
            Flip();
        }
        else if (moveDirection < 0f && facingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;
        isFacingRight = facingRight;
        Vector3 scale = spriteTransform.localScale;
        scale.x *= -1;
        spriteTransform.localScale = scale;
    }
}