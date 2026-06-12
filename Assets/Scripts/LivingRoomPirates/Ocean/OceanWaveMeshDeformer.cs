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

    [Header("Runtime")]
    [Tooltip("Use world X/Z for the wave equation so cloned water tiles seam together perfectly.")]
    public bool useWorldSpaceWaveCoordinates = true;

    private Mesh _mesh;
    private Vector3[] _baseVertices;
    private Vector3[] _workingVertices;

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

            if (useWorldSpaceWaveCoordinates)
            {
                Vector3 world = transform.TransformPoint(baseVertex);
                displaced.y = baseVertex.y + SampleWaveOffsetWorld(world) / verticalScale;
            }
            else
            {
                displaced.y = baseVertex.y + SampleWaveOffsetLocal(baseVertex.x, baseVertex.z);
            }

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

        return wave * heightAmplitude;
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
    }
}
