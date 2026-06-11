using UnityEngine;
using LivingRoomPirates.Core;
using LivingRoomPirates.Loot;
using LivingRoomPirates.Damage;

namespace LivingRoomPirates.Voyages
{
    public class VoyageEventBridge : MonoBehaviour
    {
        private void OnEnable()
        {
            LrpEvents.LootCollected += OnLootCollected;
            LrpEvents.LootDeposited += OnLootDeposited;
            LrpEvents.RepairCompleted += OnRepairCompleted;
        }

        private void OnDisable()
        {
            LrpEvents.LootCollected -= OnLootCollected;
            LrpEvents.LootDeposited -= OnLootDeposited;
            LrpEvents.RepairCompleted -= OnRepairCompleted;
        }

        private void OnLootCollected(LootItem item) => VoyageManager.Instance?.AddProgress(VoyageObjectiveType.CollectLoot, 1);
        private void OnLootDeposited(LootItem item) => VoyageManager.Instance?.AddProgress(VoyageObjectiveType.DepositGold, item.Definition != null ? item.Definition.DepositReward.Amount : 1);
        private void OnRepairCompleted(RepairPoint point) => VoyageManager.Instance?.AddProgress(VoyageObjectiveType.RepairDamage, 1);
    }
}
