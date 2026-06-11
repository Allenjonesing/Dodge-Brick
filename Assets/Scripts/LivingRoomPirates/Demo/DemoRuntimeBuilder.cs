using UnityEngine;
using LivingRoomPirates.Resources;
using LivingRoomPirates.Damage;
using LivingRoomPirates.Voyages;
using LivingRoomPirates.Upgrades;
using LivingRoomPirates.Integration;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Optional quick-start helper. Add this to an empty scene to create manager objects at runtime.
    /// Replace with real prefabs once integrated.
    /// </summary>
    public class DemoRuntimeBuilder : MonoBehaviour
    {
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private Transform shipRoot;
        [SerializeField] private bool attachSupportComponentsToShipRoot = true;

        private void Start()
        {
            if (!buildOnStart) return;
            BuildRuntime();
        }

        public void BuildRuntime()
        {
            Transform runtimeShipRoot = ResolveShipRoot();
            GameObject supportHost = runtimeShipRoot != null && attachSupportComponentsToShipRoot
                ? runtimeShipRoot.gameObject
                : gameObject;

            ShipResourceBank resourceBank = EnsureOnHost<ShipResourceBank>(supportHost);
            ShipDamageManager damageManager = EnsureOnHost<ShipDamageManager>(supportHost);
            ShipFloodingManager floodingManager = EnsureOnHost<ShipFloodingManager>(supportHost);
            OceanStormGameplayBridge stormBridge = EnsureOnHost<OceanStormGameplayBridge>(supportHost);

            Ensure<VoyageManager>("LRP Voyage Manager");
            Ensure<VoyageEventBridge>("LRP Voyage Event Bridge");
            Ensure<ShipUpgradeManager>("LRP Upgrade Manager");
            Ensure<PhotonStateAdapter>("LRP Photon Adapter");

            stormBridge.Configure(damageManager, runtimeShipRoot != null ? runtimeShipRoot : supportHost.transform);

            if (resourceBank != null && floodingManager != null)
            {
                floodingManager.name = "ShipFloodingManager";
            }
        }

        public void SetShipRoot(Transform root)
        {
            shipRoot = root;
        }

        private Transform ResolveShipRoot()
        {
            if (shipRoot != null)
            {
                return shipRoot;
            }

            ShipDamageManager damageManager = FindObjectOfType<ShipDamageManager>();
            if (damageManager != null)
            {
                return damageManager.transform;
            }

            return transform;
        }

        private static T Ensure<T>(string objectName) where T : Component
        {
            T existing = FindObjectOfType<T>();
            if (existing != null) return existing;
            GameObject go = new GameObject(objectName);
            return go.AddComponent<T>();
        }

        private static T EnsureOnHost<T>(GameObject host) where T : Component
        {
            T existing = host.GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            return host.AddComponent<T>();
        }
    }
}
