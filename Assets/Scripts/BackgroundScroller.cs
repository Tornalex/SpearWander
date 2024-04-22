using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    [SerializeField] float overallSlowDown = 1;
    public float scrollSpeed = 0f;
    float offset = 0f;
    Material material;
    [SerializeField] PlayerActions playerActions;
    void Awake()
    {
        material = GetComponent<Renderer>().material;
    }
    void Update()
    {
        
        offset += (Time.deltaTime * (scrollSpeed * playerActions.moveInput.x)) / overallSlowDown;
        material.SetTextureOffset("_MainTex", new Vector2(offset, 0));
    }
}
