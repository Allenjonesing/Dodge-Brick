using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[DefaultExecutionOrder(-100)]
public class OceanWaveMeshDeformer : MonoBehaviour
{
    [Header("Wave Shape - THESE ARE THE SOURCE OF TRUTH")]
    public float heightAmplitude = 4.14f;
    public float spatialFrequency = 0.69f;
    public float primaryFrequency = 0.02f;
    public float secondaryBlend = 0.51f;
    public float secondaryFrequency = 0.24f;
    public int seed = 17;

    [Header("Seamless World Variation")]
    [Tooltip("Adds extra non-repeating world-space swell. This is continuous across tile seams and helps the grid stop looking copy-pasted.")]
    public bool useSeamlessWorldVariation = true;
    [Range(0f, 1f)] public float worldVariationStrength = 0.22f;
    public float worldVariationFrequency = 0.083f;
    public float worldVariationSpeed = 0.011f;

    [Header("Per-Tile Interior Variation")]
    [Tooltip("Adds small per-tile differences only in the middle of each tile. It fades to zero at every edge so neighboring tiles still fit together.")]
    public bool useTileInteriorVariation = true;
    [Range(0f, 1f)] public float tileInteriorVariationStrength = 0.12f;
    public float tileInteriorVariationFrequency = 1.35f;
    public float tileInteriorVariationSpeed = 0.035f;
    public int tileVariationSeed = 0;
    [Range(0.5f, 6f)] public float tileEdgeFadePower = 2.5f;

    [Header("Runtime")]
    [Tooltip("Use world X/Z for the base wave equation so cloned water tiles seam together perfectly.")]
    public bool useWorldSpaceWaveCoordinates = true;

    [Tooltip("Optional shared ocean-space transform. When set, the wave equation samples coordinates in this transform's local X/Z space, so rotating the Water1 grid root rotates the actual wave pattern too, not just the material/meshes.")]
    public Transform waveCoordinateRoot;

    [Tooltip("When true and waveCoordinateRoot is set, world positions are converted through that root before sampling waves.")]
    public bool useWaveCoordinateRoot = true;

    private Mesh _mesh;
    private Vector3[] _baseVertices;
    private Vector3[] _workingVertices;
    private Bounds _baseBounds;

    private void OnEnable()
    {
        EnsureMeshInstance();
    }

