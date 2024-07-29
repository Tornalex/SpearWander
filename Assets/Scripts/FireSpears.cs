using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions.Must;

public class FireSpears : MonoBehaviour
{
    [Header("Spear Stats")]

    [Header("Components")]
    [SerializeField] PlayerInputs playerInputs;
    [SerializeField] GameObject spearObject;
    [SerializeField] Transform spearTransform;

    public void FireWithMouse(Vector3 mousePos)
    {
        if(playerInputs.equippedSpears > 0)
        {
            playerInputs.equippedSpears--;
            Vector3 spearDirection = mousePos - transform.position;
            GameObject thrownSpear = Instantiate(spearObject, spearTransform.position, Quaternion.identity);
            thrownSpear.GetComponent<Rigidbody2D>().velocity = new Vector2 (spearDirection.x, spearDirection.y).normalized * playerInputs.spearSpeed;
            playerInputs.thrownSpearsQueue.Enqueue(thrownSpear);
        }
    }
    public void FireWithGamepad(Vector3 direction)
    {
        if (playerInputs.equippedSpears > 0)
        {
            playerInputs.equippedSpears--;
            GameObject thrownSpear = Instantiate(spearObject, spearTransform.position, Quaternion.identity);
            thrownSpear.GetComponent<Rigidbody2D>().velocity = new Vector2(direction.x, direction.y).normalized * playerInputs.spearSpeed;
            playerInputs.thrownSpearsQueue.Enqueue(thrownSpear);
        }
    }
}
