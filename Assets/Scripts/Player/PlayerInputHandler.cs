using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimInput { get; private set; }
    public Vector2 NavigateInput { get; private set; }

    public bool IsGamepad { get; private set; }

    public bool InteractTriggered { get; private set; }
    public bool CancelTriggered { get; private set; }

    public bool JumpTriggered { get; private set; }
    public bool HealTriggered { get; private set; }
    public bool DashTriggered { get; private set; }
    public bool FireTriggered { get; private set; }
    public bool RecallTriggered { get; private set; }
    public bool DownTriggered { get; private set; }

    public bool DownInputHeld => MoveInput.y < -0.5f;

    private PlayerInputActions _actions;

    private int _suppressInteractFrames;

    void Awake()
    {
        _actions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _actions.Player.Enable();
        _actions.UI.Disable();
    }

    private void OnDisable()
    {
        _actions.Disable();
    }

    void Update()
    {
        // Reset UI input every frame.
        NavigateInput = Vector2.zero;
        CancelTriggered = false;

        // Reset gameplay input every frame.
        InteractTriggered = false;
        JumpTriggered = false;
        HealTriggered = false;
        DashTriggered = false;
        FireTriggered = false;
        RecallTriggered = false;
        DownTriggered = false;

        if (_actions.Player.enabled)
        {
            MoveInput =
                _actions.Player.Move.ReadValue<Vector2>();

            Vector2 stickInput =
                _actions.Player.AimWithController.ReadValue<Vector2>();

            // ---------------------------------------------------------
            // AIM INPUT
            // ---------------------------------------------------------

            if (stickInput.sqrMagnitude > 0.1f)
            {
                AimInput = stickInput;
                IsGamepad = true;
            }
            else
            {
                // Manteniamo il dispositivo precedentemente utilizzato.
                // Se il controller è ancora il dispositivo attivo,
                // AimInput rimane neutro invece di passare al mouse.
                if (!IsGamepad)
                {
                    AimInput =
                        _actions.Player.AimWithMouse.ReadValue<Vector2>();
                }
                else
                {
                    AimInput = Vector2.zero;
                }
            }

            // ---------------------------------------------------------
            // INPUT TRIGGERED
            // ---------------------------------------------------------

            if (_suppressInteractFrames > 0)
            {
                _suppressInteractFrames--;
            }
            else
            {
                InteractTriggered =
                    _actions.Player.Interact.WasPerformedThisFrame();
            }

            JumpTriggered =
                _actions.Player.Jump.WasPerformedThisFrame();

            DashTriggered =
                _actions.Player.Dash.WasPerformedThisFrame();

            FireTriggered =
                _actions.Player.Fire.WasPerformedThisFrame();

            RecallTriggered =
                _actions.Player.Recall.WasPerformedThisFrame();

            DownTriggered =
                _actions.Player.Down.WasPerformedThisFrame();

            HealTriggered =
                _actions.Player.Heal.WasPerformedThisFrame();

            // ---------------------------------------------------------
            // FIRE DEVICE
            // ---------------------------------------------------------

            // Se il Fire è stato premuto in questo frame,
            // usiamo il dispositivo che ha effettivamente generato
            // l'input.
            if (FireTriggered)
            {
                InputControl fireControl =
                    _actions.Player.Fire.activeControl;

                if (fireControl != null)
                {
                    IsGamepad =
                        fireControl.device is Gamepad;
                }

                // Se il Fire arriva dal controller e lo stick
                // è neutro, la mira deve rimanere neutra.
                if (IsGamepad &&
                    stickInput.sqrMagnitude <= 0.1f)
                {
                    AimInput = Vector2.zero;
                }
                else if (!IsGamepad)
                {
                    AimInput =
                        _actions.Player.AimWithMouse.ReadValue<Vector2>();
                }
            }
        }
        else
        {
            MoveInput = Vector2.zero;
            AimInput = Vector2.zero;
            IsGamepad = false;
        }

        if (_actions.UI.enabled)
        {
            NavigateInput =
                _actions.UI.Navigate.ReadValue<Vector2>();

            CancelTriggered =
                _actions.UI.Cancel.WasPerformedThisFrame();
        }
    }

    public void SwitchToUI()
    {
        _actions.Player.Disable();
        _actions.UI.Enable();
    }

    public void SwitchToPlayer()
    {
        _actions.UI.Disable();
        _actions.Player.Enable();

        // Prevent the same E press used to close the textbox
        // from immediately opening it again.
        _suppressInteractFrames = 1;
    }

    public bool IsRecallHeld() =>
        _actions.Player.Recall.IsPressed();

    public bool IsJumpHeld() =>
        _actions.Player.Jump.IsPressed();

    public bool IsDashHeld() =>
        _actions.Player.Dash.IsPressed();
    public bool IsFireHeld() =>
    _actions.Player.Fire.IsPressed();
}