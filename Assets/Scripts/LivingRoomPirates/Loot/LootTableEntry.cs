using System;
using UnityEngine;

namespace LivingRoomPirates.Loot
{
    [Serializable]
    public class LootTableEntry
    {
        public LootItemDefinition Definition;
        [Min(0.01f)] public float Weight = 1f;
        [Min(1)] public int MinCount = 1;
        [Min(1)] public int MaxCount = 1;
    }
}
