using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFeet : MonoBehaviour
{
    public bool isGrounded = true;
    [SerializeField] PlayerInputs playerInputs;
    private void OnTriggerStay2D(Collider2D collision)
    {
        isGrounded = true;   
        playerInputs.hasCoyoteJumped = false;
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        isGrounded = false;
    }
}