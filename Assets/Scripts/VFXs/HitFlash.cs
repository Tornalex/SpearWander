using UnityEngine;

public class HitFlash : MonoBehaviour
{
    [SerializeField] private float flashDuration = 0.067f;

    [SerializeField] private Material hitFlashMaterial;
    private Material _originalMaterial;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private float _flashTimer;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalMaterial = _spriteRenderer.material;
    }

    public void Flash()
    {
        _flashTimer = flashDuration;
        _spriteRenderer.material = hitFlashMaterial;
    }

    void FixedUpdate()
    {
        if (_flashTimer > 0)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0)
            {
                _spriteRenderer.material = _originalMaterial;
            }
        }
    }
}
