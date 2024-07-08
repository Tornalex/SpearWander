using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpearThrow : MonoBehaviour
{
    [Header("Spear Stats")]
    public int spearsAvailable;

    [Header("Components")]
    [SerializeField] GameObject spear;
    [SerializeField] Transform bulletTransform;

    Camera mainCam;
    Vector3 mousePos;

    private void Awake()
    {
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }
    private void Update()
    {
        CheckDirection();
    }

    void CheckDirection()
    {
        mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 spearDirection =  mousePos - transform.position;
        float rotZ = Mathf.Atan2(spearDirection.y, spearDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, rotZ);
    }
    public void Fire()
    {
        if(spearsAvailable > 0)
        {
            Instantiate(spear, bulletTransform.position, Quaternion.identity);
            spearsAvailable--;
        }
    }
}
