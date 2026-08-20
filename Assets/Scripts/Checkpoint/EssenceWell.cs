using UnityEngine;

public class EssenceWell : MonoBehaviour, IInteractable
{
    public void Interact(Player player)
    {
        string currentRoom = GameSceneManager.Instance.CurrentRoom;
        CheckpointManager.Instance.SetCheckpoint(currentRoom, transform.position);

        player.Health.Heal(player.Health.MaxHealth);
        player.Essence.Refill();

        GameSceneManager.Instance.ReloadCurrentRoom(resetState: false, overrideSpawn: transform.position);
    }

    public string GetInteractPrompt()
    {
        return "E";
    }
}
