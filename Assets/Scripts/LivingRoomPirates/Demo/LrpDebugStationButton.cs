using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Physical station control. InvokeAction is kept for keyboard/mouse compatibility;
    /// Begin/Drag/End are used by VR hands so grabbing does not instantly perform every action.
    /// </summary>
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
            CenterSteering,

            WheelKnob,
            SailRope,
            SailCleat,
            AnchorCapstan,
            AmmoSource,
            CannonLoadZone,
            CannonFuse,
            RepairHammer,
            RepairLeak,
            PortSheet,
            StarboardSheet
        }

        public Action action;
        public LivingRoomPiratesSurfaceDebugSandbox sandbox;

        private void Awake()
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
        }

        private void OnMouseDown() => InvokeAction();

        // Collision should not auto-fire/toggle anymore. Hands use LrpVrHandInteractionFallback.
        private void OnTriggerEnter(Collider other) { }

        public void BeginPhysicalGrab(Vector3 handWorld)
        {
            BeginPhysicalGrab(handWorld, 0);
        }

        public void BeginPhysicalGrab(Vector3 handWorld, int handId)
        {
            BeginPhysicalGrab(handWorld, transform.rotation, handId);
        }

        public void BeginPhysicalGrab(Vector3 handWorld, Quaternion handRotation, int handId)
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
            if (sandbox != null) sandbox.BeginPhysicalInteraction(action, transform, handWorld, handRotation, handId);
        }

        public void UpdatePhysicalGrab(Vector3 handWorld)
        {
            UpdatePhysicalGrab(handWorld, 0);
        }

        public void UpdatePhysicalGrab(Vector3 handWorld, int handId)
        {
            UpdatePhysicalGrab(handWorld, transform.rotation, handId);
        }

        public void UpdatePhysicalGrab(Vector3 handWorld, Quaternion handRotation, int handId)
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
            if (sandbox != null) sandbox.UpdatePhysicalInteraction(action, transform, handWorld, handRotation, handId);
        }

        public void EndPhysicalGrab(Vector3 handWorld)
        {
            EndPhysicalGrab(handWorld, 0);
        }

        public void EndPhysicalGrab(Vector3 handWorld, int handId)
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
            if (sandbox != null) sandbox.EndPhysicalInteraction(action, transform, handWorld, handId);
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
                case Action.AmmoSource: sandbox.BeginPhysicalInteraction(action, transform, transform.position); break;
                case Action.CannonLoadZone: sandbox.LoadNearestCannonPublic(transform.position); break;
                case Action.CannonFuse: sandbox.FireNearestCannonPublic(transform.position); break;
                case Action.RepairHammer:
                case Action.RepairLeak: sandbox.RepairLeaksPublic(); break;
            }
        }
    }
}
