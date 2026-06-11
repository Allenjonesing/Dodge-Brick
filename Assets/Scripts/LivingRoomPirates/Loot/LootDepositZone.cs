using UnityEngine;

namespace LivingRoomPirates.Loot
{
    /// <summary>
    /// Put on a trigger collider on/near the ship chest. Loot entering it is converted to resources.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LootDepositZone : MonoBehaviour
    {
        [SerializeField] private bool requireCollected = false;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var loot = other.GetComponentInParent<LootItem>();
            if (loot == null) return;
            if (requireCollected && !loot.Collected) return;
            loot.DepositToShip();
        }
    }
}
