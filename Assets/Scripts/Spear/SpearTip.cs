using UnityEngine;

public class SpearTip : MonoBehaviour
{
    private Spear _mainSpear;

    void Awake()
    {
        _mainSpear = GetComponentInParent<Spear>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_mainSpear != null)
        {
            _mainSpear.OnTipHit(collision);
        }
    }
}