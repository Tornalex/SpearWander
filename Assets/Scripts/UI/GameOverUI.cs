using UnityEngine;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject overlayRoot;

    public void Show()
    {
        if (overlayRoot != null) overlayRoot.SetActive(true);
    }

    public void Hide()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    public void OnRetry()
    {
        Hide();
        GameSceneManager.Instance.ReloadCurrentRoom();
    }
}
