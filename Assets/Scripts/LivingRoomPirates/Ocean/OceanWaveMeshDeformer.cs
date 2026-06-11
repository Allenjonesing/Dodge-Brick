using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class OceanWaveMeshDeformer : MonoBehaviour
{
    [Header("Overrides")]
    public bool useControllerSettings = false;

    public float heightAmplitude = 1.63f;
    public float spatialFrequency = 1.01f;
    public float primaryFrequency = 0.05f;
    public float secondaryBlend = 0.51f;
    public float secondaryFrequency = 0.24f;
    public int seed = 17;

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

        OceanWorldController ocean = OceanWorldController.Instance;

        for (int i = 0; i < _baseVertices.Length; i++)
        {
            Vector3 baseVertex = _baseVertices[i];
            Vector3 world = transform.TransformPoint(baseVertex);

            float height;

            if (useControllerSettings && ocean != null)
            {
                height = ocean.SampleHeight(world);
            }
            else
            {
                height = SampleLocalHeight(baseVertex.x, baseVertex.z);
            }

            Vector3 displaced = baseVertex;
            displaced.y = height;
            _workingVertices[i] = displaced;
        }

        _mesh.vertices = _workingVertices;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    private float SampleLocalHeight(float x, float z)
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
