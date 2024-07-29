using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowObject : MonoBehaviour
{
    [Header("Camera Follow Stats")]
    public float flipYRotationTime = 0f;

    [Header("Components")]
    [SerializeField] PlayerInputs playerActions;
    
    bool _isFacingRight = true;
    void Start()
    {
        _isFacingRight = playerActions.isFacingRight;
    }
    void FixedUpdate()
    {
        transform.position = playerActions.transform.position;
    }
    public void CallTurn()
    {
        LeanTween.rotateY(gameObject, DetermineEndRotation(), flipYRotationTime).setEaseInOutSine();
    }
    private float DetermineEndRotation()
    {
        _isFacingRight = !_isFacingRight;
        if (_isFacingRight)
        {
            return 180f;
        }
        else
        {
            return 0f;
        }
    }    
}
