using UnityEngine;

public class SpearTipV2 : MonoBehaviour
{
    private SpearV2 _mainSpear;

    void Awake()
    {
        _mainSpear = GetComponentInParent<SpearV2>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_mainSpear != null)
        {
            _mainSpear.OnTipHit(collision);
        }
    }
}
