using UnityEngine;
using Unity.Cinemachine;

public class CameraPriorityChanger : MonoBehaviour
{
    [SerializeField] private CinemachineCamera movementCamera;
    [SerializeField] private CinemachineCamera defaultCamera;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            movementCamera.Priority = 10;
            defaultCamera.Priority = 5;
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            movementCamera.Priority = 5;
            defaultCamera.Priority = 10;
        }
    }
}
