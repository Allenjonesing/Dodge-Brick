using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Lightweight bridge for physical VR interaction. The fallback hand system calls
    /// Begin/Update/End so station logic can be driven by hand movement, not grab toggles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LrpXrInteractableBridge : MonoBehaviour
    {
        public static readonly List<LrpXrInteractableBridge> All = new List<LrpXrInteractableBridge>();

        public LrpDebugStationButton button;
        public bool invokeOnSelect = false;
        public bool invokeOnActivate = false;
        public bool continuousWhileSelected = false;
        public float continuousInterval = 0.08f;

        [Header("Hover")]
        public bool showHoverIndicator = true;
        public Vector3 hoverPadding = new Vector3(0.06f, 0.06f, 0.06f);
        [Header("Context indicators")]
        public bool forceShowIndicator = false;
        public Color forcedIndicatorColor = new Color(1f, 0.92f, 0.15f, 0.30f);

        private XRBaseInteractable _interactable;
        private Component _interactionManager;
        private bool _wasSelected;
        private float _nextContinuousTime;
        private PropertyInfo _isSelectedProperty;
        private GameObject _hoverBox;
        private Renderer _hoverRenderer;
        private bool _grabbedByFallback;

        private void Awake()
        {
            if (button == null) button = GetComponent<LrpDebugStationButton>();
            EnsureInteractable();
            CacheReflection();
            EnsureHoverIndicator();
        }

        private void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            EnsureInteractable();
            CacheReflection();
            EnsureHoverIndicator();
            _wasSelected = IsSelected();
        }

        private void OnDisable()
        {
            All.Remove(this);
            SetHover(false, false);
        }

        private void Update()
        {
            bool selected = IsSelected();

            // XR toolkit selection is treated as "held", not as an instant button press.
            if (selected && !_wasSelected)
                BeginFromFallback(transform.position);
            else if (selected)
                UpdateFromFallback(transform.position);
            else if (!selected && _wasSelected)
                EndFromFallback(transform.position);

            if (selected && continuousWhileSelected && button != null)
            {
                if (Time.time >= _nextContinuousTime)
                {
                    _nextContinuousTime = Time.time + Mathf.Max(0.01f, continuousInterval);
                    button.UpdatePhysicalGrab(transform.position);
                }
            }

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

        public void InvokeFromFallback() => BeginFromFallback(transform.position);

        public void BeginFromFallback(Vector3 handWorld)
        {
            BeginFromFallback(handWorld, 0);
        }

        public void BeginFromFallback(Vector3 handWorld, int handId)
        {
            BeginFromFallback(handWorld, transform.rotation, handId);
        }

        public void BeginFromFallback(Vector3 handWorld, Quaternion handRotation, int handId)
        {
            _grabbedByFallback = true;
            _nextContinuousTime = 0f;
            SetHover(true, true);
            if (button != null) button.BeginPhysicalGrab(handWorld, handRotation, handId);
        }

        public void UpdateFromFallback(Vector3 handWorld)
        {
            UpdateFromFallback(handWorld, 0);
        }

        public void UpdateFromFallback(Vector3 handWorld, int handId)
        {
            UpdateFromFallback(handWorld, transform.rotation, handId);
        }

        public void UpdateFromFallback(Vector3 handWorld, Quaternion handRotation, int handId)
        {
            if (!_grabbedByFallback) BeginFromFallback(handWorld, handRotation, handId);
            SetHover(true, true);
            if (button != null) button.UpdatePhysicalGrab(handWorld, handRotation, handId);
        }

        public void EndFromFallback(Vector3 handWorld)
        {
            EndFromFallback(handWorld, 0);
        }

        public void EndFromFallback(Vector3 handWorld, int handId)
        {
            if (button != null) button.EndPhysicalGrab(handWorld, handId);
            _grabbedByFallback = false;
            SetHover(false, false);
        }

        public bool CanFallbackContinuously() { return continuousWhileSelected; }
        public float FallbackContinuousInterval() { return Mathf.Max(0.01f, continuousInterval); }

        public void SetHover(bool hovered, bool grabbed)
        {
            if (_hoverBox == null) EnsureHoverIndicator();
            if (_hoverBox == null) return;
            bool active = showHoverIndicator && (forceShowIndicator || hovered || grabbed);
            _hoverBox.SetActive(active);
            if (_hoverRenderer != null)
            {
                Color c = forceShowIndicator ? forcedIndicatorColor : (grabbed ? new Color(0.20f, 0.75f, 1f, 0.34f) : new Color(0.20f, 1f, 0.25f, 0.22f));
                _hoverRenderer.material.color = c;
            }
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
                _interactable = gameObject.AddComponent<XRSimpleInteractable>();

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

        private void EnsureHoverIndicator()
        {
            if (_hoverBox != null) return;
            _hoverBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _hoverBox.name = "LRP_HoverIndicator_green_direct_grab_only";
            _hoverBox.transform.SetParent(transform, false);
            _hoverBox.transform.localPosition = Vector3.zero;
            Collider c = _hoverBox.GetComponent<Collider>(); if (c != null) Destroy(c);
            _hoverRenderer = _hoverBox.GetComponent<Renderer>();
            if (_hoverRenderer != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.20f, 1f, 0.25f, 0.20f);
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
                _hoverRenderer.material = m;
            }
            Bounds b = new Bounds(transform.position, Vector3.one * 0.18f);
            Collider own = GetComponent<Collider>();
            if (own != null) b = own.bounds;
            Vector3 worldSize = b.size + hoverPadding;
            Vector3 lossy = transform.lossyScale;
            _hoverBox.transform.localScale = new Vector3(
                worldSize.x / Mathf.Max(0.001f, Mathf.Abs(lossy.x)),
                worldSize.y / Mathf.Max(0.001f, Mathf.Abs(lossy.y)),
                worldSize.z / Mathf.Max(0.001f, Mathf.Abs(lossy.z)));
            _hoverBox.SetActive(false);
        }

        private void CacheReflection()
        {
            _isSelectedProperty = null;
            if (_interactable == null) return;
            _isSelectedProperty = _interactable.GetType().GetProperty("isSelected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
