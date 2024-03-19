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
    Vector2 fallingSpeedCheck = Vector2.zero;
    SpearThrow spearThrow;
    PlayerFeet playerFeet;
    private void Awake()
    {
        playerRb = GetComponent<Rigidbody2D>();
        spearThrow = FindObjectOfType<SpearThrow>();
        playerFeet = FindObjectOfType<PlayerFeet>();
    }
    private void FixedUpdate()
    {
        Move();
        CheckFallingSpeed();
    }
    void Move()
    {
        Vector2 playerVelocity = new(moveInput.x * movementSpeed, playerRb.velocity.y);
        playerRb.velocity = playerVelocity;
    }
    void CheckFallingSpeed()
    {
        Vector2 playerFallingSpeed = new(playerRb.velocity.x, playerRb.velocity.y);
        if(playerFallingSpeed.y <= fallingSpeedCheck.y)
        {
            isFalling = false;
        }
        else
        {
            isFalling = true;
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
}