using UnityEngine;

public interface IEmbeddable
{
    void OnSpearEmbedded(Spear spear, Vector2 hitPoint, Vector2 surfaceNormal);
    void OnSpearRemoved(Spear spear);
}
