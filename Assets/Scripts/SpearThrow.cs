using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpearThrow : MonoBehaviour
{
    public int currentlyEquippedSpears = 3;
    [SerializeField] GameObject spearObject;
    Vector2 playerPosition;
    Vector2 spearSpawnOffset = new(1, 0);
    Vector2 spearSpawnPosition;
    Spear spearScript;
    private void Update()
    {
        playerPosition = transform.position;
        spearSpawnPosition = new(playerPosition.x + spearSpawnOffset.x, playerPosition.y + spearSpawnOffset.y);
    }
    public void Fire()
    {
        if (currentlyEquippedSpears > 0)
        {
            Instantiate(spearObject, spearSpawnPosition, Quaternion.Euler(0, 0, 0));
            spearScript = FindObjectOfType<Spear>();
            spearScript.SpearThrow();
            currentlyEquippedSpears--;
        }
    }
}
