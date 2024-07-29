using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThrowAndRecallSpears : MonoBehaviour
{
    [Header("Spear Stats")]
    public int equippedSpears;
    [SerializeField] int spearSpeed;

    [Header("Components")]
    [SerializeField] PlayerActions playerActions;
    [SerializeField] GameObject spearObject;
    [SerializeField] Transform spearTransform;

    [HideInInspector] public Queue<GameObject> thrownSpearsQueue = new Queue<GameObject>();
    Camera mainCam;
    Vector3 mousePos;

    private void Awake()
    {
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        
    }
    private void Update()
    {
        AimwithMouse();
    }

    void AimwithMouse()
    {
        mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 spearDirection =  mousePos - transform.position;
        float rotZ = Mathf.Atan2(spearDirection.y, spearDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, rotZ);
    }
    public void AimWithController(Vector3 inputVector)
    {
        Vector3 spearDirection = inputVector;
        float rotZ = Mathf.Atan2(spearDirection.y, spearDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, rotZ);
    }

    public void FireWithMouse()
    {
        if(equippedSpears > 0)
        {
            equippedSpears--;
            GameObject thrownSpear = Instantiate(spearObject, spearTransform.position, Quaternion.identity);
            thrownSpearsQueue.Enqueue(thrownSpear);
        }
    }
    public void FireWithController()
    {
        if (equippedSpears > 0)
        {
            equippedSpears--;
            GameObject thrownSpear = Instantiate(spearObject, spearTransform.position, Quaternion.identity);
            Vector3 direction = playerActions.playerInputActions.Player.Aim.ReadValue<Vector2>();
            thrownSpear.GetComponent<Rigidbody2D>().velocity = new Vector2(direction.x, direction.y).normalized * spearSpeed;
            thrownSpearsQueue.Enqueue(thrownSpear);
        }
    }

    public void Recall()
    {
        GameObject recalledSpear = thrownSpearsQueue.Dequeue();
        Destroy(recalledSpear);
        equippedSpears++;
    }    

    void SetSpearDirectionWithMouse()
    {

    }

    void SetSpearDirectionWithController()
    {

    }    
}
