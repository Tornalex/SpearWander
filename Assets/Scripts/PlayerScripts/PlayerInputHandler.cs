using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }
    public bool IsGamepad { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool DashTriggered { get; private set; }
    public bool FireTriggered { get; private set; }
    public bool RecallTriggered { get; private set; }
    public bool DownTriggered { get; private set; }
    public bool DownInputHeld => MoveInput.y < -0.5f;

    private PlayerInputActions _actions;

    void Awake()
    {
        _actions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _actions.Enable();
    }

    private void OnDisable()
    {
        _actions.Disable();
    }

    void Update()
    {
        MoveInput = _actions.Player.Move.ReadValue<Vector2>();

        Vector2 stickInput = _actions.Player.AimWithController.ReadValue<Vector2>();
        
        if (stickInput.sqrMagnitude > 0.1f)
        {
            AimInput = stickInput;
            IsGamepad = true;
        }
        else
        {
            AimInput = _actions.Player.AimWithMouse.ReadValue<Vector2>();
            IsGamepad = false;
        }

        JumpTriggered = _actions.Player.Jump.WasPerformedThisFrame();
        DashTriggered = _actions.Player.Dash.WasPerformedThisFrame();
        FireTriggered = _actions.Player.Fire.WasPerformedThisFrame();
        RecallTriggered = _actions.Player.Recall.WasPerformedThisFrame();
        DownTriggered = _actions.Player.Down.WasPerformedThisFrame();
    }

    public bool IsRecallHeld() => _actions.Player.Recall.IsPressed();
    public bool IsJumpHeld() => _actions.Player.Jump.IsPressed();
    public bool IsDashHeld() => _actions.Player.Dash.IsPressed();
}