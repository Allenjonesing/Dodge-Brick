using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Lightweight safety net for hand tools: if a throwable tool falls through
    /// the deck/ocean, return it to its authored spawn pose. This lets the hammer
    /// behave physically without permanently losing the repair mechanic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LrpRespawnIfLost : MonoBehaviour
    {
        public float respawnBelowY = -0.75f;
        public float respawnDelay = 0.4f;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Vector3 _spawnScale;
        private float _belowSince = -1f;

        private void Awake()
        {
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _spawnScale = transform.localScale;
        }

        private void Update()
        {
            if (transform.position.y >= respawnBelowY)
            {
                _belowSince = -1f;
                return;
            }

            if (_belowSince < 0f) _belowSince = Time.time;
            if (Time.time - _belowSince < respawnDelay) return;

            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                Destroy(rb);
            }

            transform.position = _spawnPosition;
            transform.rotation = _spawnRotation;
            transform.localScale = _spawnScale;

            Collider[] cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].isTrigger = true;
            }

            _belowSince = -1f;
        }
    }
}
