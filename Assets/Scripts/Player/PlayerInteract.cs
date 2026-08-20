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
        if (_interactPromptRoot.activeSelf && _currentInteractable != null && _mainCamera != null)
        {
            Vector2 worldPos = (Vector2)_currentInteractable.position + _promptOffset;
            _interactPromptRoot.transform.position = _mainCamera.WorldToScreenPoint(worldPos);
        }

        if (_player.Input.InteractTriggered && _interactablesInRange.Count > 0)
        {
            IInteractable target = _interactablesInRange[_interactablesInRange.Count - 1];
            if (target != null)
            {
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
        if (_interactablesInRange.Count > 0)
        {
            IInteractable interactable = _interactablesInRange[^1];
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
            _interactPromptRoot.SetActive(false);
            _currentInteractable = null;
        }
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