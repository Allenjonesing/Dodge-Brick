using UnityEngine;
using LivingRoomPirates.Core;

namespace LivingRoomPirates.Damage
{
    public class BucketTool : MonoBehaviour, ILrpInteractable
    {
        [SerializeField] private ShipFloodingManager floodingManager;
        public string InteractionLabel => "Bail Water";
        public bool CanInteract(GameObject interactor) => floodingManager != null;
        public void Interact(GameObject interactor) => floodingManager.BucketWaterOut();
    }
}
