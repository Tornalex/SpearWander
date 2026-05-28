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

    private Player _player;
    private int _cooldownTimer;
    private bool _isSpearReturning;
    private bool _waitingForRecallRelease;
    private Spear _returningSpear;
    private Camera _mainCam;
    [HideInInspector] public List<Spear> activeSpears = new List<Spear>();
    public int MaxSpears => maxSpears;

    void Awake()
    {
        _player = GetComponent<Player>();
        _mainCam = Camera.main;
        currentSpears = maxSpears;
    }

    void Update()
    {
        HandleAiming();
        
        if (_player.Input.FireTriggered && currentSpears > 0 && _cooldownTimer <= 0)
        {
            Fire();
        }

        if (!_player.Input.IsRecallHeld())
        {
            _waitingForRecallRelease = false;
            
            if (_isSpearReturning && _returningSpear != null)
            {
                AbortRecall();
            }
        }

        if (_player.Input.IsRecallHeld() && !_isSpearReturning && !_waitingForRecallRelease)
        {
            RecallFirstSpear();
        }
    }

    void FixedUpdate()
    {
        if (_cooldownTimer > 0) _cooldownTimer--;
    }

    void HandleAiming()
    {
        Vector2 dir = Vector2.zero;

        if (_player.Input.IsGamepad)
        {
            dir = _player.Input.AimInput;
        }
        else
        {
            Vector3 mousePos = _mainCam.ScreenToWorldPoint(new Vector3(_player.Input.AimInput.x, _player.Input.AimInput.y, -_mainCam.transform.position.z));
            dir = ((Vector2)mousePos - (Vector2)transform.position).normalized;
        }

        if (dir.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            aimIndicator.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void Fire()
    {
        currentSpears--;
        _cooldownTimer = throwCooldownFrames;

        GameObject s = Instantiate(spearPrefab, firePoint.position, aimIndicator.rotation);
        Spear spearScript = s.GetComponentInChildren<Spear>();
        activeSpears.Add(spearScript);

        spearScript.Initialize(_player.Collider);
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
                _waitingForRecallRelease = true;

                _returningSpear.StartReturn(transform);

                _returningSpear.OnSpearReturned += CatchSpear;

                activeSpears.RemoveAt(0); 
            }
        }
    }

    private void AbortRecall()
    {
        _returningSpear.OnSpearReturned -= CatchSpear;

        _returningSpear.AbortReturn();
        activeSpears.Insert(0, _returningSpear);
        _returningSpear = null;
        _isSpearReturning = false;
    }

    public void CatchSpear(Spear spear)
    {
        spear.OnSpearReturned -= CatchSpear;

        currentSpears++;
        _isSpearReturning = false;
        _returningSpear = null;

        if (spear.HasHitEnemy)
        {
            if (_player.Health != null)
            {
                _player.Health.AddEssenceFromCatch(false); 
            }
        }
        
        Destroy(spear.gameObject);
    }

    public void IncreaseMaxSpears(int amount)
    {
        maxSpears += amount;
        currentSpears += amount;
        Debug.Log($"[COMBAT] Nuova capacità massima sbloccata! Max: {maxSpears}, Attuali: {currentSpears}");
    }
}