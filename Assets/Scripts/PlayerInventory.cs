using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public int currentlyEquippedSpears = 3;
    [SerializeField] GameObject spearObject;
    Vector2 playerPosition;
    Spear spearScript;
    private void Update()
    {
        playerPosition = transform.position;
    }
    public void Fire()
    {
        if (currentlyEquippedSpears > 0)
        {
            Instantiate(spearObject, playerPosition, Quaternion.Euler(0, 0, 0));
            spearScript = FindObjectOfType<Spear>();
            spearScript.SpearThrow();
            currentlyEquippedSpears--;
        }
    }
}
