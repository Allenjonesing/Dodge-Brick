using UnityEngine;

namespace LivingRoomPirates.Demo
{
    public sealed class BoomPulse : MonoBehaviour
    {
        public float duration = 0.35f;
        public float maxScale = 1f;
        private float _age;
        private Vector3 _startScale;
        private Material _material;

        private void Start()
        {
            _startScale = transform.localScale;
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) _material = renderer.material;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(_age / duration);
            transform.localScale = Vector3.Lerp(_startScale, Vector3.one * maxScale, t);
            if (_material != null)
            {
                Color c = _material.color;
                c.a = 1f - t;
                _material.color = c;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
