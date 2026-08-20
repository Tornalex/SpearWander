using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class Player : MonoBehaviour
{
    // --- Unity Standard Components ---
    public Rigidbody2D Rb { get; private set; }
    public Collider2D Collider { get; private set; }
    public SpriteRenderer Sprite { get; private set; }
    public Animator Animator { get; private set; }
    public CinemachineImpulseSource ImpulseSource { get; private set; }

    // --- Custom Player Components ---
    public PlayerInputHandler Input { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public PlayerJump Jump { get; private set; }
    public PlayerDash Dash { get; private set; }
    public PlayerPogo Pogo { get; private set; }
    public PlayerCombat Combat { get; private set; }
    public PlayerHealth Health { get; private set; }
    public PlayerKnockback Knockback { get; private set; }
    public PlayerRopeClimb RopeClimb { get; private set; }
    public PlayerEssence Essence { get; private set; }

    // --- HasControl System ---
    public enum ControlReason { Death, Respawn, Cutscene, Transition, Pickup, Pause }

    private HashSet<ControlReason> _controlReasons = new();

    public bool HasControl => _controlReasons.Count == 0;

    public void AddControlRequest(ControlReason reason) => _controlReasons.Add(reason);
    public void RemoveControlRequest(ControlReason reason) => _controlReasons.Remove(reason);

    [Header("Scriptable Objects")]
    [SerializeField] private PlayerStatsData playerStats;
    [SerializeField] private PlayerCombatData combatStats;
    [SerializeField] private PlayerEssenceData essenceStats;
    [SerializeField] private PlayerDashData dashStats;
    [SerializeField] private PlayerRopeData ropeStats;
    [SerializeField] private PlayerPogoData pogoStats;

    public PlayerStatsData PlayerStats => playerStats;
    public PlayerCombatData CombatStats => combatStats;
    public PlayerEssenceData EssenceStats => essenceStats;
    public PlayerDashData DashStats => dashStats;
    public PlayerRopeData RopeStats => ropeStats;
    public PlayerPogoData PogoStats => pogoStats;

    void Awake()
    {
        // Cache Unity standard components
        Rb = GetComponent<Rigidbody2D>();
        Collider = GetComponent<Collider2D>();
        Sprite = GetComponentInChildren<SpriteRenderer>();
        Animator = GetComponentInChildren<Animator>();
        ImpulseSource = GetComponent<CinemachineImpulseSource>();

        // Cache custom components
        Input = GetComponent<PlayerInputHandler>();
        Movement = GetComponent<PlayerMovement>();
        Jump = GetComponent<PlayerJump>();
        Dash = GetComponent<PlayerDash>();
        Pogo = GetComponent<PlayerPogo>();
        Combat = GetComponent<PlayerCombat>();
        Health = GetComponent<PlayerHealth>();
        Knockback = GetComponent<PlayerKnockback>();
        RopeClimb = GetComponent<PlayerRopeClimb>();
        Essence = GetComponent<PlayerEssence>();
    }

    public void ResetState()
    {
        Health.ResetState();
        Essence.ResetState();
        Combat.ResetState();
        _controlReasons.Clear();
    }
}
