
using UnityEngine;

/// <summary>
/// Draws predicted points using a reusable pool of small SpriteRenderers.
/// This component intentionally contains no trajectory or collision logic.
/// </summary>
public sealed class SpearTrajectoryRenderer : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField, Min(0.01f)] private float dotSize = 0.075f;
    [SerializeField, Min(0.01f)] private float dotSpacing = 2f;
    [SerializeField] private Color dotColor = Color.white;

    [Header("Rendering")]
    [SerializeField] private int sortingOrder = 502;

    private SpriteRenderer[] _dots = new SpriteRenderer[0];
    private Sprite _dotSprite;

    public void Show(Vector2[] points, int pointCount)
    {
        if (points == null || pointCount <= 0)
        {
            Hide();
            return;
        }

        float spacing = Mathf.Max(0.01f, dotSpacing);

        int dotIndex = 0;
        Vector2 lastDotPosition = points[0];

        EnsureCapacity(1);
        SetDot(dotIndex++, lastDotPosition);

        float accumulatedDistance = 0f;
        float nextDotDistance = spacing;

        for (int i = 1; i < pointCount; i++)
        {
            Vector2 segmentStart = points[i - 1];
            Vector2 segmentEnd = points[i];

            float segmentLength = Vector2.Distance(
                segmentStart,
                segmentEnd);

            if (segmentLength <= Mathf.Epsilon)
                continue;

            while (accumulatedDistance + segmentLength >= nextDotDistance)
            {
                float distanceOnSegment =
                    nextDotDistance - accumulatedDistance;

                float t = distanceOnSegment / segmentLength;
                Vector2 dotPosition = Vector2.Lerp(
                    segmentStart,
                    segmentEnd,
                    t);

                EnsureCapacity(dotIndex + 1);
                SetDot(dotIndex++, dotPosition);

                lastDotPosition = dotPosition;
                nextDotDistance += spacing;
            }

            accumulatedDistance += segmentLength;
        }

        // Always preserve the exact final endpoint.
        Vector2 endpoint = points[pointCount - 1];

        if ((endpoint - lastDotPosition).sqrMagnitude > 0.000001f)
        {
            EnsureCapacity(dotIndex + 1);
            SetDot(dotIndex++, endpoint);
        }

        for (int i = dotIndex; i < _dots.Length; i++)
            _dots[i].enabled = false;
    }

    public void Hide()
    {
        for (int i = 0; i < _dots.Length; i++)
        {
            if (_dots[i] != null)
                _dots[i].enabled = false;
        }
    }

    private void EnsureCapacity(int requiredCount)
    {
        if (_dots.Length >= requiredCount)
            return;

        int newCapacity = Mathf.Max(16, _dots.Length);

        while (newCapacity < requiredCount)
            newCapacity *= 2;

        SpriteRenderer[] expanded = new SpriteRenderer[newCapacity];

        for (int i = 0; i < _dots.Length; i++)
            expanded[i] = _dots[i];

        for (int i = _dots.Length; i < expanded.Length; i++)
        {
            GameObject dot = new GameObject("Spear Trajectory Dot");
            dot.transform.SetParent(null);

            SpriteRenderer spriteRenderer = dot.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetDotSprite();
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.enabled = false;

            expanded[i] = spriteRenderer;
        }

        _dots = expanded;
    }

    private void SetDot(int index, Vector2 position)
    {
        SpriteRenderer dot = _dots[index];

        dot.transform.position = position;
        dot.transform.localScale = Vector3.one * dotSize;
        dot.color = dotColor;
        dot.sortingOrder = sortingOrder;
        dot.enabled = true;
    }

    private Sprite GetDotSprite()
    {
        if (_dotSprite == null)
        {
            _dotSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        return _dotSprite;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _dots.Length; i++)
        {
            if (_dots[i] != null)
                Destroy(_dots[i].gameObject);
        }

        if (_dotSprite != null)
            Destroy(_dotSprite);
    }
}
