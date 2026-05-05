using UnityEngine;

public class HitFlash : MonoBehaviour
{
    [SerializeField] private int flashDurationFrames = 4;

    [SerializeField] private Material hitFlashMaterial;
    private Material _originalMaterial;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private int _flashFramesLeft;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalMaterial = _spriteRenderer.material;
    }

    public void Flash()
    {
        _flashFramesLeft = flashDurationFrames;
        _spriteRenderer.material = hitFlashMaterial;
    }

    void FixedUpdate()
    {
        if (_flashFramesLeft > 0)
        {
            _flashFramesLeft--;
            if (_flashFramesLeft <= 0)
            {
                _spriteRenderer.material = _originalMaterial;
            }
        }
    }
}