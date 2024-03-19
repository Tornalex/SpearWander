using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plate : MonoBehaviour
{
    OpenDoor openDoor;
    void Awake()
    {
        openDoor = FindObjectOfType<OpenDoor>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.transform.tag == "Player")
        {
            openDoor.DoorOpener();
        }
    }
}