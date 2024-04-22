using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerActions : MonoBehaviour
{
    [SerializeField] float movementSpeed = 0f;
    [SerializeField] float jumpHeight = 0f;
    [HideInInspector] public bool isDead = false;

    [HideInInspector] public bool isFacingRight = true;
    [HideInInspector] public Vector2 moveInput;
    float fallingSpeed;
    Rigidbody2D playerRb;

    [SerializeField] SpearThrow spearThrow;
    [SerializeField] SpearCollector spearCollector;
    [SerializeField] PlayerFeet playerFeet;
    [SerializeField] CameraFollowObject cameraFollowObject;
    private void Awake()
    {
        playerRb = GetComponent<Rigidbody2D>();
        isDead = false;
    }
    private void FixedUpdate()
    {
        fallingSpeed = playerRb.velocity.y;
        Move();
        if (moveInput.x > 0 || moveInput.x < 0)
        {
            CheckTurn();
        }
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
    void CheckTurn()
    {
        if (moveInput.x > 0 && !isFacingRight)
        {
            Turn();
        }
        else if (moveInput.x < 0 && isFacingRight)
        {
            Turn();
        }
    }    
    void Turn()
    {
        if(isFacingRight)
        {
            Vector3 rotator = new(transform.rotation.x, 180f, transform.rotation.z);
            transform.rotation = Quaternion.Euler(rotator);
            isFacingRight = !isFacingRight;
            cameraFollowObject.CallTurn();
        }
        else
        {
            Vector3 rotator = new(transform.rotation.x, 0f, transform.rotation.z);
            transform.rotation = Quaternion.Euler(rotator);
            isFacingRight = !isFacingRight;
            cameraFollowObject.CallTurn();

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
        if (playerFeet.canJump && !isDead)
        {
            GetComponent<Rigidbody2D>().AddForce(Vector2.up * jumpHeight, ForceMode2D.Impulse);
        }
    }
    void OnFire()
    {
        if (!isDead)
        {
            spearThrow.Fire();
        }
    }
    void OnPickup()
    {
        if(spearCollector.isTouchingSpear && !isDead)
        {
            spearCollector.DestroySpear();
            spearThrow.currentlyEquippedSpears++;
        }
    }
}