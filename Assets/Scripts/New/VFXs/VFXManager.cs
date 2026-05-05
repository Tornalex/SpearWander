using UnityEngine;
using System.Collections.Generic;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [System.Serializable]
    public struct VFXMapping
    {
        public VFXType vfxType;
        public GameObject prefab;
    }

    [Header("Catalogo Effetti Visivi")]
    [SerializeField] private List<VFXMapping> vfxLibrary = new List<VFXMapping>();
    
    private Dictionary<VFXType, GameObject> _vfxDict;

    void Awake()
    {
        // Setup del Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _vfxDict = new Dictionary<VFXType, GameObject>();
        foreach (var mapping in vfxLibrary)
        {
            if (!_vfxDict.ContainsKey(mapping.vfxType))
            {
                _vfxDict.Add(mapping.vfxType, mapping.prefab);
            }
        }
    }

    public void PlayVFX(VFXType type, Vector3 position, Quaternion rotation = default)
    {
        if (_vfxDict.TryGetValue(type, out GameObject prefab))
        {
            if (prefab != null)
            {
                Instantiate(prefab, position, rotation);
            }
        }
        else
        {
            Debug.LogWarning($"[VFXManager] Il tipo di effetto '{type}' non è stato assegnato nell'Inspector!");
        }
    }
}