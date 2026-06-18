using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Prevents generated ship/station props from physically pushing the XR rig.
    /// The red laser/direct hands can still select trigger colliders, but the
    /// CharacterController should not be launched upward by solid primitive props.
    /// </summary>
    public sealed class LrpVrPhysicsSanitizer : MonoBehaviour
    {
        public bool runOnStart = true;
        public bool keepScanningBriefly = true;
        public float scanDuration = 8f;
        public float scanInterval = 0.5f;

        private float _stopTime;
        private float _nextScan;

        private void Start()
        {
            _stopTime = Time.time + scanDuration;
            if (runOnStart) SanitizeNow();
        }

        private void Update()
        {
            if (!keepScanningBriefly || Time.time > _stopTime || Time.time < _nextScan) return;
            _nextScan = Time.time + Mathf.Max(0.1f, scanInterval);
            SanitizeNow();
        }

        [ContextMenu("Sanitize LRP VR Colliders Now")]
        public void SanitizeNow()
        {
            foreach (Collider col in FindObjectsOfType<Collider>())
            {
                if (col == null || !IsLrpGeneratedObject(col.transform)) continue;

                string n = col.gameObject.name.ToLowerInvariant();

                // Keep true projectiles and dropped tools physical. Everything else is a station/ship prop.
                bool shouldRemainPhysical = n.Contains("projectile") || n.Contains("firedcannonball") || n.Contains("dropped") || n.Contains("hammer_tool_physics");
                if (!shouldRemainPhysical)
                {
                    col.isTrigger = true;
                }

                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null && !shouldRemainPhysical)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.constraints = RigidbodyConstraints.FreezeAll;
                }
            }
        }

        private static bool IsLrpGeneratedObject(Transform t)
        {
            while (t != null)
            {
                string n = t.name;
                if (n == "LivingRoomPiratesRoot" || n == "shipGeneratedRoot" || n == "OceanWorldRoot") return true;
                if (n.StartsWith("LRP_") || n.StartsWith("Water1_Tile_")) return true;
                t = t.parent;
            }
            return false;
        }
    }
}
