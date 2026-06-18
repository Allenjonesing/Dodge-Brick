using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Conservative Quest performance helper.
    /// Previous versions replaced Unity primitive meshes with custom meshes and some
    /// of those custom meshes were winding-inverted in this Unity/shader setup.
    /// This version does NOT replace renderer meshes. It only reduces expensive
    /// runtime costs that are safe: shadows, reflection probes, and excess scans.
    /// </summary>
    public sealed class LrpPrimitivePerformanceOptimizer : MonoBehaviour
    {
        public bool optimizeOnStart = true;
        public bool keepScanningBriefly = false;
        public float scanInterval = 1f;
        public float scanDuration = 3f;

        private float _nextScan;
        private float _stopTime;

        private void Start()
        {
            _stopTime = Time.time + scanDuration;
            if (optimizeOnStart) OptimizeNow();
        }

        private void Update()
        {
            if (!keepScanningBriefly || Time.time > _stopTime || Time.time < _nextScan) return;
            _nextScan = Time.time + Mathf.Max(0.2f, scanInterval);
            OptimizeNow();
        }

        [ContextMenu("Optimize Primitive Renderers Now")]
        public void OptimizeNow()
        {
            foreach (Renderer r in FindObjectsOfType<Renderer>())
            {
                if (r == null) continue;
                string n = r.gameObject.name.ToLowerInvariant();
                if (!n.Contains("water") && !n.Contains("cannon") && !n.Contains("knob") && !n.Contains("rope") && !n.Contains("barrel") && !n.Contains("sail") && !n.Contains("anchor") && !n.Contains("deck") && !n.Contains("rail") && !n.Contains("mast") && !n.Contains("debris") && !n.Contains("leak") && !n.Contains("ball"))
                    continue;

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
        }
    }
}
