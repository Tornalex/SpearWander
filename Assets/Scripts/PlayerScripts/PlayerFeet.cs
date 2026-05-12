using UnityEngine;

public class PlayerFeet : MonoBehaviour
{
    private bool _isGrounded;
    private PlayerPogo _pogoScript;

    void Awake()
    {
        // Cerca lo script Pogo nell'oggetto padre (il Player)
        _pogoScript = GetComponentInParent<PlayerPogo>();
    }

    public bool IsGrounded() => _isGrounded;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        _isGrounded = true;

        // Se il giocatore sta effettuando un pogo, passa la collisione al relativo script
        if (_pogoScript != null && _pogoScript.IsPlunging)
        {
            _pogoScript.OnPogoHit(collision);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        _isGrounded = false;
    }
}