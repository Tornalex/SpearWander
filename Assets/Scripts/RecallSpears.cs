using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecallSpears : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] PlayerInputs playerInputs;

    public void Recall()
    {
        while (playerInputs.thrownSpearsQueue.Count > 0 && playerInputs.thrownSpearsQueue.Peek() == null)
        {
            playerInputs.thrownSpearsQueue.Dequeue();
            playerInputs.equippedSpears++;
        }

        if (playerInputs.thrownSpearsQueue.Count > 0)
        {
            GameObject recalledSpear = playerInputs.thrownSpearsQueue.Dequeue();
            Destroy(recalledSpear);
            playerInputs.equippedSpears++;
        }
    }
}