using UnityEngine;

namespace LivingRoomPirates.Demo
{
    [DefaultExecutionOrder(250)]
    public sealed class CannonballWaterImpact : MonoBehaviour
    {
        public OceanWorldController ocean;
        public float lifetime = 6f;
        public float waterImpactOffset = 0.03f;
        private float _deathTime;

        private void Start()
        {
            _deathTime = Time.time + lifetime;
            if (ocean == null) ocean = OceanWorldController.Instance != null ? OceanWorldController.Instance : FindObjectOfType<OceanWorldController>();
        }

        private void Update()
        {
            if (Time.time >= _deathTime)
            {
                Destroy(gameObject);
                return;
            }

            if (ocean == null)
            {
                ocean = OceanWorldController.Instance != null ? OceanWorldController.Instance : FindObjectOfType<OceanWorldController>();
                return;
            }

            float waterY = ocean.SampleHeight(transform.position);
            if (transform.position.y <= waterY + waterImpactOffset)
            {
                SpawnSplash(new Vector3(transform.position.x, waterY + 0.02f, transform.position.z));
                Destroy(gameObject);
            }
        }

        private void SpawnSplash(Vector3 position)
        {
            GameObject splash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            splash.name = "CannonballWaterSplash";
            splash.transform.position = position;
            splash.transform.localScale = Vector3.one * 0.12f;
            Renderer r = splash.GetComponent<Renderer>();
            if (r != null)
            {
                Material m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.75f, 0.9f, 1f, 0.85f);
                r.material = m;
            }
            BoomPulse pulse = splash.AddComponent<BoomPulse>();
            pulse.duration = 0.45f;
            pulse.maxScale = 0.9f;
        }
    }
}
