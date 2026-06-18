using System.Collections;
using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Quest/OVR/OpenXR boundary geometry is often unavailable during AfterSceneLoad.
    /// This component waits and retries before allowing BoundaryShipGenerator to fall
    /// back to the compact stationary dinghy. This is intentionally the only runtime
    /// path that calls RegenerateShip from the primitive installer.
    /// </summary>
    public sealed class LrpBoundaryDelayedShipStartup : MonoBehaviour
    {
        public BoundaryShipGenerator generator;
        public Transform shipRoot;
        public OceanWorldController ocean;
        public float timeoutSeconds = 10f;
        public float retryIntervalSeconds = 0.5f;

        private bool _started;

        private void OnEnable()
        {
            if (!_started)
            {
                _started = true;
                StartCoroutine(Run());
            }
        }

        private IEnumerator Run()
        {
            if (generator == null)
                yield break;

#if UNITY_ANDROID && !UNITY_EDITOR
            float elapsed = 0f;
            bool validBoundary = false;

            // Give OVR/OpenXR one frame plus a short settle period before first query.
            yield return null;
            yield return new WaitForSeconds(1.25f);

            while (elapsed < timeoutSeconds)
            {
                validBoundary = generator.TryDetectValidBoundaryNow();
                Debug.Log($"[LRP Boundary Startup] try t={elapsed:F1}s valid={validBoundary} source={generator.LastBoundarySource} points={generator.LastBoundaryPointCount} size={generator.DetectedWidth:F2}x{generator.DetectedDepth:F2}");
                // Keep retrying for a larger Guardian if the first valid result looks like
                // stationary bounds. Quest can report small bounds first, then roomscale.
                if (validBoundary && generator.DetectedWidth * generator.DetectedDepth >= 6.0f)
                    break;

                elapsed += retryIntervalSeconds;
                yield return new WaitForSeconds(retryIntervalSeconds);
            }

            if (!validBoundary)
            {
                Debug.LogWarning("[LRP Boundary Startup] No valid roomscale boundary after retry window; generating stationary fallback dinghy.");
            }
#else
            yield return null;
#endif

            // Generate the actual boundary-sized ship FIRST. The generator clears shipRoot,
            // so any debug/station systems must be built AFTER this point or they get deleted.
            generator.RegenerateShip();
            LivingRoomPiratesPrimitiveSceneInstaller.CreateOrUpdateShipBottomContact(shipRoot, ocean);

            LivingRoomPiratesSurfaceDebugSandbox sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
            if (sandbox != null)
            {
                sandbox.shipRoot = shipRoot;
                sandbox.ocean = ocean;
                sandbox.waterOne = GameObject.Find("Water1") != null ? GameObject.Find("Water1").transform : null;
                sandbox.enabled = true;
                sandbox.BuildOrRepair();
            }

            LivingRoomPiratesPrimitiveSceneInstaller.AddPrimitiveBehavioursToGeneratedStations(shipRoot);

            Debug.Log($"[LRP Boundary Startup] generated ship. source={generator.LastBoundarySource} points={generator.LastBoundaryPointCount} detected={generator.DetectedWidth:F2}x{generator.DetectedDepth:F2} usable={generator.UsableWidth:F2}x{generator.UsableDepth:F2} tier={generator.CurrentTier}");
        }
    }
}
