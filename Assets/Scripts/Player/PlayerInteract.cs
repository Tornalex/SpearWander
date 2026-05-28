using UnityEngine;
using System.Collections.Generic;

public class PlayerInteract : MonoBehaviour
{
    private Player _player;
    private List<IInteractable> _interactablesInRange = new List<IInteractable>();

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void Update()
    {
        if (_player.Input.InteractTriggered && _interactablesInRange.Count > 0)
        {
            IInteractable target = _interactablesInRange[_interactablesInRange.Count - 1];
            if (target != null)
            {
                Debug.Log("Interazione eseguita con successo!");
                target.Interact(_player);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        IInteractable interactable = collision.GetComponent<IInteractable>();
        if (interactable != null && !_interactablesInRange.Contains(interactable))
        {
            _interactablesInRange.Add(interactable);
            Debug.Log(interactable.GetInteractPrompt()); 
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        IInteractable interactable = collision.GetComponent<IInteractable>();
        if (interactable != null && _interactablesInRange.Contains(interactable))
        {
            _interactablesInRange.Remove(interactable);
        }
    }

    public void RemoveInteractableFromList(IInteractable interactable)
    {
        if (_interactablesInRange.Contains(interactable))
        {
            _interactablesInRange.Remove(interactable);
        }
    }
}