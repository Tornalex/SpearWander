using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Background Speed Controller")]
    [SerializeField] float overallSlowDown = 1;
    [SerializeField] float scrollSpeed = 0f;

    [Header("Components")]
    [SerializeField] PlayerInputs playerInputs;
    
    float offset = 0f;
    Material material;
    void Awake()
    {
        material = GetComponent<Renderer>().material;
    }
    void Update()
    {
        
        offset += (Time.deltaTime * (scrollSpeed * playerInputs.moveInput.x)) / overallSlowDown;
        material.SetTextureOffset("_MainTex", new Vector2(offset, 0));
    }
}
