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

        private void EnsureInteractable()
        {
            Collider col = GetComponent<Collider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = false;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;

            _interactable = GetComponent<XRBaseInteractable>();
            if (_interactable == null)
            {
                // Prefer XRGrabInteractable so default XR rigs treat handles as actually grabbable.
                // Fall back to XRSimpleInteractable for older/minimal setups.
                System.Type grabType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
                if (grabType == null)
                    grabType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit");

                if (grabType != null && typeof(XRBaseInteractable).IsAssignableFrom(grabType))
                    _interactable = (XRBaseInteractable)gameObject.AddComponent(grabType);
                else
                    _interactable = gameObject.AddComponent<XRSimpleInteractable>();
            }
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
