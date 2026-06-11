using UnityEngine;

namespace LivingRoomPirates.Encounters
{
    [CreateAssetMenu(menuName = "Living Room Pirates/Encounter")]
    public class EncounterDefinition : ScriptableObject
    {
        public string Id = "floating_loot";
        public EncounterType Type = EncounterType.FloatingLoot;
        public string DisplayName = "Floating Loot";
        public GameObject Prefab;
        public float Weight = 1f;
        public float MinDistanceFromShip = 10f;
        public float MaxDistanceFromShip = 30f;
        public bool AllowDuringStorm = true;
    }
}
