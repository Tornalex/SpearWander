using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerActions : MonoBehaviour
{
    [SerializeField] float movementSpeed = 0f;
    [SerializeField] float jumpHeight = 0f;
    [SerializeField] GameObject spearObject;

    private bool isTouchingGround = true;
    Rigidbody2D playerRb;
    Vector2 moveInput;
    PlayerInventory playerInventory;
    private void Awake()
    {
        playerRb = GetComponent<Rigidbody2D>();
        playerInventory = FindObjectOfType<PlayerInventory>();
    }
    private void FixedUpdate()
    {
        Move();
    }
    void Move()
    {
        Vector2 playerVelocity = new(moveInput.x * movementSpeed,
        playerRb.velocity.y);
        playerRb.velocity = playerVelocity;
    }
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.transform.CompareTag("Ground"))
        {
            isTouchingGround = true;
        }
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        isTouchingGround = false;
    }
    void OnJump()
    {
        if (isTouchingGround)
        {
            GetComponent<Rigidbody2D>().AddForce(Vector2.up * jumpHeight, ForceMode2D.Impulse);
        }
    }
    void OnFire()
    {
        playerInventory.Fire();
    }
}