using System.Collections.Generic;
using UnityEngine;
using LivingRoomPirates.Resources;

namespace LivingRoomPirates.Upgrades
{
    [CreateAssetMenu(menuName = "Living Room Pirates/Ship Upgrade")]
    public class ShipUpgradeDefinition : ScriptableObject
    {
        public string Id = "upgrade_hull_strength";
        public string DisplayName = "Hull Reinforcement";
        public ShipUpgradeType Type = ShipUpgradeType.HullStrength;
        public int MaxLevel = 5;
        public float BonusPerLevel = 0.1f;
        public List<ResourceStack> BaseCost = new List<ResourceStack> { new ResourceStack(ResourceType.Gold, 50), new ResourceStack(ResourceType.Wood, 5) };

        public List<ResourceStack> GetCostForNextLevel(int currentLevel)
        {
            var cost = new List<ResourceStack>();
            int multiplier = Mathf.Max(1, currentLevel + 1);
            foreach (var stack in BaseCost) cost.Add(new ResourceStack(stack.Type, stack.Amount * multiplier));
            return cost;
        }
    }
}
