using UnityEngine;

/// <summary>
/// Runtime hotkey switcher for Water1 wave values.
/// Attach to the same object as OceanWaveMeshDeformer, usually Water1.
/// Number keys apply presets immediately and copy the values to all generated Water1 grid tiles.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(OceanWaveMeshDeformer))]
public class OceanWavePresetHotkeys : MonoBehaviour
{
    [System.Serializable]
    public struct WavePreset
    {
        public string name;
        public KeyCode hotkey;
        public float heightAmplitude;
        public float spatialFrequency;
        public float primaryFrequency;
        public float secondaryBlend;
        public float secondaryFrequency;
        public int seed;

        public WavePreset(string name, KeyCode hotkey, float heightAmplitude, float spatialFrequency, float primaryFrequency, float secondaryBlend, float secondaryFrequency, int seed)
        {
            this.name = name;
            this.hotkey = hotkey;
            this.heightAmplitude = heightAmplitude;
            this.spatialFrequency = spatialFrequency;
            this.primaryFrequency = primaryFrequency;
            this.secondaryBlend = secondaryBlend;
            this.secondaryFrequency = secondaryFrequency;
            this.seed = seed;
        }
    }

    [Header("Preset Hotkeys")]
    public bool enableHotkeys = true;
    public bool applyDefaultPresetOnStart = false;
    public int defaultPresetIndex = 2;

    [Tooltip("When true, preset values are copied to all Water1_Tile_* child deformers after every preset change.")]
    public bool copyToWaterGridTiles = true;

    [Header("Presets")]
    public WavePreset[] presets = new WavePreset[]
    {
        new WavePreset("1 Calm / Alive", KeyCode.Alpha1, 0.18f, 0.55f, 0.06f, 0.18f, 0.09f, 17),
        new WavePreset("2 Playable Pirate Swell", KeyCode.Alpha2, 0.45f, 0.42f, 0.08f, 0.30f, 0.12f, 17),
        new WavePreset("3 Big VR Swell", KeyCode.Alpha3, 0.85f, 0.32f, 0.10f, 0.42f, 0.16f, 17),
        new WavePreset("4 Stormy Playable", KeyCode.Alpha4, 1.25f, 0.25f, 0.12f, 0.55f, 0.19f, 17),
        new WavePreset("5 Cartoon Storm", KeyCode.Alpha5, 1.80f, 0.20f, 0.14f, 0.70f, 0.23f, 17),
        new WavePreset("6 Huge Test / 4.14", KeyCode.Alpha6, 4.14f, 0.16f, 0.10f, 0.55f, 0.17f, 17),
    };

    public int CurrentPresetIndex { get; private set; } = -1;

    private OceanWaveMeshDeformer _deformer;

    private void Awake()
    {
        _deformer = GetComponent<OceanWaveMeshDeformer>();
    }

    private void Start()
    {
        if (applyDefaultPresetOnStart && presets != null && presets.Length > 0)
        {
            ApplyPreset(Mathf.Clamp(defaultPresetIndex, 0, presets.Length - 1));
        }
        else
        {
            SyncGridTilesFromCurrentValues();
            LogCurrentControls();
        }
    }

    private void Update()
    {
        if (!enableHotkeys || presets == null)
        {
            return;
        }

        for (int i = 0; i < presets.Length; i++)
        {
            if (Input.GetKeyDown(presets[i].hotkey))
            {
                ApplyPreset(i);
                return;
            }
        }
    }

    [ContextMenu("Apply Default Preset")]
    public void ApplyDefaultPreset()
    {
        ApplyPreset(defaultPresetIndex);
    }

    public void ApplyPreset(int index)
    {
        if (_deformer == null)
        {
            _deformer = GetComponent<OceanWaveMeshDeformer>();
        }

        if (_deformer == null || presets == null || presets.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, presets.Length - 1);
        WavePreset preset = presets[index];

        _deformer.heightAmplitude = preset.heightAmplitude;
        _deformer.spatialFrequency = preset.spatialFrequency;
        _deformer.primaryFrequency = preset.primaryFrequency;
        _deformer.secondaryBlend = preset.secondaryBlend;
        _deformer.secondaryFrequency = preset.secondaryFrequency;
        _deformer.seed = preset.seed;
        _deformer.useWorldSpaceWaveCoordinates = true;
        _deformer.enabled = true;

        CurrentPresetIndex = index;
        SyncGridTilesFromCurrentValues();

        Debug.Log($"[OceanWavePresetHotkeys] Applied {preset.name}: height={preset.heightAmplitude:F2}, spatialFrequency={preset.spatialFrequency:F2}, primaryFrequency={preset.primaryFrequency:F2}, secondaryBlend={preset.secondaryBlend:F2}, secondaryFrequency={preset.secondaryFrequency:F2}", this);
    }

    public void SyncGridTilesFromCurrentValues()
    {
        if (!copyToWaterGridTiles || _deformer == null)
        {
            return;
        }

        OceanWaveMeshDeformer[] deformers = GetComponentsInChildren<OceanWaveMeshDeformer>(true);
        for (int i = 0; i < deformers.Length; i++)
        {
            OceanWaveMeshDeformer child = deformers[i];
            if (child == null || child == _deformer)
            {
                continue;
            }

            child.CopyWaveSettingsFrom(_deformer);
            child.enabled = true;
        }
    }

    private void LogCurrentControls()
    {
        if (presets == null || presets.Length == 0)
        {
            return;
        }

        string message = "[OceanWavePresetHotkeys] Wave preset keys:";
        for (int i = 0; i < presets.Length; i++)
        {
            message += $"\n  {presets[i].hotkey}: {presets[i].name}";
        }

        Debug.Log(message, this);
    }
}
