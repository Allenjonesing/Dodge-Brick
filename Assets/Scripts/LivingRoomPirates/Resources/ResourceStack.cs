using System;

namespace LivingRoomPirates.Resources
{
    [Serializable]
    public struct ResourceStack
    {
        public ResourceType Type;
        public int Amount;

        public ResourceStack(ResourceType type, int amount)
        {
            Type = type;
            Amount = amount;
        }
    }
}
