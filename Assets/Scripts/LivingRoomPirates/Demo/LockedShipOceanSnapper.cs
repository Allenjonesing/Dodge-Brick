using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Final Phase-1 ship/ocean contact solver.
    /// The ship NEVER moves. This script directly moves scene Water1 Y so the
    /// rendered wave surface at the ship center-bottom touches the hull contact.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class LockedShipOceanSnapper : MonoBehaviour
    {
        public Transform shipRoot;
        public Transform waterOne;
        public OceanWorldController ocean;

        [Tooltip("Negative lowers the ocean relative to the ship contact, making the ship appear slightly raised.")]
        public float shipToOceanSnapHeightOffset = -0.05f;
        public bool snapInstantly = true;
        public float snapSharpness = 60f;
        public bool logDebug;
        public float logInterval = 1f;

        private float _nextLog;
        private Vector3 _lastContact;
        private float _lastTargetY;

        private void LateUpdate()
        {
            Resolve();
            if (shipRoot == null || waterOne == null)
            {
                return;
            }

            Vector3 contact = ResolveShipBottomCenter(shipRoot);
            OceanWaveMeshDeformer deformer = waterOne.GetComponent<OceanWaveMeshDeformer>();
            float waveOffset = deformer != null ? deformer.SampleWaveOffsetWorld(contact) : 0f;

            // desiredSurfaceY = contactY + offset
            // renderedSurfaceY = Water1RootY + waveOffset
            // Water1RootY = desiredSurfaceY - waveOffset
            float targetY = contact.y + shipToOceanSnapHeightOffset - waveOffset;
            Vector3 p = waterOne.position;
            p.y = snapInstantly || snapSharpness <= 0f
                ? targetY
                : Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-snapSharpness * Time.deltaTime));
            waterOne.position = p;

            _lastContact = contact;
            _lastTargetY = targetY;

            if (ocean != null)
            {
                ocean.waterOne = waterOne;
                ocean.visualShipRoot = shipRoot;
                ocean.closeWaterOneGapToShip = false;
                ocean.waterOneHeightOffset = 0f;
                ocean.shipToOceanSnapHeightOffset = shipToOceanSnapHeightOffset;
            }

            if (logDebug && Time.time >= _nextLog)
            {
                _nextLog = Time.time + Mathf.Max(0.1f, logInterval);
                Debug.Log($"[LockedShipOceanSnapper] contactY={contact.y:F3} waveOffset={waveOffset:F3} Water1Y={waterOne.position.y:F3} targetY={targetY:F3} offset={shipToOceanSnapHeightOffset:F3}", this);
            }
        }

        private void Resolve()
        {
            if (shipRoot == null)
            {
                GameObject ship = GameObject.Find("shipGeneratedRoot");
                if (ship != null) shipRoot = ship.transform;
            }

            if (waterOne == null)
            {
                GameObject water = GameObject.Find("Water1");
                if (water != null) waterOne = water.transform;
            }

            if (ocean == null)
            {
                ocean = FindObjectOfType<OceanWorldController>();
            }
        }

        private static Vector3 ResolveShipBottomCenter(Transform root)
        {
            Bounds? b = ResolveBounds(root);
            if (b.HasValue)
            {
                Vector3 p = b.Value.center;
                p.y = b.Value.min.y;
                return p;
            }

            return root.position + Vector3.down * 0.10f;
        }

        private static Bounds? ResolveBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || ShouldIgnore(r.transform)) continue;

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
                if (n.Contains("water") || n.Contains("ocean") || n.Contains("debug") ||
                    n.Contains("floating") || n.Contains("cannonball") || n.Contains("boom") ||
                    n.Contains("ammo") || n.Contains("sail") || n.Contains("anchor") ||
                    n.Contains("wheel") || n.Contains("rope") || n.Contains("sign") ||
                    n.Contains("contact") || n.Contains("spyglass") || n.Contains("treasure") ||
                    n.Contains("bucket") || n.Contains("repair") || n.Contains("doohicky"))
                {
                    return true;
                }
                t = t.parent;
            }
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(_lastContact, 0.12f);
            Gizmos.DrawLine(_lastContact, new Vector3(_lastContact.x, _lastTargetY, _lastContact.z));
        }
    }
}
