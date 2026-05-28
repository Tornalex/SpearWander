using UnityEngine;

public class PlayerFeet : MonoBehaviour
{
    private bool _isGrounded;
    private Player _player;

    void Awake()
    {
        _player = GetComponentInParent<Player>();
    }

    public bool IsGrounded() => _isGrounded;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        _isGrounded = true;

        // Se il giocatore sta effettuando un pogo, passa la collisione al relativo script
        if (_player != null && _player.Pogo != null && _player.Pogo.IsPlunging)
        {
            _player.Pogo.OnPogoHit(collision);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        _isGrounded = false;
    }
}