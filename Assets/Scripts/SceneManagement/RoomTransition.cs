using UnityEngine;

public class RoomTransition : MonoBehaviour
{
    [Header("Questa Porta")]
    [Tooltip("L'ID univoco di QUESTA porta all'interno di questa stanza (es: Porta_A, Porta_B)")]
    [SerializeField] private string doorID;

    [Header("Spawn")]
    [Tooltip("Child della porta dove il player appare quando arriva in questa stanza")]
    [SerializeField] private Transform _spawnPoint;

    [Header("Destinazione")]
    [Tooltip("Il nome esatto della scena Unity da caricare")]
    [SerializeField] private string targetSceneName;
    [Tooltip("L'ID della porta su cui il player deve apparire nella prossima scena")]
    [SerializeField] private string targetDoorID;

    public string DoorID => doorID;
    public Transform SpawnPoint => _spawnPoint;
    public string TargetSceneName => targetSceneName;
    public string TargetDoorID => targetDoorID;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            GameSceneManager.Instance.TransitionToRoom(targetSceneName, targetDoorID);
        }
    }
}