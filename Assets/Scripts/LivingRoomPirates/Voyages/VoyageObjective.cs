using System;
using UnityEngine;

namespace LivingRoomPirates.Voyages
{
    [Serializable]
    public class VoyageObjective
    {
        public VoyageObjectiveType Type;
        public string Description;
        public int RequiredAmount = 1;
        public int CurrentAmount;
        public bool Completed;

        public void AddProgress(int amount)
        {
            if (Completed) return;
            CurrentAmount = Mathf.Clamp(CurrentAmount + amount, 0, RequiredAmount);
            Completed = CurrentAmount >= RequiredAmount;
        }
    }
}
