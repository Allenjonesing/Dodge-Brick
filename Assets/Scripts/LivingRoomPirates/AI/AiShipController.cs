using UnityEngine;
using LivingRoomPirates.Damage;
using LivingRoomPirates.Voyages;

namespace LivingRoomPirates.AI
{
    public class AiShipController : MonoBehaviour
    {
        [SerializeField] private Transform targetShip;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float turnSpeed = 60f;
        [SerializeField] private float attackRange = 18f;
        [SerializeField] private float fireInterval = 4f;
        [SerializeField] private float cannonDamage = 15f;
        [SerializeField] private ShipDamageManager targetDamageManager;
        [SerializeField] private int health = 100;
        private float _fireTimer;

        private void Update()
        {
            if (targetShip == null) return;
            Vector3 toTarget = targetShip.position - transform.position;
            toTarget.y = 0;
            if (toTarget.sqrMagnitude > 0.1f)
            {
                var desired = Quaternion.LookRotation(toTarget.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
            }
            if (toTarget.magnitude > attackRange)
                transform.position += transform.forward * moveSpeed * Time.deltaTime;
            else
                TryFire();
        }

        public void SetTarget(Transform ship, ShipDamageManager damageManager)
        {
            targetShip = ship;
            targetDamageManager = damageManager;
        }

        private void TryFire()
        {
            _fireTimer -= Time.deltaTime;
            if (_fireTimer > 0) return;
            _fireTimer = fireInterval;
            if (targetDamageManager != null)
                targetDamageManager.ApplyDamageToNearest(targetShip.position, cannonDamage, DamageType.Cannon, gameObject);
        }

        public void ApplyDamage(int amount)
        {
            health -= amount;
            if (health <= 0)
            {
                VoyageManager.Instance?.AddProgress(VoyageObjectiveType.DefeatShips, 1);
                Destroy(gameObject);
            }
        }
    }
}
