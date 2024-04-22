using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFeet : MonoBehaviour
{
    public bool canJump = true;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        canJump = true;   
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        canJump = false;
    }
}