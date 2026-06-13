using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>Small physical station trigger usable by mouse, collision, or XR Simple Interactable.</summary>
    [DisallowMultipleComponent]
    public sealed class LrpDebugStationButton : MonoBehaviour
    {
        public enum Action
        {
            ToggleSail,
            ToggleAnchor,
            FireCannons,
            RaiseSail,
            LowerSail,
            LoadCannons,
            RepairLeaks,
            SteerLeft,
            SteerRight,
            CenterSteering
        }

        public Action action;
        public LivingRoomPiratesSurfaceDebugSandbox sandbox;

        private void Awake()
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
        }

        private void OnMouseDown() => InvokeAction();

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            string n = other.name.ToLowerInvariant();
            if (other.CompareTag("Player") || n.Contains("hand") || n.Contains("controller") || n.Contains("cannonball") || n.Contains("hammer"))
            {
                InvokeAction();
            }
        }

        public void InvokeAction()
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
            if (sandbox == null) return;

            switch (action)
            {
                case Action.ToggleSail: sandbox.ToggleSail(); break;
                case Action.ToggleAnchor: sandbox.ToggleAnchor(); break;
                case Action.FireCannons: sandbox.FireNearestCannonPublic(transform.position); break;
                case Action.RaiseSail: sandbox.AdjustSailPercent(0.12f); break;
                case Action.LowerSail: sandbox.AdjustSailPercent(-0.12f); break;
                case Action.LoadCannons: sandbox.LoadNearestCannonPublic(transform.position); break;
                case Action.RepairLeaks: sandbox.RepairLeaksPublic(); break;
                case Action.SteerLeft: sandbox.NudgeSteering(-0.18f); break;
                case Action.SteerRight: sandbox.NudgeSteering(0.18f); break;
                case Action.CenterSteering: sandbox.CenterSteering(); break;
            }
        }
    }
}
