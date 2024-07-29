using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    [Header("Spear Stats")]
    [SerializeField] int spearSpeed;

    [Header("Components")]
    [SerializeField] Vector3 mousePos;
    [SerializeField] PlayerActions playerActions;
    
    [HideInInspector] public Rigidbody2D spearRb;
    Enemy enemy;
    Camera mainCam;

    void Awake()
    {
        spearRb = GetComponent<Rigidbody2D>();
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }
    private void Start()
    {
        
    }

    private void Update()
    {
        if (enemy != null && enemy.isDead)
        {
            spearRb.isKinematic = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.transform.tag == "Wall"
           || collision.transform.tag == "Ground")
        {
            spearRb.velocity = Vector2.zero;
            spearRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        if (collision.transform.tag == "Enemy")
        {
            enemy = collision.gameObject.GetComponent<Enemy>();
            spearRb.isKinematic = true;
            transform.parent = collision.transform;   
        }
    }
    /*void SetSpearDirectionWithMouse()
    {
        mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 direction = mousePos - transform.position;
        spearRb.velocity = new Vector2(direction.x, direction.y).normalized * spearSpeed;
    }
    void SetSpearDirectionWithController()
    {
        Vector3 direction = playerActions.playerInputActions.Player.Aim.ReadValue<Vector2>();
        spearRb.velocity = new Vector2(direction.x, direction.y).normalized * spearSpeed;
    }*/
}
