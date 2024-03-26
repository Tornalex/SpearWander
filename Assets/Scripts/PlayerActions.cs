using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerActions : MonoBehaviour
{
    [SerializeField] float movementSpeed = 0f;
    [SerializeField] float jumpHeight = 0f;

    bool isFalling = false;
    Rigidbody2D playerRb;
    Vector2 moveInput;
    Vector2 fallingSpeed;
    SpearThrow spearThrow;
    SpearCollector spearCollector;
    PlayerFeet playerFeet;
    private void Awake()
    {
        playerRb = GetComponent<Rigidbody2D>();
        spearThrow = FindObjectOfType<SpearThrow>();
        playerFeet = FindObjectOfType<PlayerFeet>();
        spearCollector = FindObjectOfType<SpearCollector>();
    }
    private void FixedUpdate()
    {
        Move();
        fallingSpeed = playerRb.velocity;
        CheckFallingSpeed();

    }
    private void Update()
    {
    }
    void Move()
    {
        Vector2 playerVelocity = new(moveInput.x * movementSpeed, playerRb.velocity.y);
        playerRb.velocity = playerVelocity;
    }
    void CheckFallingSpeed()
    {
        if (fallingSpeed.y > 0.05f)
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