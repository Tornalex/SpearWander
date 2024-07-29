using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static UnityEditor.Timeline.TimelinePlaybackControls;
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

    float coyoteTime;
    [HideInInspector] public bool hasCoyoteJumped;
    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool isFacingRight = true;
    [HideInInspector] public Vector2 moveInput;
    public PlayerInputActions playerInputActions;
    private Camera mainCam;
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
        if (transform.position.y < -15)
        {
            SceneManager.LoadScene(0);
        }
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
        playerRb.velocity = new Vector2(moveDirection.x * speed, playerRb.velocity.y);
        if (isDead)
        {
            playerRb.velocity = Vector2.zero;
        }
    }
    public void AimWithMouse(InputAction.CallbackContext context)
    {
        if (context.performed && !isDead && context.control.device is Mouse)
        {
            Vector3 spearDirection = mainCam.ScreenToWorldPoint(playerInputActions.Player.AimWithMouse.ReadValue<Vector2>()) - transform.position;
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
            fireSpears.FireWithMouse(mainCam.ScreenToWorldPoint(playerInputActions.Player.AimWithMouse.ReadValue<Vector2>()));
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
        if (context.canceled)
        {
            playerRb.velocity = new(playerRb.velocity.x, playerRb.velocity.y * .35f);
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