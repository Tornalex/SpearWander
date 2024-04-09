using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowObject : MonoBehaviour
{
    public float flipYRotationTime = 0f;
    bool _isFacingRight = true;
    PlayerActions playerActions;
    void Start()
    {
        playerActions = FindObjectOfType<PlayerActions>();
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
