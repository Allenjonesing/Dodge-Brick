using System.Collections.Generic;
using UnityEngine;

namespace LivingRoomPirates.Damage
{
    public class ShipDamageManager : MonoBehaviour
    {
        [SerializeField] private List<HullSection> hullSections = new List<HullSection>();
        [SerializeField] private bool autoFindSections = true;

        public IReadOnlyList<HullSection> HullSections => hullSections;

        private void Awake()
        {
            if (autoFindSections) hullSections = new List<HullSection>(GetComponentsInChildren<HullSection>());
        }

        public void ApplyDamageToNearest(Vector3 worldPoint, float amount, DamageType type, GameObject source = null)
        {
            var section = FindNearestSection(worldPoint);
            if (section == null) return;
            section.ApplyDamage(new DamageInfo(amount, type, worldPoint, source, true));
        }

        public HullSection FindNearestSection(Vector3 worldPoint)
        {
            HullSection best = null;
            float bestDistance = float.MaxValue;
            foreach (var section in hullSections)
            {
                if (section == null) continue;
                float d = Vector3.SqrMagnitude(section.transform.position - worldPoint);
                if (d < bestDistance) { bestDistance = d; best = section; }
            }
            return best;
        }
    }
}
