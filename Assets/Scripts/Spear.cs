using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spear : MonoBehaviour
{
    [Header("Spear Stats")]
    [SerializeField] int spearSpeed;

    [Header("Components")]
    [SerializeField] Vector3 mousePos;
    
    SpearThrow spearThrow;
    [HideInInspector] public Rigidbody2D spearRb;
    Enemy enemy;
    Camera mainCam;

    void Awake()
    {
        spearThrow = FindObjectOfType<SpearThrow>();
        spearRb = GetComponent<Rigidbody2D>();
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }
    private void Start()
    {
        Setdirection();
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
    private void OnMouseOver()
    {
        if (Input.GetMouseButtonDown(1) && gameObject.CompareTag("Spear"))
        {
            spearThrow.spearsAvailable++;
            Destroy(gameObject);
        }
    }
    void Setdirection()
    {
        mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 direction = mousePos - transform.position;
        spearRb.velocity = new Vector2(direction.x, direction.y).normalized * spearSpeed;
    }
}
