using System;
using LivingRoomPirates.Loot;
using LivingRoomPirates.Resources;
using LivingRoomPirates.Voyages;
using LivingRoomPirates.Damage;
using LivingRoomPirates.Upgrades;

namespace LivingRoomPirates.Core
{
    /// <summary>
    /// Lightweight event hub. Network code can listen here and replicate only important state changes.
    /// </summary>
    public static class LrpEvents
    {
        public static event Action<LootItem> LootCollected;
        public static event Action<LootItem> LootDeposited;
        public static event Action<ResourceType, int, int> ResourceChanged;
        public static event Action<Voyage> VoyageStarted;
        public static event Action<VoyageObjective> ObjectiveCompleted;
        public static event Action<Voyage> VoyageCompleted;
        public static event Action<HullSection, float> HullDamaged;
        public static event Action<Leak> LeakCreated;
        public static event Action<RepairPoint> RepairCompleted;
        public static event Action<ShipUpgradeDefinition, int> UpgradePurchased;

        public static void RaiseLootCollected(LootItem item) => LootCollected?.Invoke(item);
        public static void RaiseLootDeposited(LootItem item) => LootDeposited?.Invoke(item);
        public static void RaiseResourceChanged(ResourceType type, int amount, int newTotal) => ResourceChanged?.Invoke(type, amount, newTotal);
        public static void RaiseVoyageStarted(Voyage voyage) => VoyageStarted?.Invoke(voyage);
        public static void RaiseObjectiveCompleted(VoyageObjective objective) => ObjectiveCompleted?.Invoke(objective);
        public static void RaiseVoyageCompleted(Voyage voyage) => VoyageCompleted?.Invoke(voyage);
        public static void RaiseHullDamaged(HullSection section, float damage) => HullDamaged?.Invoke(section, damage);
        public static void RaiseLeakCreated(Leak leak) => LeakCreated?.Invoke(leak);
        public static void RaiseRepairCompleted(RepairPoint point) => RepairCompleted?.Invoke(point);
        public static void RaiseUpgradePurchased(ShipUpgradeDefinition upgrade, int newLevel) => UpgradePurchased?.Invoke(upgrade, newLevel);
    }
}
