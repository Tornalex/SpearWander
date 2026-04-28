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
        if (_input.RecallTriggered) RecallLastSpear();
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
        foreach (Transform child in gameObject.transform)
        {
            gameObject.layer = LayerMask.NameToLayer("Player");
        }
        s.GetComponent<Rigidbody2D>().AddForce(aimIndicator.right * shootForce, ForceMode2D.Impulse);
    }

public void RecallLastSpear()
{
    if (activeSpears.Count > 0)
    {
        Spear lastSpear = activeSpears[activeSpears.Count - 1];
        
        if (lastSpear.currentState != Spear.SpearState.Returning)
        {
            lastSpear.StartReturn(transform, this);
            activeSpears.Remove(lastSpear); 
        }
    }
}

public void CatchSpear(Spear spear)
{
    currentSpears++;
    Destroy(spear.gameObject);
}
}