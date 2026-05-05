using UnityEngine;
using System.Collections.Generic;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [System.Serializable]
    public struct SFXMapping
    {
        public SFXType sfxType;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
    }

    [Header("Catalogo Effetti Sonori")]
    [SerializeField] private List<SFXMapping> sfxLibrary = new List<SFXMapping>();

    [Header("Impostazioni Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private float globalPitchVariation = 0.05f; // Per rendere i suoni meno ripetitivi

    private Dictionary<SFXType, SFXMapping> _sfxDict;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Se non hai assegnato un AudioSource, ne creiamo uno noi
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        _sfxDict = new Dictionary<SFXType, SFXMapping>();
        foreach (var mapping in sfxLibrary)
        {
            if (!_sfxDict.ContainsKey(mapping.sfxType))
            {
                _sfxDict.Add(mapping.sfxType, mapping);
            }
        }
    }

    public void PlaySFX(SFXType type)
    {
        if (_sfxDict.TryGetValue(type, out SFXMapping mapping))
        {
            // Un trucco da professionisti: variamo leggermente il pitch (tonalità) 
            // ogni volta che il suono viene riprodotto. Così il gioco sembra più "vivo".
            sfxSource.pitch = 1f + Random.Range(-globalPitchVariation, globalPitchVariation);
            
            sfxSource.PlayOneShot(mapping.clip, mapping.volume);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] Il suono '{type}' non è presente in libreria!");
        }
    }
}