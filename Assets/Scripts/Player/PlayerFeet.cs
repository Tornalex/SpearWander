using UnityEngine;
using System.Collections.Generic;

public class PlayerFeet : MonoBehaviour
{
    private bool _isGrounded;
    private Player _player;
    private HashSet<int> _groundColliders = new HashSet<int>();

    void Awake()
    {
        _player = GetComponentInParent<Player>();
    }

    public bool IsGrounded() => _isGrounded;

    private void OnCollisionStay2D(Collision2D collision)
    {
        bool isGround = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.normal.y > 0.9f && contact.point.y <= transform.position.y + 0.2f)
            {
                isGround = true;
                break;
            }
        }
        if (!isGround) return;

        _groundColliders.Add(collision.collider.GetEntityId());
        _isGrounded = true;

        if (_player != null && _player.Pogo != null && _player.Pogo.IsPlunging)
        {
            _player.Pogo.OnPogoHit(collision);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        _groundColliders.Remove(collision.collider.GetEntityId());
        _isGrounded = _groundColliders.Count > 0;
    }
}