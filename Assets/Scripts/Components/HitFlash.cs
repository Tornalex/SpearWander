using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HitFlash : MonoBehaviour
{
    [SerializeField] private float flashDuration = 0.067f;

    [SerializeField] private Material hitFlashMaterial;
    private Material _originalMaterial;
    private SpriteRenderer _spriteRenderer;
    private float _flashTimer;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalMaterial = _spriteRenderer.material;
    }

    public void Flash()
    {
        if (hitFlashMaterial == null)
            return;

        _flashTimer = flashDuration;
        _spriteRenderer.material = hitFlashMaterial;
    }

    void FixedUpdate()
    {
        if (_flashTimer > 0)
        {
            _flashTimer -= Time.fixedDeltaTime;
            if (_flashTimer <= 0)
            {
                _spriteRenderer.material = _originalMaterial;
            }
        }
    }
}
