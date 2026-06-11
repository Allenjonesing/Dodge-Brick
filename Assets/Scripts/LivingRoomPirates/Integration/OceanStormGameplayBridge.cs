using UnityEngine;
using LivingRoomPirates.Damage;
using LivingRoomPirates.Voyages;

namespace LivingRoomPirates.Integration
{
    /// <summary>
    /// Call SetStormLevel from your existing ocean/storm controller. This converts storm intensity into gameplay pressure.
    /// </summary>
    public class OceanStormGameplayBridge : MonoBehaviour
    {
        [SerializeField] private ShipDamageManager shipDamageManager;
        [SerializeField] private Transform shipRoot;
        [SerializeField] private float stormDamageInterval = 10f;
        [SerializeField] private float maxStormDamage = 12f;
        [Range(0,1)] [SerializeField] private float stormLevel;
        private float _timer;

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        public void SetStormLevel(float value) => stormLevel = Mathf.Clamp01(value);

        public void Configure(ShipDamageManager damageManager, Transform root)
        {
            shipDamageManager = damageManager;
            shipRoot = root;
        }

        private void Update()
        {
            if (stormLevel <= 0.25f || shipDamageManager == null || shipRoot == null) return;
            _timer -= Time.deltaTime;
            if (_timer > 0) return;
            _timer = Mathf.Lerp(stormDamageInterval, stormDamageInterval * 0.35f, stormLevel);
            Vector3 randomPoint = shipRoot.position + Random.insideUnitSphere * 3f;
            shipDamageManager.ApplyDamageToNearest(randomPoint, maxStormDamage * stormLevel, DamageType.Storm, gameObject);
            if (stormLevel > 0.8f) VoyageManager.Instance?.AddProgress(VoyageObjectiveType.SurviveStorm, 1);
        }

        private void AutoAssignReferences()
        {
            if (shipDamageManager == null)
            {
                shipDamageManager = GetComponent<ShipDamageManager>();
                if (shipDamageManager == null)
                {
                    shipDamageManager = FindObjectOfType<ShipDamageManager>();
                }
            }

            if (shipRoot == null)
            {
                shipRoot = shipDamageManager != null ? shipDamageManager.transform : transform;
            }
        }
    }
}
