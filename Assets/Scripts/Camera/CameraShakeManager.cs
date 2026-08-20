using UnityEngine;
using System.Collections.Generic;
using Unity.Cinemachine;

public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager Instance { get; private set; }

    [System.Serializable]
    public struct ShakePreset
    {
        public CameraShakeType type;
        [Range(0f, 5f)] public float amplitude;
        [Range(0f, 5f)] public float frequency;
        [Range(0f, 1f)] public float duration;
    }

    [Header("Riferimenti")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Catalogo Shake Preset")]
    [SerializeField] private List<ShakePreset> shakePresets = new List<ShakePreset>();

    private Dictionary<CameraShakeType, ShakePreset> _presetDict;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (impulseSource == null)
        {
            Player player = FindAnyObjectByType<Player>();
            if (player != null)
            {
                impulseSource = player.ImpulseSource;
            }
        }

        _presetDict = new Dictionary<CameraShakeType, ShakePreset>();
        foreach (var preset in shakePresets)
        {
            if (!_presetDict.ContainsKey(preset.type))
            {
                _presetDict.Add(preset.type, preset);
            }
        }
    }

    public void Shake(CameraShakeType type)
    {
        if (_presetDict.TryGetValue(type, out ShakePreset preset))
        {
            ApplyPreset(preset);
        }
        else
        {
            Debug.LogWarning($"[CameraShakeManager] Il preset '{type}' non è stato assegnato nell'Inspector!");
        }
    }

    public void Shake(float amplitude, float frequency, float duration)
    {
        if (impulseSource == null)
        {
            Debug.LogWarning("[CameraShakeManager] CinemachineImpulseSource non trovato!");
            return;
        }

        impulseSource.ImpulseDefinition.AmplitudeGain = amplitude;
        impulseSource.ImpulseDefinition.FrequencyGain = frequency;
        impulseSource.ImpulseDefinition.ImpulseDuration = duration;
        impulseSource.GenerateImpulse();
    }

    private void ApplyPreset(ShakePreset preset)
    {
        if (impulseSource == null)
        {
            Debug.LogWarning("[CameraShakeManager] CinemachineImpulseSource non trovato!");
            return;
        }

        impulseSource.ImpulseDefinition.AmplitudeGain = preset.amplitude;
        impulseSource.ImpulseDefinition.FrequencyGain = preset.frequency;
        impulseSource.ImpulseDefinition.ImpulseDuration = preset.duration;
        impulseSource.GenerateImpulse();
    }
}
