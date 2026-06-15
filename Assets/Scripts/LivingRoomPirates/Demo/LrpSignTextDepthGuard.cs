using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// TextMesh can be visually confusing in VR if it remains visible through ship
    /// geometry. This hides sign text whenever another collider blocks the view
    /// between the camera and the sign face.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class LrpSignTextDepthGuard : MonoBehaviour
    {
        public Transform boardRoot;
        public float checkInterval = 0.08f;
        private Renderer _renderer;
        private Camera _camera;
        private float _nextCheck;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + checkInterval;
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_camera == null) _camera = Camera.main;
            if (_renderer == null || _camera == null) return;

            Vector3 from = _camera.transform.position;
            Vector3 to = transform.position;
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist <= 0.001f) { _renderer.enabled = true; return; }

            RaycastHit hit;
            bool blocked = Physics.Raycast(from, dir / dist, out hit, dist - 0.015f, ~0, QueryTriggerInteraction.Ignore)
                && hit.transform != transform
                && (boardRoot == null || !hit.transform.IsChildOf(boardRoot));
            _renderer.enabled = !blocked;
        }
    }
}
