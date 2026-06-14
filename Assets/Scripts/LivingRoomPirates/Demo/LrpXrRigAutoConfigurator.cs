using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// SAFE XR rig helper. v32 auto-installed direct interactors/colliders onto
    /// broad "hand/controller" candidates, which could destabilize some rigs.
    /// This version does NOT auto-install. If present in scene, it only repairs
    /// LRP station interactables for ray selection by default. Direct hand repair
    /// is opt-in and tightly filtered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LrpXrRigAutoConfigurator : MonoBehaviour
    {
        public bool configureOnStart = true;
        public bool keepScanningBriefly = true;
        public float directGrabRadius = 0.14f;

        [Header("Mode")]
        [Tooltip("When true, hand/direct-grab repair only runs when Unity reports an XR headset/device is active. Editor-without-headset stays safe.")]
        public bool onlyModifyHandsWhenHeadsetActive = true;
        [Tooltip("Allow the safer station/ray interactable repair in editor even without a headset.")]
        public bool repairStationInteractablesInEditor = true;

        [Header("Headset hand interaction repair")]
        public bool addDirectInteractorToHands = true;
        public bool addRayInteractorIfMissing = false;
        public bool addHandTriggerCollider = true;
        public bool addHandRigidbody = true;
        public bool makeHandColliderTrigger = true;

        private float _stopScanningAt;

        // Deliberately no RuntimeInitializeOnLoadMethod here.
        // The primitive installer may add this component, but we do not silently
        // modify every XR rig in the scene before the user's rig has initialized.

        private void Start()
        {
            if (!configureOnStart) return;
            ConfigureNow();
            if (keepScanningBriefly)
            {
                _stopScanningAt = Time.time + 2f;
                StartCoroutine(ConfigureForSeveralFrames());
            }
        }

        private IEnumerator ConfigureForSeveralFrames()
        {
            while (Time.time < _stopScanningAt)
            {
                ConfigureNow();
                yield return new WaitForSeconds(0.75f);
            }
        }

        [ContextMenu("Configure XR Rig For LRP Grabbing")]
        public void ConfigureNow()
        {
            Component manager = EnsureInteractionManager();

            bool headsetActive = IsHeadsetActive();
            bool mayModifyHands = !onlyModifyHandsWhenHeadsetActive || headsetActive;

            // Station interactables are lightweight and safe in both Editor and headset mode.
            if (repairStationInteractablesInEditor || headsetActive)
                RepairLrpInteractables(manager);

            // Hand/controller modification is the part that could destabilize editor rigs,
            // so only do it when a headset is actually active unless explicitly overridden.
            if (mayModifyHands && (addDirectInteractorToHands || addRayInteractorIfMissing || addHandTriggerCollider || addHandRigidbody))
                ConfigureCandidateHands(manager);
        }

        private static bool IsHeadsetActive()
        {
            try
            {
                if (XRSettings.enabled && XRSettings.isDeviceActive)
                    return true;
            }
            catch { }

            try
            {
                InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (head.isValid)
                    return true;
            }
            catch { }

            return false;
        }

        private static Component EnsureInteractionManager()
        {
            Type managerType = FindType(
                "UnityEngine.XR.Interaction.Toolkit.XRInteractionManager, Unity.XR.Interaction.Toolkit",
                "UnityEngine.XR.Interaction.Toolkit.Interaction.XRInteractionManager, Unity.XR.Interaction.Toolkit");

            if (managerType == null) return null;

            Component existing = FindObjectOfType(managerType) as Component;
            if (existing != null) return existing;

            GameObject go = new GameObject("XR Interaction Manager (LRP Auto)");
            return go.AddComponent(managerType) as Component;
        }

        private void ConfigureCandidateHands(Component manager)
        {
            Type xrControllerType = FindType(
                "UnityEngine.XR.Interaction.Toolkit.XRController, Unity.XR.Interaction.Toolkit",
                "UnityEngine.XR.Interaction.Toolkit.Interactors.XRController, Unity.XR.Interaction.Toolkit");
            Type directInteractorType = FindType(
                "UnityEngine.XR.Interaction.Toolkit.XRDirectInteractor, Unity.XR.Interaction.Toolkit",
                "UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor, Unity.XR.Interaction.Toolkit");
            Type rayInteractorType = FindType(
                "UnityEngine.XR.Interaction.Toolkit.XRRayInteractor, Unity.XR.Interaction.Toolkit",
                "UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor, Unity.XR.Interaction.Toolkit");

            GameObject[] all = FindObjectsOfType<GameObject>();
            int configured = 0;

            foreach (GameObject go in all)
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!IsStrictHandController(go, xrControllerType, rayInteractorType, directInteractorType)) continue;

                if (addDirectInteractorToHands && directInteractorType != null && go.GetComponent(directInteractorType) == null)
                {
                    Component direct = go.AddComponent(directInteractorType) as Component;
                    AssignInteractionManager(direct, manager);
                }

                if (addRayInteractorIfMissing && rayInteractorType != null && go.GetComponent(rayInteractorType) == null)
                {
                    Component ray = go.AddComponent(rayInteractorType) as Component;
                    AssignInteractionManager(ray, manager);
                }

                if (addHandTriggerCollider)
                {
                    SphereCollider sphere = go.GetComponent<SphereCollider>();
                    if (sphere == null) sphere = go.AddComponent<SphereCollider>();
                    sphere.isTrigger = makeHandColliderTrigger;
                    sphere.radius = Mathf.Clamp(directGrabRadius, 0.04f, 0.25f);
                    sphere.center = Vector3.zero;
                }

                if (addHandRigidbody)
                {
                    Rigidbody rb = go.GetComponent<Rigidbody>();
                    if (rb == null) rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }

                configured++;
            }

            if (configured > 0)
                Debug.Log($"[LRP XR] Safely configured {configured} hand/controller objects.");
        }

        private static bool IsStrictHandController(GameObject go, Type xrControllerType, Type rayInteractorType, Type directInteractorType)
        {
            string n = go.name.ToLowerInvariant();
            bool sideName = n.Contains("left") || n.Contains("right");
            bool handName = n.Contains("hand") || n.Contains("controller");
            if (!sideName || !handName) return false;

            // Also require an actual XR component. This prevents adding colliders/
            // rigidbodies to Camera Offset, visual hand meshes, or unrelated objects.
            bool hasXrController = xrControllerType != null && go.GetComponent(xrControllerType) != null;
            bool hasRay = rayInteractorType != null && go.GetComponent(rayInteractorType) != null;
            bool hasDirect = directInteractorType != null && go.GetComponent(directInteractorType) != null;
            return hasXrController || hasRay || hasDirect;
        }

        private static void RepairLrpInteractables(Component manager)
        {
            LrpXrInteractableBridge[] bridges = FindObjectsOfType<LrpXrInteractableBridge>();
            foreach (LrpXrInteractableBridge bridge in bridges)
            {
                if (bridge == null) continue;
                bridge.RepairForXr(manager);
            }
        }

        public static Type FindType(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Type t = Type.GetType(names[i]);
                if (t != null) return t;
            }
            return null;
        }

        public static void AssignInteractionManager(Component component, Component manager)
        {
            if (component == null || manager == null) return;
            Type t = component.GetType();
            string[] names = { "interactionManager", "m_InteractionManager" };
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo f = t.GetField(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null && f.FieldType.IsAssignableFrom(manager.GetType()))
                {
                    f.SetValue(component, manager);
                    return;
                }

                PropertyInfo p = t.GetProperty(names[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite && p.PropertyType.IsAssignableFrom(manager.GetType()))
                {
                    p.SetValue(component, manager, null);
                    return;
                }
            }
        }
    }
}
