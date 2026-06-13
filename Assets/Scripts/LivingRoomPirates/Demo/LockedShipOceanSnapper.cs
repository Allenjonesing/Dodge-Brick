using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Final Phase-1 ship/ocean contact solver.
    /// The ship NEVER moves. This script directly moves scene Water1 Y so the
    /// rendered wave surface touches an averaged set of hull contact samples.
    ///
    /// Why not one center point? Because one point lies easily: mast/rails/debug
    /// bounds can move the center, and a single wave sample can be at a crest or trough.
    /// This uses 8 points around the hull footprint and averages their required
    /// Water1 root Y. That keeps the locked ship visually riding the wave plane
    /// without actually moving/rotating the player or ship.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class LockedShipOceanSnapper : MonoBehaviour
    {
        public Transform shipRoot;
        public Transform waterOne;
        public OceanWorldController ocean;

        [Tooltip("Negative lowers the ocean relative to the ship contact, making the ship appear slightly raised.")]
        public float shipToOceanSnapHeightOffset = -0.05f;

        [Header("Hull Samples")]
        [Tooltip("Use 8 hull footprint samples: four corners plus front/back/left/right centers.")]
        public bool useEightPointHullAverage = true;
        [Tooltip("Pull samples inward from the renderer bounds so rail overhangs do not dominate.")]
        [Range(0f, 0.45f)] public float hullSampleInsetPercent = 0.12f;
        [Tooltip("Optional extra downward offset from renderer bottom if the generated deck mesh bottom is above the intended waterline.")]
        public float hullContactExtraDown = 0f;

        public bool snapInstantly = true;
        public float snapSharpness = 60f;
        public bool logDebug;
        public float logInterval = 1f;

        private float _nextLog;
        private readonly Vector3[] _lastContacts = new Vector3[8];
        private readonly Vector3[] _lastSurfaces = new Vector3[8];
        private int _lastCount;
        private float _lastTargetY;
        private float _lastAverageGap;

        private void LateUpdate()
        {
            Resolve();
            if (shipRoot == null || waterOne == null)
            {
                return;
            }

            OceanWaveMeshDeformer deformer = waterOne.GetComponent<OceanWaveMeshDeformer>();
            Vector3[] contacts = ResolveShipContactSamples(shipRoot, useEightPointHullAverage);
            if (contacts == null || contacts.Length == 0)
            {
                return;
            }

            float targetSum = 0f;
            float gapSum = 0f;
            int count = 0;
            float currentWaterY = waterOne.position.y;

            for (int i = 0; i < contacts.Length; i++)
            {
                Vector3 contact = contacts[i];
                float waveOffset = deformer != null ? deformer.SampleWaveOffsetWorld(contact) : 0f;
                float targetYForThisPoint = contact.y + shipToOceanSnapHeightOffset - waveOffset;
                float currentSurfaceY = currentWaterY + waveOffset;
                float desiredSurfaceY = contact.y + shipToOceanSnapHeightOffset;

                targetSum += targetYForThisPoint;
                gapSum += desiredSurfaceY - currentSurfaceY;

                if (i < _lastContacts.Length)
                {
                    _lastContacts[i] = contact;
                    _lastSurfaces[i] = new Vector3(contact.x, currentSurfaceY, contact.z);
                }
                count++;
            }

            if (count <= 0) return;

            float targetY = targetSum / count;
            Vector3 p = waterOne.position;
            p.y = snapInstantly || snapSharpness <= 0f
                ? targetY
                : Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-snapSharpness * Time.deltaTime));
            waterOne.position = p;

            _lastCount = Mathf.Min(count, _lastContacts.Length);
            _lastTargetY = targetY;
            _lastAverageGap = gapSum / count;

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
                Debug.Log($"[LockedShipOceanSnapper] samples={count} avgGapBefore={_lastAverageGap:F3} Water1Y={waterOne.position.y:F3} targetY={targetY:F3} offset={shipToOceanSnapHeightOffset:F3}", this);
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

        private Vector3[] ResolveShipContactSamples(Transform root, bool eightPoints)
        {
            Bounds? maybeBounds = ResolveBounds(root);
            if (!maybeBounds.HasValue)
            {
                Vector3 p = root.position + Vector3.down * (0.10f + hullContactExtraDown);
                return new[] { p };
            }

            Bounds b = maybeBounds.Value;
            float y = b.min.y - Mathf.Max(0f, hullContactExtraDown);

            float insetX = b.size.x * hullSampleInsetPercent;
            float insetZ = b.size.z * hullSampleInsetPercent;
            float minX = Mathf.Lerp(b.min.x, b.center.x, hullSampleInsetPercent);
            float maxX = Mathf.Lerp(b.max.x, b.center.x, hullSampleInsetPercent);
            float minZ = Mathf.Lerp(b.min.z, b.center.z, hullSampleInsetPercent);
            float maxZ = Mathf.Lerp(b.max.z, b.center.z, hullSampleInsetPercent);
            float cx = b.center.x;
            float cz = b.center.z;

            if (!eightPoints)
            {
                return new[] { new Vector3(cx, y, cz) };
            }

            return new[]
            {
                new Vector3(minX, y, maxZ), // front-left
                new Vector3(cx,   y, maxZ), // front-center
                new Vector3(maxX, y, maxZ), // front-right
                new Vector3(minX, y, cz),   // mid-left
                new Vector3(maxX, y, cz),   // mid-right
                new Vector3(minX, y, minZ), // rear-left
                new Vector3(cx,   y, minZ), // rear-center
                new Vector3(maxX, y, minZ), // rear-right
            };
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
            for (int i = 0; i < _lastCount; i++)
            {
                Gizmos.DrawWireSphere(_lastContacts[i], 0.09f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_lastSurfaces[i], 0.07f);
                Gizmos.DrawLine(_lastContacts[i], _lastSurfaces[i]);
                Gizmos.color = Color.magenta;
            }
        }
    }
}
