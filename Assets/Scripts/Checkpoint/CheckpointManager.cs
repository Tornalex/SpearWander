using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    private Vector3 _checkpointPosition;
    private string _checkpointRoom;
    public bool HasCheckpoint { get; private set; }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    public void SetCheckpoint(string roomName, Vector3 position)
    {
        _checkpointRoom = roomName;
        _checkpointPosition = position;
        HasCheckpoint = true;
    }

    public (string room, Vector3 position) GetCheckpoint()
    {
        return (_checkpointRoom, _checkpointPosition);
    }
}
