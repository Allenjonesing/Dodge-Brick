using UnityEngine;

namespace LivingRoomPirates.Damage
{
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public Vector3 Point;
        public GameObject Source;
        public bool CanCreateLeak;

        public DamageInfo(float amount, DamageType type, Vector3 point, GameObject source = null, bool canCreateLeak = true)
        {
            Amount = amount;
            Type = type;
            Point = point;
            Source = source;
            CanCreateLeak = canCreateLeak;
        }
    }
}
