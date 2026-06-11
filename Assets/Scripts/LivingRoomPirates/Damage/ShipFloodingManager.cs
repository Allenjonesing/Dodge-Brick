using UnityEngine;

namespace LivingRoomPirates.Damage
{
    public class ShipFloodingManager : MonoBehaviour
    {
        [SerializeField] private float currentWater;
        [SerializeField] private float maxWaterBeforeSink = 100f;
        [SerializeField] private float bucketRemoveAmount = 8f;
        [SerializeField] private Transform visualWaterPlane;
        [SerializeField] private Vector2 waterPlaneYRange = new Vector2(-1f, 0.6f);

        public float Water01 => maxWaterBeforeSink <= 0 ? 0 : currentWater / maxWaterBeforeSink;
        public bool IsSinking => currentWater >= maxWaterBeforeSink;

        public void AddWater(float amount)
        {
            currentWater = Mathf.Clamp(currentWater + amount, 0, maxWaterBeforeSink);
            UpdateVisual();
        }

        public void BucketWaterOut() => AddWater(-bucketRemoveAmount);

        private void UpdateVisual()
        {
            if (visualWaterPlane == null) return;
            var pos = visualWaterPlane.localPosition;
            pos.y = Mathf.Lerp(waterPlaneYRange.x, waterPlaneYRange.y, Water01);
            visualWaterPlane.localPosition = pos;
        }
    }
}
