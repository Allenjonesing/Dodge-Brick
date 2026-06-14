using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Input fallback for projects where the XR Interaction Toolkit rig is present
    /// visually but its Direct/Ray Interactors are not wired to generated runtime objects.
    /// It uses the actual tracked hand/controller transforms and XR grip/trigger buttons
    /// to select LRP station handles by proximity or laser raycast.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LrpVrHandInteractionFallback : MonoBehaviour
    {
        public bool runInEditor = true;
        public bool enableVrHands = true;
        public float directGrabRadius = 0.28f;
        public float rayDistance = 12f;
        public float rescanInterval = 1f;
        public LayerMask interactionMask = ~0;
        public bool drawDebugRays = false;

        private readonly List<Transform> _leftHands = new List<Transform>();
        private readonly List<Transform> _rightHands = new List<Transform>();
        private readonly Dictionary<LrpXrInteractableBridge, float> _nextContinuous = new Dictionary<LrpXrInteractableBridge, float>();
        private float _nextScan;
        private bool _leftWasPressed;
        private bool _rightWasPressed;

        private void OnEnable()
        {
            ScanHandsNow();
            _nextScan = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + Mathf.Max(0.25f, rescanInterval);
                ScanHandsNow();
            }

            if (enableVrHands)
            {
                HandleSide(XRNode.LeftHand, _leftHands, ref _leftWasPressed);
                HandleSide(XRNode.RightHand, _rightHands, ref _rightWasPressed);
            }

#if UNITY_EDITOR
            if (runInEditor && Input.GetMouseButtonDown(0))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                    LrpXrInteractableBridge bridge = FindBridgeByRay(ray);
                    if (bridge != null) bridge.InvokeFromFallback();
                }
            }
#endif
        }

        private void HandleSide(XRNode node, List<Transform> hands, ref bool wasPressed)
        {
            bool pressed = ReadPressed(node);
            bool pressedThisFrame = pressed && !wasPressed;
            wasPressed = pressed;

            if (!pressed) return;
            if (hands.Count == 0) return;

            LrpXrInteractableBridge best = null;
            foreach (Transform hand in hands)
            {
                if (hand == null) continue;

                if (drawDebugRays)
                    Debug.DrawRay(hand.position, hand.forward * rayDistance, Color.red, 0f, false);

                LrpXrInteractableBridge rayBridge = FindBridgeByRay(new Ray(hand.position, hand.forward));
                LrpXrInteractableBridge nearBridge = FindBridgeByProximity(hand.position);
                best = nearBridge != null ? nearBridge : rayBridge;
                if (best != null) break;
            }

            if (best == null) return;

            if (pressedThisFrame)
            {
                best.InvokeFromFallback();
                _nextContinuous[best] = Time.time + best.FallbackContinuousInterval();
            }
            else if (best.CanFallbackContinuously())
            {
                float next;
                if (!_nextContinuous.TryGetValue(best, out next) || Time.time >= next)
                {
                    best.InvokeFromFallback();
                    _nextContinuous[best] = Time.time + best.FallbackContinuousInterval();
                }
            }
        }

        private bool ReadPressed(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return false;

            bool gripButton;
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out gripButton) && gripButton)
                return true;

            bool triggerButton;
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButton) && triggerButton)
                return true;

            float grip;
            if (device.TryGetFeatureValue(CommonUsages.grip, out grip) && grip > 0.65f)
                return true;

            float trigger;
            if (device.TryGetFeatureValue(CommonUsages.trigger, out trigger) && trigger > 0.65f)
                return true;

            return false;
        }

        private LrpXrInteractableBridge FindBridgeByRay(Ray ray)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, rayDistance, interactionMask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0) return null;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null) continue;
                LrpXrInteractableBridge bridge = c.GetComponentInParent<LrpXrInteractableBridge>();
                if (bridge != null && bridge.enabled && bridge.gameObject.activeInHierarchy)
                    return bridge;
            }
            return null;
        }

        private LrpXrInteractableBridge FindBridgeByProximity(Vector3 position)
        {
            Collider[] cols = Physics.OverlapSphere(position, directGrabRadius, interactionMask, QueryTriggerInteraction.Collide);
            LrpXrInteractableBridge best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < cols.Length; i++)
            {
                Collider c = cols[i];
                if (c == null) continue;
                LrpXrInteractableBridge bridge = c.GetComponentInParent<LrpXrInteractableBridge>();
                if (bridge == null || !bridge.enabled || !bridge.gameObject.activeInHierarchy) continue;
                float d = (c.bounds.ClosestPoint(position) - position).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = bridge;
                }
            }
            return best;
        }

        [ContextMenu("Scan Hands Now")]
        public void ScanHandsNow()
        {
            _leftHands.Clear();
            _rightHands.Clear();

            GameObject[] all = FindObjectsOfType<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i];
                if (go == null || !go.activeInHierarchy) continue;
                string n = go.name.ToLowerInvariant();
                bool isHandish = n.Contains("hand") || n.Contains("controller");
                if (!isHandish) continue;

                bool hasUsefulComponent = HasComponentName(go, "XRController")
                    || HasComponentName(go, "XRRayInteractor")
                    || HasComponentName(go, "XRDirectInteractor")
                    || HasComponentName(go, "ActionBasedController");

                // The sample project uses blue visual hand meshes; keep them as a fallback
                // only when clearly named left/right hand/controller.
                if (!hasUsefulComponent && !(n.Contains("hand") && (n.Contains("left") || n.Contains("right"))))
                    continue;

                if (n.Contains("left")) AddUnique(_leftHands, go.transform);
                if (n.Contains("right")) AddUnique(_rightHands, go.transform);
            }
        }

        private static bool HasComponentName(GameObject go, string contains)
        {
            Component[] comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null) continue;
                if (c.GetType().Name.Contains(contains)) return true;
            }
            return false;
        }

        private static void AddUnique(List<Transform> list, Transform t)
        {
            if (t == null || list.Contains(t)) return;
            list.Add(t);
        }
    }
}
