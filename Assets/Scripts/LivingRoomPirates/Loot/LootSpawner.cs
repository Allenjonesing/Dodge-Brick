using UnityEngine;

namespace LivingRoomPirates.Loot
{
    public class LootSpawner : MonoBehaviour
    {
        [SerializeField] private LootTable lootTable;
        [SerializeField] private GameObject fallbackLootPrefab;
        [SerializeField] private int rolls = 3;
        [SerializeField] private float scatterRadius = 1.5f;
        [SerializeField] private bool spawnOnStart;

        private void Start()
        {
            if (spawnOnStart) SpawnLoot();
        }

        public void SpawnLoot()
        {
            for (int i = 0; i < rolls; i++)
            {
                var entry = lootTable != null ? lootTable.Pick() : null;
                if (entry == null) continue;
                int count = Random.Range(entry.MinCount, entry.MaxCount + 1);
                for (int c = 0; c < count; c++) SpawnOne(entry.Definition);
            }
        }

        private void SpawnOne(LootItemDefinition definition)
        {
            var prefab = definition.WorldPrefab != null ? definition.WorldPrefab : fallbackLootPrefab;
            if (prefab == null) return;
            Vector3 offset = Random.insideUnitSphere * scatterRadius;
            offset.y = Mathf.Abs(offset.y) * 0.2f;
            var instance = Instantiate(prefab, transform.position + offset, Random.rotation, transform);
            var loot = instance.GetComponent<LootItem>();
            if (loot != null) loot.Initialize(definition);
        }
    }
}
