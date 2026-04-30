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

    [Header("Cooldown Settings")]
    [SerializeField] private int throwCooldownFrames = 25;

    private PlayerInputHandler _input;
    private int _cooldownTimer;
    private bool _isSpearReturning;
    private Spear _returningSpear;
    private Collider2D _playerCollider;
    public List<Spear> activeSpears = new List<Spear>();

    void Awake()
    {
        _input = GetComponent<PlayerInputHandler>();
        _playerCollider = GetComponent<Collider2D>();
        currentSpears = maxSpears;
    }

    void Update()
    {
        HandleAiming();
        
        if (_input.FireTriggered && currentSpears > 0 && _cooldownTimer <= 0)
        {
            Fire();
        }

        if (_input.IsRecallHeld() && !_isSpearReturning)
        {
            RecallFirstSpear();
        }
        else if (!_input.IsRecallHeld() && _isSpearReturning && _returningSpear != null)
        {
            AbortRecall();
        }
    }

    void FixedUpdate()
    {
        if (_cooldownTimer > 0)
        {
            _cooldownTimer--;
        }
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
        _cooldownTimer = throwCooldownFrames;

        GameObject s = Instantiate(spearPrefab, firePoint.position, aimIndicator.rotation);
        Spear spearScript = s.GetComponent<Spear>();
        activeSpears.Add(spearScript);

        spearScript.Initialize(_playerCollider);
        
        s.GetComponent<Rigidbody2D>().AddForce(aimIndicator.right * shootForce, ForceMode2D.Impulse);
    }

    public void RecallFirstSpear()
    {
        activeSpears.RemoveAll(item => item == null);

        if (activeSpears.Count > 0)
        {
            _returningSpear = activeSpears[0];
            
            if (_returningSpear.currentState != Spear.SpearState.Returning)
            {
                _isSpearReturning = true;
                _returningSpear.StartReturn(transform, this);
                activeSpears.RemoveAt(0); 
            }
        }
    }

    private void AbortRecall()
    {
        _returningSpear.AbortReturn();
        activeSpears.Insert(0, _returningSpear);
        _returningSpear = null;
        _isSpearReturning = false;
    }

    public void CatchSpear(Spear spear)
    {
        currentSpears++;
        _isSpearReturning = false;
        _returningSpear = null;
        Destroy(spear.gameObject);
    }
}