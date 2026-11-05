
using UnityEngine;
using Unity.Cinemachine;
using Yarn.Unity;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera))]
[RequireComponent(typeof(CinemachinePositionComposer))]
public class DialogueCameraController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private int inactivePriority = 0;
    [SerializeField] private int activePriority = 20;

    [Header("Yarn Spinner")]
    [SerializeField] private DialogueRunner dialogueRunner;

    private CinemachineCamera dialogueCamera;
    private CinemachinePositionComposer positionComposer;
    private Transform currentTarget;

    private void Awake()
    {
        dialogueCamera = GetComponent<CinemachineCamera>();
        positionComposer = GetComponent<CinemachinePositionComposer>();

        if (dialogueRunner == null)
        {
            Debug.LogError(
                "DialogueCameraController: DialogueRunner reference is missing.",
                this
            );
            return;
        }

        dialogueRunner.AddCommandHandler<GameObject, float>(
            "camera_focus",
            HandleCameraFocus
        );

        dialogueRunner.AddCommandHandler(
            "camera_restore",
            RestoreGameplayCamera
        );
    }

    private void Update()
    {
        if (currentTarget == null &&
            dialogueCamera.Priority != inactivePriority)
        {
            RestoreGameplayCamera();
        }
    }

    private void HandleCameraFocus(GameObject target, float cameraDistance)
    {
        if (target == null)
        {
            Debug.LogWarning(
                "DialogueCameraController: Yarn camera target not found.",
                this
            );
            return;
        }

        FocusOn(target.transform, cameraDistance);
    }

    public void FocusOn(Transform target, float cameraDistance)
    {
        if (target == null)
            return;

        currentTarget = target;

        CameraTarget cameraTarget = dialogueCamera.Target;
        cameraTarget.TrackingTarget = target;
        dialogueCamera.Target = cameraTarget;

        positionComposer.CameraDistance =
            Mathf.Max(0.1f, cameraDistance);

        dialogueCamera.Priority = activePriority;
    }

    public void RestoreGameplayCamera()
    {
        currentTarget = null;

        CameraTarget cameraTarget = dialogueCamera.Target;
        cameraTarget.TrackingTarget = null;
        dialogueCamera.Target = cameraTarget;

        dialogueCamera.Priority = inactivePriority;
    }
}
