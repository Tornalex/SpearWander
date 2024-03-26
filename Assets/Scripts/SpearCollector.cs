using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpearCollector : MonoBehaviour
{
    public bool isTouchingSpear = false;
    SpearThrow spearThrow;
    private GameObject spearToCollect;
    private void Awake()
    {
        spearThrow = FindObjectOfType<SpearThrow>();
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if(collision.CompareTag("Spear"))
        {
            isTouchingSpear = true;
            spearToCollect = collision.gameObject;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Spear"))
        {
            isTouchingSpear = false;
            spearToCollect = null;
        }
    }
    public void DestroySpear()
    {
        Destroy(spearToCollect);
    }
}
