using UnityEngine;

public class Rope : MonoBehaviour
{
    private EdgeCollider2D _edgeCollider;

    void Awake()
    {
        _edgeCollider = GetComponent<EdgeCollider2D>();
    }

    public float GetBottomY()
    {
        if (_edgeCollider != null && _edgeCollider.points.Length >= 2)
        {
            Vector2 worldBottom = transform.TransformPoint(_edgeCollider.points[_edgeCollider.points.Length - 1]);
            return worldBottom.y;
        }
        return transform.position.y - 6f;
    }
}
