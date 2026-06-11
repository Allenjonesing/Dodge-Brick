using System.Collections.Generic;
using UnityEngine;

namespace LivingRoomPirates.Encounters
{
    public class EncounterDirector : MonoBehaviour
    {
        [SerializeField] private Transform shipRoot;
        [SerializeField] private List<EncounterDefinition> encounters = new List<EncounterDefinition>();
        [SerializeField] private float firstEncounterDelay = 20f;
        [SerializeField] private float encounterInterval = 45f;
        [SerializeField] private int maxActiveEncounters = 3;
        private readonly List<GameObject> _active = new List<GameObject>();
        private float _timer;

        private void Start() => _timer = firstEncounterDelay;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0) return;
            _timer = encounterInterval;
            Cleanup();
            if (_active.Count < maxActiveEncounters) SpawnEncounter();
        }

        public void SpawnEncounter()
        {
            var def = Pick();
            if (def == null || def.Prefab == null || shipRoot == null) return;
            float distance = Random.Range(def.MinDistanceFromShip, def.MaxDistanceFromShip);
            Vector2 ring = Random.insideUnitCircle.normalized * distance;
            Vector3 pos = shipRoot.position + new Vector3(ring.x, 0f, ring.y);
            var obj = Instantiate(def.Prefab, pos, Quaternion.identity);
            _active.Add(obj);
        }

        private EncounterDefinition Pick()
        {
            float total = 0;
            foreach (var e in encounters) if (e != null) total += Mathf.Max(0.01f, e.Weight);
            float roll = Random.value * total;
            foreach (var e in encounters)
            {
                if (e == null) continue;
                roll -= Mathf.Max(0.01f, e.Weight);
                if (roll <= 0) return e;
            }
            return null;
        }

        private void Cleanup() => _active.RemoveAll(x => x == null);
    }
}
