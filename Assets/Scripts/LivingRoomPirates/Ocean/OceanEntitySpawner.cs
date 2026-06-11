using UnityEngine;

public class OceanEntitySpawner : MonoBehaviour
{
    public GameObject[] prefabs;
    public Transform spawnParent;

    public float minDistance = 35f;
    public float maxDistance = 90f;
    public float spawnInterval = 8f;
    public int maxSpawned = 12;

    private float _timer;
    private int _spawnedCount;

    private void Update()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return;
        }

        if (_spawnedCount >= maxSpawned)
        {
            return;
        }

        _timer += Time.deltaTime;

        if (_timer < spawnInterval)
        {
            return;
        }

        _timer = 0f;
        SpawnEntity();
    }

    private void SpawnEntity()
    {
        OceanWorldController ocean = OceanWorldController.Instance;

        if (ocean == null)
        {
            return;
        }

        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];

        float angle = Random.Range(0f, 360f);
        float distance = Random.Range(minDistance, maxDistance);

        Vector3 localOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
        Vector3 spawnPosition = localOffset;

        if (ocean.oceanWorldRoot != null)
        {
            spawnPosition = ocean.oceanWorldRoot.TransformPoint(localOffset);
        }

        spawnPosition.y = ocean.SampleHeight(spawnPosition);

        GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), spawnParent);

        if (instance.GetComponent<OceanSurfaceFollower>() == null)
        {
            instance.AddComponent<OceanSurfaceFollower>();
        }

        _spawnedCount++;
    }
}
