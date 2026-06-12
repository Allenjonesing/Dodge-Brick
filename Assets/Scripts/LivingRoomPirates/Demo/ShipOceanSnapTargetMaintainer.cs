using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Keeps the Water1 snap target alive after the ship generator rebuilds.
    /// This fixes the Phase 1 skip where BoundaryShipGenerator.Start() cleared
    /// ShipBottomOceanContact after the installer had assigned it.
    /// </summary>
    [DefaultExecutionOrder(90)]
    public sealed class ShipOceanSnapTargetMaintainer : MonoBehaviour
    {
        public Transform shipRoot;
        public OceanWorldController ocean;
        public float shipToOceanSnapHeightOffset = -0.05f;
        public bool updateEveryFrame = true;
        public bool logDebug;

        private Transform _contact;

        private void Awake()
        {
            Resolve();
            RefreshTarget();
        }

        private void LateUpdate()
        {
            Resolve();
            if (updateEveryFrame)
            {
                RefreshTarget();
            }
        }

        public void RefreshTarget()
        {
            if (shipRoot == null || ocean == null)
            {
                return;
            }

            if (_contact == null || _contact.parent != shipRoot)
            {
                Transform existing = shipRoot.Find("ShipBottomOceanContact");
                if (existing != null)
                {
                    _contact = existing;
                }
                else
                {
                    GameObject go = new GameObject("ShipBottomOceanContact");
                    _contact = go.transform;
                    _contact.SetParent(shipRoot, false);
                }
            }

            Bounds? bounds = ResolveShipBounds(shipRoot);
            if (bounds.HasValue)
            {
                Vector3 p = bounds.Value.center;
                p.y = bounds.Value.min.y;
                _contact.position = p;
            }
            else
            {
                // Placeholder ship bottom is approximately 0.14m below ship root.
                _contact.localPosition = new Vector3(0f, -0.14f, 0f);
            }

            ocean.waterOneFollowTarget = _contact;
            ocean.useVisualShipBoundsBottomAsWaterOneTarget = false;
            ocean.closeWaterOneGapToShip = true;
            ocean.waterOneFollowStrength = 0f;
            ocean.waterOneHeightOffset = 0f;
            ocean.shipToOceanSnapHeightOffset = shipToOceanSnapHeightOffset;

            if (logDebug)
            {
                Debug.Log($"[ShipOceanSnapTargetMaintainer] target={_contact.position} water1={(ocean.waterOne != null ? ocean.waterOne.position.y.ToString("F3") : "null")}", this);
            }
        }

        private void Resolve()
        {
            if (shipRoot == null)
            {
                GameObject go = GameObject.Find("shipGeneratedRoot");
                if (go != null) shipRoot = go.transform;
            }

            if (ocean == null)
            {
                ocean = FindObjectOfType<OceanWorldController>();
            }
        }

        private static Bounds? ResolveShipBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || ShouldIgnore(r.transform))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return found ? bounds : (Bounds?)null;
        }

        private static bool ShouldIgnore(Transform t)
        {
            while (t != null)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("water") || n.Contains("ocean") || n.Contains("debug") || n.Contains("floating") || n.Contains("cannonball") || n.Contains("boom") || n.Contains("contact"))
                {
                    return true;
                }
                t = t.parent;
            }
            return false;
        }
    }
}
