using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Snaps a floating object to the visible Water1/OceanWorldController surface.
    /// This is intentionally simple: sample at the object's X/Z center and move only Y.
    /// Use contactMode=Bottom when the object should sit on the surface; Center when the
    /// object's origin should be the contact point.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class SurfaceFloatFollower : MonoBehaviour
    {
        public enum ContactMode
        {
            Center,
            RendererBottom
        }

        public OceanWorldController ocean;
        public ContactMode contactMode = ContactMode.RendererBottom;
        public float heightOffset = -0.02f;
        public bool snapInstantly = true;
        public bool alignToNormal = true;
        [Tooltip("Moves this object with the Water1 conveyor so debris/enemies appear to drift past the locked ship.")]
        public bool moveWithWaterConveyor = true;
        public float conveyorMultiplier = 1f;
        public float positionSharpness = 24f;
        public float rotationSharpness = 8f;

        private Renderer[] _renderers;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        private void LateUpdate()
        {
            if (ocean == null)
            {
                ocean = OceanWorldController.Instance != null ? OceanWorldController.Instance : FindObjectOfType<OceanWorldController>();
            }

            if (ocean == null)
            {
                return;
            }

            Vector3 p = transform.position;

            if (moveWithWaterConveyor && ocean != null && ocean.enableWaterEscalatorTravel)
            {
                Vector2 dir = ocean.waterEscalatorDirection.sqrMagnitude > 0.0001f
                    ? ocean.waterEscalatorDirection.normalized
                    : Vector2.up;
                Vector3 step = new Vector3(-dir.x, 0f, -dir.y) * ocean.waterEscalatorSpeed * conveyorMultiplier * Time.deltaTime;
                p += step;
            }

            float waterY = ocean.SampleHeight(p);
            float contactY = ResolveContactY();
            float delta = (waterY + heightOffset) - contactY;
            float targetY = transform.position.y + delta;

            if (snapInstantly || positionSharpness <= 0f)
            {
                p.y = targetY;
            }
            else
            {
                p.y = Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-positionSharpness * Time.deltaTime));
            }

            transform.position = p;

            if (alignToNormal)
            {
                Vector3 normal = ocean.SampleNormal(transform.position);
                if (normal.sqrMagnitude > 0.001f)
                {
                    Quaternion target = Quaternion.FromToRotation(transform.up, normal.normalized) * transform.rotation;
                    transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
                }
            }
        }

        private float ResolveContactY()
        {
            if (contactMode == ContactMode.Center)
            {
                return transform.position.y;
            }

            Bounds? bounds = ResolveBounds();
            return bounds.HasValue ? bounds.Value.min.y : transform.position.y;
        }

        private Bounds? ResolveBounds()
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }

            Bounds bounds = default;
            bool found = false;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null || !r.enabled)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return found ? bounds : (Bounds?)null;
        }
    }
}
