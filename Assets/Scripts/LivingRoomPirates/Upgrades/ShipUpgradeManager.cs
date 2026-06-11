using System.Collections.Generic;
using UnityEngine;
using LivingRoomPirates.Core;
using LivingRoomPirates.Resources;

namespace LivingRoomPirates.Upgrades
{
    public class ShipUpgradeManager : LrpSingleton<ShipUpgradeManager>
    {
        [SerializeField] private List<ShipUpgradeDefinition> availableUpgrades = new List<ShipUpgradeDefinition>();
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        public int GetLevel(ShipUpgradeDefinition upgrade)
        {
            if (upgrade == null) return 0;
            return _levels.TryGetValue(upgrade.Id, out var level) ? level : 0;
        }

        public float GetBonus(ShipUpgradeType type)
        {
            float bonus = 0f;
            foreach (var upgrade in availableUpgrades)
            {
                if (upgrade == null || upgrade.Type != type) continue;
                bonus += GetLevel(upgrade) * upgrade.BonusPerLevel;
            }
            return bonus;
        }

        public bool TryPurchase(ShipUpgradeDefinition upgrade)
        {
            if (upgrade == null || ShipResourceBank.Instance == null) return false;
            int level = GetLevel(upgrade);
            if (level >= upgrade.MaxLevel) return false;
            var cost = upgrade.GetCostForNextLevel(level);
            if (!ShipResourceBank.Instance.Spend(cost)) return false;
            _levels[upgrade.Id] = level + 1;
            LrpEvents.RaiseUpgradePurchased(upgrade, level + 1);
            return true;
        }
    }
}
