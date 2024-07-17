using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrowAndRecallSpears : MonoBehaviour
{
    [Header("Spear Stats")]
    public int equippedSpears;
    public Queue<GameObject> thrownSpearsQueue = new Queue<GameObject>();

    [Header("Components")]
    [SerializeField] GameObject spear;
    [SerializeField] Transform spearTransform;

    Camera mainCam;
    Vector3 mousePos;

    private void Awake()
    {
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }
    private void Update()
    {
        CheckMousePosition();
    }

    void CheckMousePosition()
    {
        mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 spearDirection =  mousePos - transform.position;
        float rotZ = Mathf.Atan2(spearDirection.y, spearDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, rotZ);
    }
    public void Fire()
    {
        if(equippedSpears > 0)
        {
            equippedSpears--;
            GameObject thrownSpear = Instantiate(spear, spearTransform.position, Quaternion.identity);
            thrownSpearsQueue.Enqueue(thrownSpear);
        }
    }

    public void Recall()
    {
        GameObject recalledSpear = thrownSpearsQueue.Dequeue();
        Destroy(recalledSpear);
        equippedSpears++;
    }    
}