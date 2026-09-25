using UnityEngine;
using System.Collections;
using SpearWander.Boss;

public partial class Spear : MonoBehaviour
{
private void OnCollisionEnter2D(
    Collision2D collision)
{
    if (currentState != SpearState.Flying &&
        currentState != SpearState.Dropped)
    {
        return;
    }

    Boss boss =
        collision.collider.GetComponentInParent<Boss>();

    if (boss != null &&
        currentState == SpearState.Flying)
    {
        WeakPoint weakPoint =
            collision.collider.GetComponent<WeakPoint>();

        if (weakPoint != null)
            return;

        BounceOffBoss(collision);
        return;
    }

    if (currentState == SpearState.Dropped)
    {
        Vector2 hitPoint =
            collision.contacts[0].point;

        Vector2 normal =
            collision.contacts[0].normal;

        ProcessHit(
            collision.collider,
            hitPoint,
            normal
        );
    }
}

    public void OnTipHit(Collider2D other)
    {
        if (other.gameObject.layer == _spearLayer)
            return;

        if (other.transform.IsChildOf(transform))
            return;

        if (currentState == SpearState.Embedded)
            return;

        if (other.CompareTag(cameraBoundsTag))
            return;

        if (other.CompareTag(directionChangerTag))
            return;

        if (_playerCollider != null &&
            other.transform.IsChildOf(
                _playerCollider.transform))
        {
            return;
        }

        Vector2 hitPoint =
            other.ClosestPoint(transform.position);

        Vector2 flyDir =
            _lastVelocity.sqrMagnitude > 0.01f
                ? _lastVelocity.normalized
                : (Vector2)transform.right;

        RaycastHit2D hit =
            Physics2D.Raycast(
                hitPoint - flyDir * 0.5f,
                flyDir,
                tipRaycastLength,
                1 << other.gameObject.layer
            );

        Vector2 normal =
            hit.collider != null
                ? hit.normal
                : Vector2.up;

        ProcessHit(
            other,
            hitPoint,
            normal
        );
    }

    private void BounceOffBoss(
        Collision2D collision)
    {
        Vector2 normal =
            collision.contacts[0].normal;

        Vector2 velocity =
            _rb.linearVelocity;

        float dot =
            Vector2.Dot(
                velocity,
                normal
            );

        Vector2 bounceDirection =
            velocity -
            2 * dot * normal;

        float bounceFactor = 0.6f;

        Vector2 bounceVelocity =
            bounceDirection *
            bounceFactor;

        _rb.linearVelocity =
            bounceVelocity;

        if (bounceVelocity.sqrMagnitude > 0.01f)
        {
            transform.right =
                bounceVelocity.normalized;
        }

        _rb.bodyType =
            RigidbodyType2D.Dynamic;

        _rb.gravityScale =
            1f;

        _collider.isTrigger =
            false;

        currentState =
            SpearState.Dropped;

        if (collision.collider != null &&
            _collider != null)
        {
            Physics2D.IgnoreCollision(
                _collider,
                collision.collider,
                true
            );

            StartCoroutine(
                ReenableCollisionWithBoss(
                    collision.collider,
                    0.5f
                )
            );
        }
    }

    private IEnumerator ReenableCollisionWithBoss(
        Collider2D bossCollider,
        float delay)
    {
        yield return new WaitForSeconds(delay);

        if (bossCollider != null &&
            _collider != null)
        {
            Physics2D.IgnoreCollision(
                _collider,
                bossCollider,
                false
            );
        }
    }
}