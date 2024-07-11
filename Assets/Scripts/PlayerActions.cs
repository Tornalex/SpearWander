using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
public class PlayerActions : MonoBehaviour
{
    [Header("Player Movement Stats")]
    [SerializeField] float movementSpeed = 0f;
    [SerializeField] float jumpHeight = 0f;
    [SerializeField] float coyoteTimeReset = 0f;

    [Header("Components")]
    [SerializeField] SpearThrow spearThrow;
    [SerializeField] SpearCollector spearCollector;
    [SerializeField] PlayerFeet playerFeet;
    [SerializeField] CameraFollowObject cameraFollowObject;
    [SerializeField] Rigidbody2D playerRb;

    float coyoteTime;
    [HideInInspector] public bool hasCoyoteJumped;
    [HideInInspector] public bool isDead = false;
    [HideInInspector] public bool isFacingRight = true;
    [HideInInspector] public Vector2 moveInput;

    private void Awake()
    {
        isDead = false;
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
        Vector2 playerVelocity = new(moveInput.x * movementSpeed, playerRb.velocity.y);
        playerRb.velocity = playerVelocity;
        if (isDead)
        {
            playerRb.velocity = Vector2.zero;
        }
    }

    void OnMove(InputValue value)
    {
        if (!isDead)
        {
            moveInput = value.Get<Vector2>();    
        }
    }

    void OnJump()
    {
        if(coyoteTime >= 0 && !isDead && !hasCoyoteJumped)
        {
            GetComponent<Rigidbody2D>().AddForce(Vector2.up * jumpHeight, ForceMode2D.Impulse);  
            coyoteTime = 0;
            hasCoyoteJumped = true;
            print("coyote!");
        }
    }

    void OnFire()
    {
        if (!isDead)
        {
            spearThrow.Fire();
        }
    }
    void OnRestart()
    {
        SceneManager.LoadScene(0);
    }
    void CoyoteJump()
    {
        if (playerFeet.isGrounded)
        {
            coyoteTime = coyoteTimeReset;
        }
        coyoteTime -= Time.deltaTime;
    }
}