    private void LateUpdate()
    {
        EnsureMeshInstance();

        if (_mesh == null || _baseVertices == null)
        {
            return;
        }

        float verticalScale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));

        for (int i = 0; i < _baseVertices.Length; i++)
        {
            Vector3 baseVertex = _baseVertices[i];
            Vector3 displaced = baseVertex;

            float offset;
            if (useWorldSpaceWaveCoordinates)
            {
                Vector3 world = transform.TransformPoint(baseVertex);
                offset = SampleWaveOffsetWorld(world);
            }
            else
            {
                offset = SampleWaveOffsetLocal(baseVertex.x, baseVertex.z) * verticalScale;
            }

            offset += SampleTileInteriorVariation(baseVertex);
            displaced.y = baseVertex.y + offset / verticalScale;
            _workingVertices[i] = displaced;
        }

        _mesh.vertices = _workingVertices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    public float SampleHeightAtWorldPosition(Vector3 worldPosition)
    {
        return useWorldSpaceWaveCoordinates
            ? SampleWaveOffsetWorld(worldPosition)
            : SampleWaveOffsetLocal(transform.InverseTransformPoint(worldPosition).x, transform.InverseTransformPoint(worldPosition).z) * Mathf.Abs(transform.lossyScale.y);
    }

    public float SampleWorldYAtWorldPosition(Vector3 worldPosition)
    {
        return transform.position.y + SampleHeightAtWorldPosition(worldPosition);
    }

    public float SampleWaveOffsetWorld(Vector3 worldPosition)
    {
        if (useWaveCoordinateRoot && waveCoordinateRoot != null)
        {
            Vector3 oceanSpace = waveCoordinateRoot.InverseTransformPoint(worldPosition);
            return SampleWave(oceanSpace.x, oceanSpace.z);
        }

        return SampleWave(worldPosition.x, worldPosition.z);
    }

    private float SampleWaveOffsetLocal(float x, float z)
    {
        return SampleWave(x, z);
    }

    private float SampleWave(float x, float z)
    {
        float t = Time.timeSinceLevelLoad;

        float primary = Mathf.Sin((x + seed * 0.19f) * spatialFrequency + t * primaryFrequency * Mathf.PI * 2f);
        float secondary = Mathf.Sin((z - seed * 0.11f) * (spatialFrequency * 1.37f) + t * secondaryFrequency * Mathf.PI * 2f + 0.8f);
        float diagonal = Mathf.Sin((x + z) * (spatialFrequency * 0.52f) + t * primaryFrequency * 1.3f * Mathf.PI * 2f + 1.7f);

        float wave = primary;
        wave += secondary * secondaryBlend;
        wave += diagonal * secondaryBlend * 0.5f;

        if (useSeamlessWorldVariation && worldVariationStrength > 0.0001f)
        {
            // Continuous, non-tile-locked waves. These break the obvious repetition without creating seams.
            float macroA = Mathf.Sin((x * 0.731f - z * 0.412f + seed * 3.17f) * worldVariationFrequency + t * worldVariationSpeed * Mathf.PI * 2f);
            float macroB = Mathf.Sin((x * -0.289f + z * 0.947f - seed * 1.73f) * worldVariationFrequency * 1.71f - t * worldVariationSpeed * 1.37f * Mathf.PI * 2f + 2.4f);
            float macroC = Mathf.Sin((x + z * 0.37f) * worldVariationFrequency * 0.43f + t * worldVariationSpeed * 0.63f * Mathf.PI * 2f + 5.1f);
            wave += (macroA + macroB * 0.65f + macroC * 0.45f) * worldVariationStrength;
        }

        return wave * heightAmplitude;
    }

    private float SampleTileInteriorVariation(Vector3 localVertex)
    {
        if (!useTileInteriorVariation || tileInteriorVariationStrength <= 0.0001f || _baseBounds.size.x <= 0.0001f || _baseBounds.size.z <= 0.0001f)
        {
            return 0f;
        }

        float nx = Mathf.InverseLerp(_baseBounds.min.x, _baseBounds.max.x, localVertex.x);
        float nz = Mathf.InverseLerp(_baseBounds.min.z, _baseBounds.max.z, localVertex.z);

        // 0 at every tile edge, 1 near the center. This makes per-tile variation fit like a puzzle piece.
        float edgeFadeX = Mathf.Sin(Mathf.Clamp01(nx) * Mathf.PI);
        float edgeFadeZ = Mathf.Sin(Mathf.Clamp01(nz) * Mathf.PI);
        float edgeMask = Mathf.Pow(Mathf.Max(0f, edgeFadeX * edgeFadeZ), tileEdgeFadePower);
        if (edgeMask <= 0.0001f)
        {
            return 0f;
        }

        float t = Time.timeSinceLevelLoad;
        float s = seed * 13.37f + tileVariationSeed * 19.91f;
        float local = Mathf.Sin((localVertex.x + s) * tileInteriorVariationFrequency + t * tileInteriorVariationSpeed * Mathf.PI * 2f);
        local += Mathf.Sin((localVertex.z - s * 0.37f) * tileInteriorVariationFrequency * 1.83f - t * tileInteriorVariationSpeed * 1.4f * Mathf.PI * 2f + 1.2f) * 0.55f;
        local += Mathf.Sin((localVertex.x - localVertex.z + s * 0.13f) * tileInteriorVariationFrequency * 0.71f + t * tileInteriorVariationSpeed * 0.75f * Mathf.PI * 2f + 2.7f) * 0.35f;

        return local * heightAmplitude * tileInteriorVariationStrength * edgeMask;
    }

    public void CopyWaveSettingsFrom(OceanWaveMeshDeformer source)
    {
        if (source == null)
        {
            return;
        }

        heightAmplitude = source.heightAmplitude;
        spatialFrequency = source.spatialFrequency;
        primaryFrequency = source.primaryFrequency;
        secondaryBlend = source.secondaryBlend;
        secondaryFrequency = source.secondaryFrequency;
        seed = source.seed;
        useWorldSpaceWaveCoordinates = source.useWorldSpaceWaveCoordinates;
        useWaveCoordinateRoot = source.useWaveCoordinateRoot;

        useSeamlessWorldVariation = source.useSeamlessWorldVariation;
        worldVariationStrength = source.worldVariationStrength;
        worldVariationFrequency = source.worldVariationFrequency;
        worldVariationSpeed = source.worldVariationSpeed;

        useTileInteriorVariation = source.useTileInteriorVariation;
        tileInteriorVariationStrength = source.tileInteriorVariationStrength;
        tileInteriorVariationFrequency = source.tileInteriorVariationFrequency;
        tileInteriorVariationSpeed = source.tileInteriorVariationSpeed;
        tileVariationSeed = source.tileVariationSeed;
        tileEdgeFadePower = source.tileEdgeFadePower;
    }

    private void EnsureMeshInstance()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        if (_mesh != null && meshFilter.sharedMesh == _mesh)
        {
            return;
        }

        _mesh = Instantiate(meshFilter.sharedMesh);
        _mesh.name = meshFilter.sharedMesh.name + "_OceanRuntimeClone";
        meshFilter.sharedMesh = _mesh;

        _baseVertices = _mesh.vertices;
        _workingVertices = new Vector3[_baseVertices.Length];
        _baseBounds = _mesh.bounds;
    }
}
