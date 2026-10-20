using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class PlayerInteract : MonoBehaviour
{
    private Player _player;
    private List<IInteractable> _interactablesInRange = new List<IInteractable>();
    private Transform _currentInteractable;
    private Camera _mainCamera;

    [SerializeField] private GameObject _interactPromptRoot;
    [SerializeField] private TextMeshProUGUI _interactPromptText;
    [SerializeField] private Vector2 _promptOffset = new Vector2(0, 1.5f);

    void Awake()
    {
        _player = GetComponent<Player>();
        _mainCamera = Camera.main;
    }

    void Update()
    {
        // The prompt is optional: interaction must still work when no
        // prompt root/text is assigned (for example in a room scene opened
        // without the Master scene).
        if (_interactPromptRoot != null &&
            _interactPromptRoot.activeSelf &&
            _currentInteractable != null &&
            _mainCamera != null)
        {
            Vector2 worldPos = (Vector2)_currentInteractable.position + _promptOffset;
            _interactPromptRoot.transform.position = _mainCamera.WorldToScreenPoint(worldPos);
        }

        if (_player.HasControl && _player.Input.InteractTriggered && _interactablesInRange.Count > 0)
        {
            // A destroyed interactable does not always raise OnTriggerExit2D.
            // Clean the list before using its last entry.
            if (RemoveDestroyedInteractables())
                UpdatePrompt();

            if (_interactablesInRange.Count > 0)
            {
                IInteractable target = _interactablesInRange[^1];
                if (target != null)
                {
                    target.Interact(_player);
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        IInteractable interactable = collision.GetComponent<IInteractable>();
        if (interactable != null && !_interactablesInRange.Contains(interactable))
        {
            _interactablesInRange.Add(interactable);
            UpdatePrompt();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        IInteractable interactable = collision.GetComponent<IInteractable>();
        if (interactable != null && _interactablesInRange.Contains(interactable))
        {
            _interactablesInRange.Remove(interactable);
            UpdatePrompt();
        }
    }

    private void UpdatePrompt()
    {
        // Interactables can be destroyed (pickups, enemies) without raising
        // OnTriggerExit2D. Remove those stale entries before using the list.
        RemoveDestroyedInteractables();

        if (_interactablesInRange.Count > 0)
        {
            IInteractable interactable = _interactablesInRange[^1];

            if (_interactPromptRoot == null)
            {
                _currentInteractable = null;
                return;
            }

            if (_interactPromptText != null)
                _interactPromptText.text = interactable.GetInteractPrompt();

            _currentInteractable = ((MonoBehaviour)interactable).transform;
            _interactPromptRoot.SetActive(true);

            if (_mainCamera != null)
            {
                Vector2 worldPos = (Vector2)_currentInteractable.position + _promptOffset;
                _interactPromptRoot.transform.position = _mainCamera.WorldToScreenPoint(worldPos);
            }
        }
        else
        {
            _currentInteractable = null;

            if (_interactPromptRoot != null)
                _interactPromptRoot.SetActive(false);
        }
    }

    private bool RemoveDestroyedInteractables()
    {
        bool removed = false;

        for (int i = _interactablesInRange.Count - 1; i >= 0; i--)
        {
            // Comparing an interface reference to null does not use Unity's
            // overloaded null check, so a destroyed component still looks
            // non-null. Check the underlying MonoBehaviour explicitly.
            MonoBehaviour behaviour = _interactablesInRange[i] as MonoBehaviour;

            if (behaviour == null)
            {
                _interactablesInRange.RemoveAt(i);
                removed = true;
            }
        }

        return removed;
    }

    public void RemoveInteractableFromList(IInteractable interactable)
    {
        if (_interactablesInRange.Contains(interactable))
        {
            _interactablesInRange.Remove(interactable);
            UpdatePrompt();
        }
    }
}
