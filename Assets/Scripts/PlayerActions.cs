using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerActions : MonoBehaviour
{
    [SerializeField] float movementSpeed = 0f;
    [SerializeField] float jumpHeight = 0f;

    [HideInInspector] public bool isFacingRight = true;
    bool isFalling = false;
    [HideInInspector] public Vector2 moveInput;
    float fallingSpeed;
    Rigidbody2D playerRb;
    SpriteRenderer playerSprite;
    SpearThrow spearThrow;
    SpearCollector spearCollector;
    PlayerFeet playerFeet;
    CameraFollowObject cameraFollowObject;
    private void Awake()
    {
        playerRb = GetComponent<Rigidbody2D>();
        playerSprite = GetComponent<SpriteRenderer>();
        spearThrow = FindObjectOfType<SpearThrow>();
        playerFeet = FindObjectOfType<PlayerFeet>();
        spearCollector = FindObjectOfType<SpearCollector>();
        cameraFollowObject = FindObjectOfType<CameraFollowObject>();
    }
    private void FixedUpdate()
    {
        fallingSpeed = playerRb.velocity.y;
        Move();
        CheckFallingSpeed();
        if (moveInput.x > 0 || moveInput.x < 0)
        {
            CheckTurn();
        }
    }
    void Move()
    {
        Vector2 playerVelocity = new(moveInput.x * movementSpeed, playerRb.velocity.y);
        playerRb.velocity = playerVelocity;
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
    void CheckFallingSpeed()
    {
        if (fallingSpeed > 0.05f)
        {
            isFalling = true;
        }
        else
        {
            isFalling = false;
        }
    }
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    void OnJump()
    {
        if (playerFeet.canJump && !isFalling)
        {
            GetComponent<Rigidbody2D>().AddForce(Vector2.up * jumpHeight, ForceMode2D.Impulse);
        }
    }
    void OnFire()
    {
        spearThrow.Fire();
    }
    void OnPickup()
    {
        if(spearCollector.isTouchingSpear)
        {
            spearCollector.DestroySpear();
            spearThrow.currentlyEquippedSpears++;
        }
    }
}