using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpearThrow : MonoBehaviour
{
    [Header("Spear Stats")]
    [SerializeField] float spearSpeed = 0f;
    [SerializeField] float spearHeightForce = 0f;
    public int currentlyEquippedSpears = 3;
    
    [Header("Components")]
    [SerializeField] GameObject spearObject;
    [SerializeField] PlayerActions playerActions;
    Spear spearScript;

    float spearOffset = 0f;
    float spearDirection;
    float spearSpeedDirection;
    Vector2 spearSpawnPosition;
    
    private void Update()
    {
        spearSpawnPosition = new(transform.position.x + spearOffset, transform.position.y);
        CheckDirection();
    }
    
    void CheckDirection()
    {
        if (playerActions.moveInput.x > 0)
        {
            spearDirection = 0;
            spearSpeedDirection = 1;
            spearOffset = 1;
        }
        if (playerActions.moveInput.x < 0)
        {
            spearDirection = 180;
            spearSpeedDirection = -1;
            spearOffset = -1;
        }
    }
    
    public void Fire()
    {
        if (currentlyEquippedSpears > 0)
        {
            spearScript = Instantiate(spearObject, spearSpawnPosition, Quaternion.Euler(0, spearDirection, 0)).GetComponent<Spear>();
            //spearScript = FindObjectOfType<Spear>();
            SetSpearDirection(spearSpeedDirection);
            currentlyEquippedSpears--;
        }
    }

    public void SetSpearDirection(float value)
    {
        Vector2 spearVelocity = new(spearSpeed * value, spearHeightForce);
        spearScript.spearRb.AddForce(spearVelocity, ForceMode2D.Impulse);
    }
}
