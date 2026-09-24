using UnityEngine;

public partial class Spear : MonoBehaviour
{
    [Header("Rope Settings")]
    [Tooltip("Local offset where the rope attaches (handle)")]
    [SerializeField] private Vector2 ropeAttachOffset =
        new Vector2(-1.25f, 0f);

    [Tooltip("Visual width of the rope")]
    [SerializeField] private float ropeWidth = 0.08f;

    [Tooltip("Material used for the rope line renderer")]
    [SerializeField] private Material ropeMaterial;

    private float _ropeLength;

    private GameObject _rope;

    private bool _canSpawnRope;

    private void CreateRope()
    {
        if (_rope != null)
            return;

        if (!_canSpawnRope)
            return;

        _rope =
            new GameObject("Rope");

        _rope.transform.SetParent(
            transform,
            false
        );

        _rope.transform.localPosition =
            ropeAttachOffset;

        _rope.transform.rotation =
            Quaternion.identity;

        _rope.layer =
            _spearLayer;

        LineRenderer lr =
            _rope.AddComponent<LineRenderer>();

        lr.useWorldSpace =
            false;

        lr.positionCount =
            2;

        lr.SetPosition(
            0,
            Vector3.zero
        );

        lr.SetPosition(
            1,
            Vector3.down *
            _ropeLength
        );

        lr.startWidth =
            ropeWidth;

        lr.endWidth =
            ropeWidth;

        if (ropeMaterial != null)
            lr.sharedMaterial =
                ropeMaterial;

        lr.startColor =
            new Color(
                0.5f,
                0.35f,
                0.15f
            );

        lr.endColor =
            new Color(
                0.5f,
                0.35f,
                0.35f
            );

        EdgeCollider2D ec =
            _rope.AddComponent<EdgeCollider2D>();

        ec.points =
            new Vector2[]
            {
                Vector2.zero,
                Vector2.down *
                _ropeLength
            };

        ec.isTrigger =
            true;

        _rope.AddComponent<Rope>();
    }

    private void DestroyRope()
    {
        if (_rope != null)
        {
            if (Application.isPlaying)
                Destroy(_rope);
            else
                DestroyImmediate(_rope);

            _rope = null;
        }
    }
}