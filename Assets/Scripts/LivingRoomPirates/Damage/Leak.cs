using UnityEngine;

namespace LivingRoomPirates.Damage
{
    public class Leak : MonoBehaviour
    {
        [SerializeField] private float waterPerSecond = 1f;
        [SerializeField] private float repairSeconds = 4f;
        private float _repairProgress;
        private HullSection _section;

        public float WaterPerSecond => waterPerSecond;
        public float Repair01 => repairSeconds <= 0 ? 1 : _repairProgress / repairSeconds;

        public void Initialize(HullSection section) => _section = section;

        private void Update()
        {
            var flooding = GetComponentInParent<ShipFloodingManager>();
            if (flooding != null) flooding.AddWater(waterPerSecond * Time.deltaTime);
        }

        public void AddRepairProgress(float seconds)
        {
            _repairProgress += seconds;
            if (_repairProgress >= repairSeconds) Seal();
        }

        private void Seal()
        {
            if (_section != null) _section.Repair(9999f);
            Destroy(gameObject);
        }
    }
}
