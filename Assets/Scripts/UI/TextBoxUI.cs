using TMPro;
using UnityEngine;

public class TextBoxUI : MonoBehaviour
{
    public static TextBoxUI Instance { get; private set; }

    [SerializeField] private GameObject _root;
    [SerializeField] private TextMeshProUGUI _text;

    private Player _player;
    private string[] _pages;
    private int _currentPage;

    private bool _navigationLocked;

    private void Awake()
    {
        Instance = this;

        if (_root != null)
            _root.SetActive(false);
    }

    private void Update()
    {
        if (_player == null)
            return;

        if (_player.Input.CancelTriggered)
        {
            Close();
            return;
        }

        Vector2 navigation = _player.Input.NavigateInput;

        // Wait until the navigation input is released
        // before accepting another page change.
        if (navigation.x > 0.5f)
        {
            if (!_navigationLocked)
            {
                _navigationLocked = true;
                NextPage();
            }
        }
        else if (navigation.x < -0.5f)
        {
            if (!_navigationLocked)
            {
                _navigationLocked = true;
                PreviousPage();
            }
        }
        else
        {
            _navigationLocked = false;
        }
    }

    public void Open(Player player, string[] pages)
    {
        if (player == null || pages == null || pages.Length == 0)
            return;

        _player = player;
        _pages = pages;
        _currentPage = 0;
        _navigationLocked = false;

        _player.AddControlRequest(Player.ControlReason.TextBox);
        _player.Input.SwitchToUI();

        ShowCurrentPage();

        if (_root != null)
            _root.SetActive(true);
    }

    public void Close()
    {
        if (_player == null)
            return;

        if (_root != null)
            _root.SetActive(false);

        _player.RemoveControlRequest(Player.ControlReason.TextBox);
        _player.Input.SwitchToPlayer();

        _player = null;
        _pages = null;
        _currentPage = 0;
        _navigationLocked = false;
    }

    private void NextPage()
    {
        if (_pages == null)
            return;

        if (_currentPage >= _pages.Length - 1)
            return;

        _currentPage++;
        ShowCurrentPage();
    }

    private void PreviousPage()
    {
        if (_pages == null)
            return;

        if (_currentPage <= 0)
            return;

        _currentPage--;
        ShowCurrentPage();
    }

    private void ShowCurrentPage()
    {
        if (_text == null || _pages == null)
            return;

        _text.text = _pages[_currentPage];
    }
}