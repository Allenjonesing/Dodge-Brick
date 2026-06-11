using UnityEngine;
using LivingRoomPirates.Core;
using LivingRoomPirates.Resources;

namespace LivingRoomPirates.Voyages
{
    public class VoyageManager : LrpSingleton<VoyageManager>
    {
        [SerializeField] private VoyageDefinition startingVoyage;
        [SerializeField] private bool startOnAwake = true;
        public Voyage CurrentVoyage { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (startOnAwake && startingVoyage != null) StartVoyage(startingVoyage);
        }

        public void StartVoyage(VoyageDefinition definition)
        {
            CurrentVoyage = definition.CreateRuntimeVoyage();
            LrpEvents.RaiseVoyageStarted(CurrentVoyage);
        }

        public void AddProgress(VoyageObjectiveType type, int amount = 1)
        {
            if (CurrentVoyage == null || CurrentVoyage.Completed) return;
            foreach (var objective in CurrentVoyage.Objectives)
            {
                if (objective.Type != type || objective.Completed) continue;
                objective.AddProgress(amount);
                if (objective.Completed) LrpEvents.RaiseObjectiveCompleted(objective);
            }
            if (CurrentVoyage.AreAllObjectivesComplete()) CompleteVoyage();
        }

        private void CompleteVoyage()
        {
            CurrentVoyage.Completed = true;
            var bank = ShipResourceBank.Instance;
            if (bank != null)
            {
                foreach (var reward in CurrentVoyage.Rewards) bank.AddStack(reward);
            }
            LrpEvents.RaiseVoyageCompleted(CurrentVoyage);
        }
    }
}
