using UnityEngine;
using SpearWander.Abilities;

public class EssenceWell : MonoBehaviour, IInteractable
{
    public void Interact(Player player)
    {
        string currentRoom = GameSceneManager.Instance.CurrentRoom;
        Debug.Log($"[EssenceWell.Interact] currentRoom='{currentRoom}'");
        CheckpointManager.Instance.SetCheckpoint(currentRoom, transform.position);

        player.Health.Heal(player.Health.MaxHealth);
        player.Essence.Refill();

        Debug.Log($"[EssenceWell.Interact] Calling ClearAll to respawn all enemies");
        DeadEnemyTracker.ClearAll();

        GameSceneManager.Instance.ReloadCurrentRoom(resetState: false, overrideSpawn: transform.position);
    }

    public string GetInteractPrompt()
    {
        return "E";
    }
}
