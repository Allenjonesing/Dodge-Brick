using System.Collections.Generic;
using UnityEngine;
using LivingRoomPirates.Core;

namespace LivingRoomPirates.Resources
{
    /// <summary>
    /// Shared ship inventory for gold, wood, cannonballs, etc. Put this on the ship root.
    /// </summary>
    public class ShipResourceBank : LrpSingleton<ShipResourceBank>
    {
        [SerializeField] private List<ResourceStack> startingResources = new List<ResourceStack>
        {
            new ResourceStack(ResourceType.Wood, 20),
            new ResourceStack(ResourceType.Cannonballs, 12),
            new ResourceStack(ResourceType.Food, 6)
        };

        private readonly Dictionary<ResourceType, int> _resources = new Dictionary<ResourceType, int>();

        public IReadOnlyDictionary<ResourceType, int> Resources => _resources;

        protected override void Awake()
        {
            base.Awake();
            foreach (var stack in startingResources)
            {
                Add(stack.Type, stack.Amount, false);
            }
        }

        public int Get(ResourceType type)
        {
            return _resources.TryGetValue(type, out var amount) ? amount : 0;
        }

        public bool CanSpend(IEnumerable<ResourceStack> cost)
        {
            foreach (var stack in cost)
            {
                if (Get(stack.Type) < stack.Amount) return false;
            }
            return true;
        }

        public bool Spend(IEnumerable<ResourceStack> cost)
        {
            if (!CanSpend(cost)) return false;
            foreach (var stack in cost) Add(stack.Type, -stack.Amount, true);
            return true;
        }

        public void Add(ResourceType type, int amount, bool raiseEvent = true)
        {
            if (!_resources.ContainsKey(type)) _resources[type] = 0;
            _resources[type] = Mathf.Max(0, _resources[type] + amount);
            if (raiseEvent) LrpEvents.RaiseResourceChanged(type, amount, _resources[type]);
        }

        public void AddStack(ResourceStack stack) => Add(stack.Type, stack.Amount);
    }
}
