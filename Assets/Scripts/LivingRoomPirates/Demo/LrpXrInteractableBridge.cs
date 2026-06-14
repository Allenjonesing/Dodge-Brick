using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Version-compatible XR bridge for the old XR Interaction Toolkit in this project.
    /// It avoids SelectEnterEventArgs/ActivateEventArgs and makes station handles selectable
    /// by both ray and direct interactors. Selection/grab triggers the assigned station action.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LrpXrInteractableBridge : MonoBehaviour
    {
        public LrpDebugStationButton button;
        public bool invokeOnSelect = true;
        public bool invokeOnActivate = true;
        public bool continuousWhileSelected = false;
        public float continuousInterval = 0.08f;

        [Header("Debug")]
        public bool showHoverColor = true;

        private XRBaseInteractable _interactable;
        private Component _interactionManager;
        private bool _wasSelected;
        private float _nextContinuousTime;
        private PropertyInfo _isSelectedProperty;
        private Renderer _renderer;
        private Color _baseColor;

        private void Awake()
        {
            if (button == null) button = GetComponent<LrpDebugStationButton>();
            _renderer = GetComponent<Renderer>();
            if (_renderer != null && _renderer.material != null) _baseColor = _renderer.material.color;
            EnsureInteractable();
            CacheReflection();
        }

        private void OnEnable()
        {
            EnsureInteractable();
            CacheReflection();
            _wasSelected = IsSelected();
        }

        private void Update()
        {
            bool selected = IsSelected();

            if (selected && !_wasSelected)
            {
                _nextContinuousTime = 0f;
                if ((invokeOnSelect || invokeOnActivate) && button != null)
                    button.InvokeAction();
            }

            if (selected && continuousWhileSelected && button != null)
            {
                if (Time.time >= _nextContinuousTime)
                {
                    _nextContinuousTime = Time.time + Mathf.Max(0.01f, continuousInterval);
                    button.InvokeAction();
                }
            }

            if (showHoverColor && _renderer != null && _renderer.material != null)
                _renderer.material.color = selected ? Color.yellow : _baseColor;

            _wasSelected = selected;
        }


        public void RepairForXr(Component interactionManager)
        {
            _interactionManager = interactionManager;
            EnsureInteractable();
            if (_interactable != null)
                LrpXrRigAutoConfigurator.AssignInteractionManager(_interactable, _interactionManager);
            CacheReflection();
        }


        public void InvokeFromFallback()
        {
            if (button != null)
                button.InvokeAction();
        }

        public bool CanFallbackContinuously()
        {
            return continuousWhileSelected;
        }

        public float FallbackContinuousInterval()
        {
            return Mathf.Max(0.01f, continuousInterval);
        }

        private void EnsureInteractable()
        {
            Collider col = GetComponent<Collider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            _interactable = GetComponent<XRBaseInteractable>();
            if (_interactable == null)
            {
                // Safe default: use SimpleInteractable for ray/direct select events.
                // XRGrabInteractable can fight generated kinematic station props and was
                // part of the v32 crash/rig-instability path. Cannonballs/hammer can
                // later use real XRGrabInteractable prefabs once the rig is stable.
                _interactable = gameObject.AddComponent<XRSimpleInteractable>();
            }

            if (_interactable != null && _interactionManager == null)
            {
                System.Type managerType = LrpXrRigAutoConfigurator.FindType(
                    "UnityEngine.XR.Interaction.Toolkit.XRInteractionManager, Unity.XR.Interaction.Toolkit",
                    "UnityEngine.XR.Interaction.Toolkit.Interaction.XRInteractionManager, Unity.XR.Interaction.Toolkit");
                if (managerType != null)
                    _interactionManager = FindObjectOfType(managerType) as Component;
            }

            if (_interactable != null && _interactionManager != null)
                LrpXrRigAutoConfigurator.AssignInteractionManager(_interactable, _interactionManager);
        }

        private void CacheReflection()
        {
            _isSelectedProperty = null;
            if (_interactable == null) return;

            _isSelectedProperty = _interactable.GetType().GetProperty(
                "isSelected",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private bool IsSelected()
        {
            if (_interactable == null) return false;

            if (_isSelectedProperty != null && _isSelectedProperty.PropertyType == typeof(bool))
            {
                object value = _isSelectedProperty.GetValue(_interactable, null);
                return value is bool b && b;
            }

            return false;
        }
    }
}
