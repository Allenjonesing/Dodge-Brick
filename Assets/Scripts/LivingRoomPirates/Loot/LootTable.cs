using System.Collections.Generic;
using UnityEngine;

namespace LivingRoomPirates.Loot
{
    [CreateAssetMenu(menuName = "Living Room Pirates/Loot Table")]
    public class LootTable : ScriptableObject
    {
        public List<LootTableEntry> Entries = new List<LootTableEntry>();

        public LootTableEntry Pick()
        {
            float total = 0f;
            foreach (var entry in Entries)
            {
                if (entry != null && entry.Definition != null) total += Mathf.Max(0.01f, entry.Weight);
            }
            if (total <= 0f) return null;

            float roll = Random.value * total;
            foreach (var entry in Entries)
            {
                if (entry == null || entry.Definition == null) continue;
                roll -= Mathf.Max(0.01f, entry.Weight);
                if (roll <= 0f) return entry;
            }
            return Entries.Count > 0 ? Entries[Entries.Count - 1] : null;
        }
    }
}
