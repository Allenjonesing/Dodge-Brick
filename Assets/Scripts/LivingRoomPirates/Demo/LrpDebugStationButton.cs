using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Simple editor/VR-friendly station button. XR hands can hit the collider;
    /// mouse clicks also work in the editor. It calls the active debug sandbox.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LrpDebugStationButton : MonoBehaviour
    {
        public enum Action { ToggleSail, ToggleAnchor, FireCannons, RaiseSail, LowerSail, LoadCannons, RepairLeaks }
        public Action action;
        public LivingRoomPiratesSurfaceDebugSandbox sandbox;

        private void Awake()
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
        }

        private void OnMouseDown()
        {
            InvokeAction();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other != null && (other.CompareTag("Player") || other.name.ToLowerInvariant().Contains("hand") || other.name.ToLowerInvariant().Contains("controller")))
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
                case Action.FireCannons: sandbox.FireAllCannonsPublic(); break;
                case Action.RaiseSail: sandbox.AdjustSailPercent(0.15f); break;
                case Action.LowerSail: sandbox.AdjustSailPercent(-0.15f); break;
                case Action.LoadCannons: sandbox.LoadAllCannonsPublic(); break;
                case Action.RepairLeaks: sandbox.RepairLeaksPublic(); break;
            }
        }
    }
}
