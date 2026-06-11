using UnityEngine;
using LivingRoomPirates.Core;

namespace LivingRoomPirates.Damage
{
    public class HullSection : MonoBehaviour
    {
        [SerializeField] private string sectionId = "hull_mid";
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float leakThreshold = 35f;
        [SerializeField] private GameObject leakPrefab;
        [SerializeField] private Transform leakSpawnPoint;
        [SerializeField] private bool hasActiveLeak;
        private float _health;

        public string SectionId => sectionId;
        public float Health => _health;
        public float Health01 => maxHealth <= 0 ? 0 : _health / maxHealth;

        private void Awake() => _health = maxHealth;

        public void ApplyDamage(DamageInfo damage)
        {
            if (damage.Amount <= 0) return;
            _health = Mathf.Max(0f, _health - damage.Amount);
            LrpEvents.RaiseHullDamaged(this, damage.Amount);
            if (damage.CanCreateLeak && !hasActiveLeak && _health <= leakThreshold) CreateLeak(damage.Point);
        }

        public void Repair(float amount)
        {
            if (amount <= 0) return;
            _health = Mathf.Min(maxHealth, _health + amount);
            if (_health > leakThreshold) hasActiveLeak = false;
        }

        private void CreateLeak(Vector3 point)
        {
            hasActiveLeak = true;
            if (leakPrefab == null) return;
            var spawn = leakSpawnPoint != null ? leakSpawnPoint.position : point;
            var leakObj = Instantiate(leakPrefab, spawn, Quaternion.identity, transform);
            var leak = leakObj.GetComponent<Leak>();
            if (leak != null) leak.Initialize(this);
            LrpEvents.RaiseLeakCreated(leak);
        }
    }
}
