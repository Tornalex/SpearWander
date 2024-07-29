using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFeet : MonoBehaviour
{
    public bool isGrounded = true;
    [SerializeField] PlayerActions playerActions;
    private void OnTriggerStay2D(Collider2D collision)
    {
        isGrounded = true;   
        playerActions.hasCoyoteJumped = false;
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        isGrounded = false;
    }
}