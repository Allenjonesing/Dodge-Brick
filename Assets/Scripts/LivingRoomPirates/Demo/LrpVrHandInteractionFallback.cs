using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Direct-hand interaction fallback. No ray/laser gameplay: hands must be near objects.
    /// Grip begins a grab, hand movement drives station mechanics, release ends it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LrpVrHandInteractionFallback : MonoBehaviour
    {
        public bool runInEditor = true;
        public bool enableVrHands = true;
        public bool disableRayInteractors = true;
        public float directGrabRadius = 0.30f;
        public float rescanInterval = 1f;
        public LayerMask interactionMask = ~0;

        private readonly List<Transform> _leftHands = new List<Transform>();
        private readonly List<Transform> _rightHands = new List<Transform>();
        private float _nextScan;
        private HandState _left;
        private HandState _right;

        private struct HandState
        {
            public bool wasPressed;
            public LrpXrInteractableBridge grabbed;
        }

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

            ClearHoverIndicators();

            if (enableVrHands)
            {
                HandleSide(XRNode.LeftHand, _leftHands, ref _left);
                HandleSide(XRNode.RightHand, _rightHands, ref _right);
            }

#if UNITY_EDITOR
            if (runInEditor && Input.GetMouseButtonDown(0))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    LrpXrInteractableBridge bridge = FindBridgeByProximity(cam.transform.position + cam.transform.forward * 0.6f);
                    if (bridge != null) bridge.BeginFromFallback(bridge.transform.position);
                }
            }
#endif
        }

        private void HandleSide(XRNode node, List<Transform> hands, ref HandState state)
        {
            Transform hand = BestHand(hands);
            if (hand == null) return;

            bool pressed = ReadPressed(node);
            bool pressedThisFrame = pressed && !state.wasPressed;
            bool releasedThisFrame = !pressed && state.wasPressed;
            state.wasPressed = pressed;

            if (state.grabbed == null)
            {
                LrpXrInteractableBridge hover = FindBridgeByProximity(hand.position);
                if (hover != null) hover.SetHover(true, false);
                if (pressedThisFrame && hover != null)
                {
                    state.grabbed = hover;
                    state.grabbed.BeginFromFallback(hand.position);
                }
            }
            else
            {
                state.grabbed.SetHover(true, true);
                if (pressed)
                    state.grabbed.UpdateFromFallback(hand.position);
                if (releasedThisFrame)
                {
                    state.grabbed.EndFromFallback(hand.position);
                    state.grabbed = null;
                }
            }
        }

        private Transform BestHand(List<Transform> hands)
        {
            for (int i = 0; i < hands.Count; i++)
            {
                if (hands[i] != null && hands[i].gameObject.activeInHierarchy) return hands[i];
            }
            return null;
        }

        private bool ReadPressed(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return false;

            bool gripButton;
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out gripButton) && gripButton) return true;
            float grip;
            if (device.TryGetFeatureValue(CommonUsages.grip, out grip) && grip > 0.55f) return true;
            return false;
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

        private void ClearHoverIndicators()
        {
            for (int i = LrpXrInteractableBridge.All.Count - 1; i >= 0; i--)
            {
                LrpXrInteractableBridge b = LrpXrInteractableBridge.All[i];
                if (b == null) { LrpXrInteractableBridge.All.RemoveAt(i); continue; }
                b.SetHover(false, false);
            }
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

                if (disableRayInteractors) DisableRayInteractorIfPresent(go);

                string n = go.name.ToLowerInvariant();
                bool isHandish = n.Contains("hand") || n.Contains("controller");
                if (!isHandish) continue;

                bool hasUsefulComponent = HasComponentName(go, "XRController")
                    || HasComponentName(go, "XRDirectInteractor")
                    || HasComponentName(go, "ActionBasedController")
                    || HasComponentName(go, "HandPresence");

                if (!hasUsefulComponent && !(n.Contains("hand") && (n.Contains("left") || n.Contains("right"))))
                    continue;

                if (n.Contains("left")) AddUnique(_leftHands, go.transform);
                if (n.Contains("right")) AddUnique(_rightHands, go.transform);
            }
        }

        private static void DisableRayInteractorIfPresent(GameObject go)
        {
            Component[] comps = go.GetComponents<Component>();
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null) continue;
                if (c.GetType().Name.Contains("XRRayInteractor") || c.GetType().Name.Contains("LineVisual"))
                {
                    Behaviour b = c as Behaviour;
                    if (b != null) b.enabled = false;
                }
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
