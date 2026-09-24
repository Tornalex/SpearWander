using UnityEngine;

public class TextInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string promptMessage = "Leggi";

    [Header("Text")]
    [TextArea(3, 8)]
    [SerializeField] private string[] pages;

    public void Interact(Player player)
    {
        if (TextBoxUI.Instance == null)
        {
            Debug.LogWarning("[TextInteractable] TextBoxUI not found in the scene.");
            return;
        }

        TextBoxUI.Instance.Open(player, pages);
    }

    public string GetInteractPrompt()
    {
        return promptMessage;
    }
}