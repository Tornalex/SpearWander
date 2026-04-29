using UnityEngine;
using System.Collections.Generic;

public class PlayerCombat : MonoBehaviour
{
    [Header("Spear Inventory")]
    [SerializeField] private int maxSpears = 3;
    [SerializeField] public int currentSpears;

    [Header("Spear Physics")]
    [SerializeField] private float shootForce = 20f;
    [SerializeField] private GameObject spearPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform aimIndicator;

    private PlayerInputHandler _input;
    public List<Spear> activeSpears = new List<Spear>();

    void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        currentSpears = maxSpears;
    }

    void Update()
    {
        HandleAiming();
        if (_input.FireTriggered && currentSpears > 0) Fire();
        if (_input.RecallTriggered) RecallFirstSpear();
    }

    void HandleAiming()
    {
        Vector3 target = (Vector3)_input.AimInput;
        Vector2 dir = (target - transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        aimIndicator.rotation = Quaternion.Euler(0, 0, angle);
    }

    void Fire()
    {
        currentSpears--;
        GameObject s = Instantiate(spearPrefab, firePoint.position, aimIndicator.rotation);
        Spear spearScript = s.GetComponent<Spear>();
        activeSpears.Add(spearScript);

        s.layer = LayerMask.NameToLayer("Player");
        foreach (Transform child in s.transform)
        {
            child.gameObject.layer = LayerMask.NameToLayer("Player");
        }
        
        s.GetComponent<Rigidbody2D>().AddForce(aimIndicator.right * shootForce, ForceMode2D.Impulse);
    }

    public void RecallFirstSpear()
    {
        activeSpears.RemoveAll(item => item == null);

        if (activeSpears.Count > 0)
        {
            Spear firstSpear = activeSpears[0];
            
            if (firstSpear.currentState != Spear.SpearState.Returning)
            {
                firstSpear.StartReturn(transform, this);
                activeSpears.RemoveAt(0); 
            }
        }
    }

    public void CatchSpear(Spear spear)
    {
        currentSpears++;
        Destroy(spear.gameObject);
    }
}