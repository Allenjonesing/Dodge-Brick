using System;
using System.Collections.Generic;
using LivingRoomPirates.Resources;

namespace LivingRoomPirates.Voyages
{
    [Serializable]
    public class Voyage
    {
        public string Id;
        public string Title;
        public string Description;
        public List<VoyageObjective> Objectives = new List<VoyageObjective>();
        public List<ResourceStack> Rewards = new List<ResourceStack>();
        public bool Completed;

        public bool AreAllObjectivesComplete()
        {
            foreach (var objective in Objectives)
                if (!objective.Completed) return false;
            return Objectives.Count > 0;
        }
    }
}
