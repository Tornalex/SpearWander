using UnityEngine;
using Unity.Cinemachine;

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

    [Header("V2 Spear System")]
    [SerializeField] private bool useV2SpearSystem = false;
    public PlayerCombatV2 CombatV2 { get; private set; }
    public PlayerRopeClimb RopeClimb { get; private set; }

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
        CombatV2 = GetComponent<PlayerCombatV2>();
        RopeClimb = GetComponent<PlayerRopeClimb>();

        if (useV2SpearSystem)
        {
            Combat.enabled = false;
            if (CombatV2 != null) CombatV2.enabled = true;
            if (RopeClimb != null) RopeClimb.enabled = true;
        }
        else
        {
            if (CombatV2 != null) CombatV2.enabled = false;
            if (RopeClimb != null) RopeClimb.enabled = false;
        }
    }
}
