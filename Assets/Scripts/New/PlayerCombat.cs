using UnityEngine;
using System.Collections.Generic;

public class PlayerCombat : MonoBehaviour
{
    public int maxSpears = 3;
    public int currentSpears;
    [SerializeField] GameObject spearPrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] Transform aimIndicator;

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
        
        s.GetComponent<Rigidbody2D>().AddForce(aimIndicator.right * 20f, ForceMode2D.Impulse);
    }

    void RecallLastSpear()
    {
        if (activeSpears.Count > 0)
        {
            Spear last = activeSpears[activeSpears.Count - 1];
            activeSpears.Remove(last);
            Destroy(last.gameObject);
            currentSpears++;
        }
    }
}