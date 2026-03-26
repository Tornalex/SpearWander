using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireSpears : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] PlayerInputs playerInputs;
    [SerializeField] GameObject spearObject;
    [SerializeField] Transform spearTransform;

    public void FireWithMouse(Vector3 mousePos)
    {
        if (playerInputs.equippedSpears > 0)
        {
            playerInputs.equippedSpears--;
            Vector3 spearDirection = mousePos - transform.position;
            GameObject thrownSpear = Instantiate(spearObject, spearTransform.position, Quaternion.identity);
            Rigidbody2D rb = thrownSpear.GetComponent<Rigidbody2D>();
            rb.AddForce(spearDirection.normalized * playerInputs.spearSpeed, ForceMode2D.Impulse);
            playerInputs.thrownSpearsQueue.Enqueue(thrownSpear);
        }
    }

    public void FireWithGamepad(Vector3 direction)
    {
        if (playerInputs.equippedSpears > 0)
        {
            playerInputs.equippedSpears--;
            GameObject thrownSpear = Instantiate(spearObject, spearTransform.position, Quaternion.identity);
            Rigidbody2D rb = thrownSpear.GetComponent<Rigidbody2D>();
            rb.AddForce(direction.normalized * playerInputs.spearSpeed, ForceMode2D.Impulse);
            playerInputs.thrownSpearsQueue.Enqueue(thrownSpear);
        }
    }
}