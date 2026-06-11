using UnityEngine;
using LivingRoomPirates.Core;
using LivingRoomPirates.Resources;

namespace LivingRoomPirates.Loot
{
    [RequireComponent(typeof(Collider))]
    public class LootItem : MonoBehaviour, ILrpInteractable
    {
        [SerializeField] private LootItemDefinition definition;
        [SerializeField] private bool destroyOnDeposit = true;
        [SerializeField] private bool collected;

        public LootItemDefinition Definition => definition;
        public bool Collected => collected;
        public string InteractionLabel => definition != null ? "Grab " + definition.DisplayName : "Grab Loot";

        public void Initialize(LootItemDefinition itemDefinition)
        {
            definition = itemDefinition;
            name = itemDefinition != null ? itemDefinition.DisplayName : name;
        }

        public bool CanInteract(GameObject interactor) => !collected;

        public void Interact(GameObject interactor)
        {
            Collect(interactor);
        }

        public void Collect(GameObject collector)
        {
            if (collected) return;
            collected = true;
            LrpEvents.RaiseLootCollected(this);
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = false;
        }

        public void DepositToShip()
        {
            if (definition == null) return;
            var bank = ShipResourceBank.Instance;
            if (bank != null) bank.AddStack(definition.DepositReward);
            LrpEvents.RaiseLootDeposited(this);
            if (destroyOnDeposit) Destroy(gameObject);
        }
    }
}
