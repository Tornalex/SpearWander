using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
public class GameManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] FireSpears fireSpears;
    [SerializeField] PlayerInputs playerInputs;
    [SerializeField] GameObject deathScreen;
    [SerializeField] GameObject respawnPoint;
    [SerializeField] TMP_Text spearsAvailableUI;

    void Update()
    {
        ShowDeathScreen();
        UpdateAvailableSpearsUI();
    }
    
    public void Respawn()
    {
        playerInputs.isDead = false;
        playerInputs.transform.position = respawnPoint.transform.position;
        deathScreen.SetActive(false);
    }
    
    void ShowDeathScreen()
    {
        if(playerInputs.isDead)
        {
            deathScreen.SetActive(true);
        }
    }

    public void Quit()
    {
        Application.Quit();
    }

    void UpdateAvailableSpearsUI()
    {
        spearsAvailableUI.text = "Spears: " + playerInputs.equippedSpears;
    }
}
