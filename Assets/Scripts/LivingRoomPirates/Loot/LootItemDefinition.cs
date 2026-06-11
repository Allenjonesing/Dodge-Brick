using UnityEngine;
using LivingRoomPirates.Resources;

namespace LivingRoomPirates.Loot
{
    [CreateAssetMenu(menuName = "Living Room Pirates/Loot Item Definition")]
    public class LootItemDefinition : ScriptableObject
    {
        public string Id = "loot_gold_coin";
        public string DisplayName = "Gold Coin";
        public LootRarity Rarity = LootRarity.Common;
        public ResourceStack DepositReward = new ResourceStack(ResourceType.Gold, 1);
        public GameObject WorldPrefab;
        [TextArea] public string Description;
    }
}
