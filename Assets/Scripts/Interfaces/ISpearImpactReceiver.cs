using UnityEngine;

public interface ISpearImpactReceiver
{
    /// <summary>
    /// Called when a thrown spear in an attack state reaches this receiver.
    /// Return true to accept the impact; the spear will then perform its
    /// existing enemy-style bounce. Return false to reject the impact
    /// (duplicate, already broken, disabled, etc.) so the spear does not bounce.
    ///
    /// Implementations must not modify the spear's Rigidbody2D, colliders,
    /// state or velocity, and must not apply enemy damage or enemy-specific
    /// side effects.
    /// </summary>
    bool TryReceiveSpearImpact(Spear spear, Vector2 hitPoint, Vector2 surfaceNormal);
}
