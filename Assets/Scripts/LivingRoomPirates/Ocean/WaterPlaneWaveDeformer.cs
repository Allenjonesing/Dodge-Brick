using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WaterPlaneWaveDeformer : MonoBehaviour
{
    [Range(0f, 4f)]
    public float heightAmplitude = 0.89f;

    [Range(0.005f, 1f)]
    public float primaryFrequency = 0.11f;

    [Range(0.01f, 1.5f)]
    public float spatialFrequency = 1.01f;

    [Range(0f, 1f)]
    public float secondaryBlend = 0.19f;

    [Range(0.005f, 1.5f)]
    public float secondaryFrequency = 0.11f;

    [Range(0f, 120f)]
    public float crestBias = 12f;

    public int seed = 17;

    private Mesh _deformedMesh;
    private Vector3[] _baseVertices;
    private Vector3[] _workingVertices;
    private Vector3[] _baseNormals;

    private void OnEnable()
    {
        EnsureMeshInstance();
    }

    private void LateUpdate()
    {
        if (_deformedMesh == null || _baseVertices == null || _workingVertices == null)
        {
            EnsureMeshInstance();
        }

        if (_deformedMesh == null || _baseVertices == null || _workingVertices == null)
        {
            return;
        }

        float stormTimeValue = Time.timeSinceLevelLoad;
        for (int i = 0; i < _baseVertices.Length; i++)
        {
            Vector3 baseVertex = _baseVertices[i];
            float primary = Mathf.Sin((baseVertex.x + seed * 0.19f) * spatialFrequency + stormTimeValue * primaryFrequency * Mathf.PI * 2f);
            float secondary = Mathf.Sin((baseVertex.z - seed * 0.11f) * (spatialFrequency * 1.37f) + stormTimeValue * secondaryFrequency * Mathf.PI * 2f + 0.8f);
            float diagonal = Mathf.Sin((baseVertex.x + baseVertex.z) * (spatialFrequency * 0.52f) + stormTimeValue * primaryFrequency * 1.3f * Mathf.PI * 2f + 1.7f);

            float waveHeight = primary;
            waveHeight += secondary * secondaryBlend;
            waveHeight += diagonal * (secondaryBlend * 0.5f);

            float crest = Mathf.Clamp01((baseVertex.z + crestBias) / (crestBias * 2f + 0.001f));
            float crestEnvelope = Mathf.Lerp(0.55f, 1.2f, crest);

            Vector3 displacedVertex = baseVertex;
            displacedVertex.y += waveHeight * heightAmplitude * crestEnvelope;
            displacedVertex.x += secondary * heightAmplitude * 0.08f;
            displacedVertex.z += primary * heightAmplitude * 0.12f;
            _workingVertices[i] = displacedVertex;
        }

        _deformedMesh.vertices = _workingVertices;
        _deformedMesh.RecalculateNormals();
        _deformedMesh.RecalculateBounds();
    }

    private void EnsureMeshInstance()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        if (_deformedMesh != null && meshFilter.sharedMesh == _deformedMesh)
        {
            return;
        }

        _deformedMesh = Instantiate(meshFilter.sharedMesh);
        _deformedMesh.name = meshFilter.sharedMesh.name + "_LobbyWaveClone";
        meshFilter.sharedMesh = _deformedMesh;

        _baseVertices = _deformedMesh.vertices;
        _workingVertices = new Vector3[_baseVertices.Length];
        _baseNormals = _deformedMesh.normals;
        if (_baseNormals == null || _baseNormals.Length != _baseVertices.Length)
        {
            _deformedMesh.RecalculateNormals();
            _baseNormals = _deformedMesh.normals;
        }
    }
}