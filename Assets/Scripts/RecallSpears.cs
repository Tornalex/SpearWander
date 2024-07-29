using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecallSpears : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] PlayerInputs playerInputs;
    public void Recall()
    {
        GameObject recalledSpear = playerInputs.thrownSpearsQueue.Dequeue();
        Destroy(recalledSpear);
        playerInputs.equippedSpears++;
    }
}
