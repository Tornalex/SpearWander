using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool FireTriggered { get; private set; }
    public bool RecallTriggered { get; private set; }

    private PlayerInputActions _actions;

    void Awake()
    {
        _actions = new PlayerInputActions();
        _actions.Enable();
    }

    void Update()
    {
        MoveInput = _actions.Player.Move.ReadValue<Vector2>();
        
        // Unifichiamo il puntamento: se il mouse si muove usa quello, altrimenti il gamepad
        Vector2 mousePos = _actions.Player.AimWithMouse.ReadValue<Vector2>();
        if (mousePos != Vector2.zero) 
            AimInput = Camera.main.ScreenToWorldPoint(mousePos);
        else
            AimInput = _actions.Player.AimWithController.ReadValue<Vector2>();

        JumpTriggered = _actions.Player.Jump.WasPerformedThisFrame();
        FireTriggered = _actions.Player.Fire.WasPerformedThisFrame();
        RecallTriggered = _actions.Player.Recall.WasPerformedThisFrame();
    }

    public bool IsJumpHeld() => _actions.Player.Jump.IsPressed();
}