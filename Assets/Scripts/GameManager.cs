using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GameManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] PlayerActions playerActions;
    [SerializeField] GameObject deathScreen;
    [SerializeField] GameObject respawnPoint;

    void Update()
    {
        ShowDeathScreen();
    }
    
    public void Respawn()
    {
        playerActions.isDead = false;
        playerActions.transform.position = respawnPoint.transform.position;
        deathScreen.SetActive(false);
    }
    
    void ShowDeathScreen()
    {
        if(playerActions.isDead)
        {
            deathScreen.SetActive(true);
        }
    }

    public void Quit()
    {
        Application.Quit();
    }
}
