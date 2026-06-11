using UnityEngine;
using LivingRoomPirates.Core;
using LivingRoomPirates.Resources;
using LivingRoomPirates.Upgrades;

namespace LivingRoomPirates.Integration
{
    /// <summary>
    /// Placeholder adapter: replace Debug.Log calls with Photon RaiseEvent/RPCs in your project.
    /// This keeps the package Photon-optional and prevents compile errors if PUN is not installed.
    /// </summary>
    public class PhotonStateAdapter : MonoBehaviour
    {
        [SerializeField] private bool logEvents = true;

        private void OnEnable()
        {
            LrpEvents.ResourceChanged += OnResourceChanged;
            LrpEvents.UpgradePurchased += OnUpgradePurchased;
            LrpEvents.VoyageCompleted += OnVoyageCompleted;
            LrpEvents.LeakCreated += OnLeakCreated;
        }

        private void OnDisable()
        {
            LrpEvents.ResourceChanged -= OnResourceChanged;
            LrpEvents.UpgradePurchased -= OnUpgradePurchased;
            LrpEvents.VoyageCompleted -= OnVoyageCompleted;
            LrpEvents.LeakCreated -= OnLeakCreated;
        }

        private void OnResourceChanged(ResourceType type, int delta, int total)
        {
            if (logEvents) Debug.Log($"[LRP Network Stub] Resource {type}: {delta}, total {total}");
        }

        private void OnUpgradePurchased(ShipUpgradeDefinition upgrade, int level)
        {
            if (logEvents) Debug.Log($"[LRP Network Stub] Upgrade {upgrade.DisplayName} now level {level}");
        }

        private void OnVoyageCompleted(Voyages.Voyage voyage)
        {
            if (logEvents) Debug.Log($"[LRP Network Stub] Voyage complete: {voyage.Title}");
        }

        private void OnLeakCreated(Damage.Leak leak)
        {
            if (logEvents) Debug.Log("[LRP Network Stub] Leak spawned");
        }
    }
}
