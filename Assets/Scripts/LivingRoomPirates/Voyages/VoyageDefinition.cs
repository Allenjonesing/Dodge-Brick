using System.Collections.Generic;
using UnityEngine;
using LivingRoomPirates.Resources;

namespace LivingRoomPirates.Voyages
{
    [CreateAssetMenu(menuName = "Living Room Pirates/Voyage Definition")]
    public class VoyageDefinition : ScriptableObject
    {
        public string Id = "voyage_treasure_run";
        public string Title = "Treasure Run";
        [TextArea] public string Description = "Grab treasure and get it back to the ship.";
        public List<VoyageObjective> Objectives = new List<VoyageObjective>();
        public List<ResourceStack> Rewards = new List<ResourceStack> { new ResourceStack(ResourceType.Gold, 25) };

        public Voyage CreateRuntimeVoyage()
        {
            var voyage = new Voyage { Id = Id, Title = Title, Description = Description };
            foreach (var objective in Objectives)
            {
                voyage.Objectives.Add(new VoyageObjective
                {
                    Type = objective.Type,
                    Description = objective.Description,
                    RequiredAmount = objective.RequiredAmount
                });
            }
            voyage.Rewards.AddRange(Rewards);
            return voyage;
        }
    }
}
