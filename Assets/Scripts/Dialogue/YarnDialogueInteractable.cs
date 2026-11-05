using UnityEngine;
using Yarn.Unity;

public class YarnDialogueInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string dialogueNode = "Start";

    private DialogueRunner dialogueRunner;

    private void Awake()
    {
        Collider2D interactionCollider = GetComponent<Collider2D>();
        if (interactionCollider == null || !interactionCollider.isTrigger)
        {
            Debug.LogError(
                "YarnDialogueInteractable requires a Collider2D on the same GameObject configured as a trigger.",
                this
            );
        }

        dialogueRunner = FindFirstObjectByType<DialogueRunner>();
    }

    public void Interact(Player player)
    {
        if (string.IsNullOrWhiteSpace(dialogueNode))
        {
            Debug.LogWarning("YarnDialogueInteractable has no Yarn node configured.", this);
            return;
        }

        if (dialogueRunner == null)
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();

        if (dialogueRunner == null)
        {
            Debug.LogWarning("YarnDialogueInteractable could not find the scene's DialogueRunner.", this);
            return;
        }

        if (dialogueRunner.IsDialogueRunning)
            return;

        _ = dialogueRunner.StartDialogue(dialogueNode);
    }

    public string GetInteractPrompt() => "PARLA";
}
