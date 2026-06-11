using UnityEngine;
using LivingRoomPirates.Core;
using LivingRoomPirates.Resources;

namespace LivingRoomPirates.Damage
{
    public class RepairPoint : MonoBehaviour, ILrpInteractable
    {
        [SerializeField] private HullSection targetSection;
        [SerializeField] private Leak targetLeak;
        [SerializeField] private ResourceStack repairCost = new ResourceStack(ResourceType.Wood, 1);
        [SerializeField] private float repairAmount = 25f;
        [SerializeField] private float leakRepairSecondsPerInteract = 1f;

        public string InteractionLabel => "Repair";
        public bool CanInteract(GameObject interactor) => ShipResourceBank.Instance == null || ShipResourceBank.Instance.Get(repairCost.Type) >= repairCost.Amount;

        public void Interact(GameObject interactor)
        {
            var bank = ShipResourceBank.Instance;
            if (bank != null && !bank.Spend(new [] { repairCost })) return;

            if (targetLeak != null) targetLeak.AddRepairProgress(leakRepairSecondsPerInteract);
            if (targetSection != null) targetSection.Repair(repairAmount);
            LrpEvents.RaiseRepairCompleted(this);
        }
    }
}
