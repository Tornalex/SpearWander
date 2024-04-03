using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    public float scrollSpeed = 0f;
    float offset = 0f;
    Material material;
    PlayerActions playerActions;
    void Awake()
    {
        material = GetComponent<Renderer>().material;
        playerActions = FindObjectOfType<PlayerActions>();
    }
    void Update()
    {
        
        offset += (Time.deltaTime * (scrollSpeed * playerActions.moveInput.x)) / 10f;
        material.SetTextureOffset("_MainTex", new Vector2(offset, 0));
    }
}
