using UnityEngine;
using System.Collections.Generic;

public class PlayerFeet : MonoBehaviour
{
    private struct GroundContact
    {
        public Collider2D Collider;
        public Vector2 LastPosition;
        public Vector2 Displacement;
    }

    private bool _isGrounded;
    private Player _player;
    private Dictionary<EntityId, GroundContact> _groundContacts =
        new Dictionary<EntityId, GroundContact>();

    public Vector2 GroundVelocity
    {
        get
        {
            if (!_isGrounded)
                return Vector2.zero;

            Vector2 groundVelocity = Vector2.zero;
            float fastestGroundSpeed = 0f;

            foreach (GroundContact contact in _groundContacts.Values)
            {
                if (contact.Collider == null)
                    continue;

                Vector2 velocity = Time.fixedDeltaTime > 0f
                    ? contact.Displacement / Time.fixedDeltaTime
                    : Vector2.zero;
                if (velocity.sqrMagnitude > fastestGroundSpeed)
                {
                    groundVelocity = velocity;
                    fastestGroundSpeed = velocity.sqrMagnitude;
                }
            }

            return groundVelocity;
        }
    }

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
        EntityId groundId = collision.collider.GetEntityId();
        if (!isGround)
        {
            _groundContacts.Remove(groundId);
            _isGrounded = _groundContacts.Count > 0;
            return;
        }

        Collider2D groundCollider = collision.collider;
        // Use the collider transform rather than attachedRigidbody velocity:
        // a support collider can move through a parent Rigidbody2D.
        Vector2 currentPosition = groundCollider.transform.position;
        Vector2 displacement = Vector2.zero;

        if (_groundContacts.TryGetValue(groundId, out GroundContact previousContact) &&
            previousContact.Collider == groundCollider)
        {
            displacement = currentPosition - previousContact.LastPosition;
        }

        _groundContacts[groundId] = new GroundContact
        {
            Collider = groundCollider,
            LastPosition = currentPosition,
            Displacement = displacement
        };
        _isGrounded = true;

        if (_player != null && _player.Pogo != null && _player.Pogo.enabled && _player.Pogo.IsPlunging)
        {
            _player.Pogo.OnPogoHit(collision);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        _groundContacts.Remove(collision.collider.GetEntityId());
        _isGrounded = _groundContacts.Count > 0;
    }
}
