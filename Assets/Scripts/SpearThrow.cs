using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpearThrow : MonoBehaviour
{
    [SerializeField] float spearSpeed = 0f;
    [SerializeField] float spearHeightForce = 0f;
    [SerializeField] GameObject spearObject;
    public int currentlyEquippedSpears = 3;

    float spearOffset = 0f;
    float spearDirection;
    float spearSpeedDirection;
    Vector2 spearSpawnPosition;
    [SerializeField] PlayerActions playerActions;
    Spear spearScript;
    private void Update()
    {
        spearSpawnPosition = new(transform.position.x + spearOffset, transform.position.y);
        CheckDirection();
    }
    public void SetSpearDirection(float value)
    {
        Vector2 spearVelocity = new(spearSpeed * value, spearHeightForce);
        spearScript.spearRb.AddForce(spearVelocity, ForceMode2D.Impulse);
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
            Instantiate(spearObject, spearSpawnPosition, Quaternion.Euler(0, spearDirection, 0));
            spearScript = FindObjectOfType<Spear>();
            SetSpearDirection(spearSpeedDirection);
            currentlyEquippedSpears--;
        }
    }
}
