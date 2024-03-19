using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerFeet : MonoBehaviour
{
    public bool canJump = false;
    private void OnCollisionEnter2D(Collision2D collision)
    {
        canJump = true;
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        canJump = false;
    }
}