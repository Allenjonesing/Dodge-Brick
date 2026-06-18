using System;
using System.Collections;
using UnityEngine;
using LivingRoomPirates.Encounters;
using LivingRoomPirates.Loot;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Editor/no-headset debug mode: turn on the playable systems automatically without Photon.
    /// This intentionally does NOT move or rotate the player's ship. The ship remains locked.
    /// It animates/starts stations, ocean entities, loot, encounters, and cannons where possible.
    /// </summary>
    [DisallowMultipleComponent]
    public class LivingRoomPiratesEditorAutoActivator : MonoBehaviour
    {
        public bool runInEditorOnly = true;
        public bool disableNetworking = true;
        public bool enableAllNonNetworkBehaviours = true;
        public bool animateAnchorAndSails = false;
        public bool autoFireCannons = false;
        public float cannonFireInterval = 6f;
        public bool forceOceanEntitySpawner = false;
        public float debugEnemySpawnInterval = 2f;
        public int debugEnemyMaxSpawned = 8;

        private GameObject _debugEnemyPrefab;
        private float _fireTimer;

        private void Start()
        {
            if (runInEditorOnly && !Application.isEditor)
            {
                return;
            }

            if (disableNetworking)
            {
                DisableNetworkingComponents();
            }

            if (enableAllNonNetworkBehaviours)
            {
                EnableNonNetworkBehaviours();
            }

            EnsureOceanSystems();
            EnsureDebugEnemySpawner();
            StartCoroutine(DelayedSystemKick());
        }

        private void Update()
        {
            if (runInEditorOnly && !Application.isEditor)
            {
                return;
            }

            if (animateAnchorAndSails)
            {
                AnimateNamedStationParts();
            }

            if (autoFireCannons)
            {
                _fireTimer += Time.deltaTime;
                if (_fireTimer >= cannonFireInterval)
                {
                    _fireTimer = 0f;
                    FireAllCannons();
                }
            }
        }

        private IEnumerator DelayedSystemKick()
        {
            yield return null;
            yield return null;

            foreach (LootSpawner spawner in FindObjectsOfType<LootSpawner>())
            {
                spawner.gameObject.SetActive(true);
                spawner.enabled = true;
                spawner.SpawnLoot();
            }

            foreach (EncounterDirector director in FindObjectsOfType<EncounterDirector>())
            {
                director.gameObject.SetActive(true);
                director.enabled = true;
                director.SpawnEncounter();
            }

            FireAllCannons();
        }

        private void EnsureOceanSystems()
        {
            OceanWorldController ocean = OceanWorldController.Instance != null
                ? OceanWorldController.Instance
                : FindObjectOfType<OceanWorldController>();

            if (ocean != null)
            {
                ocean.enabled = true;
                ocean.closeWaterOneGapToShip = true;
                ocean.sampleFromWaterOneDeformer = true;
                ocean.waterOneFollowStrength = 0f;
                ocean.waterOneHeightOffset = 0f;
                ocean.useShipFootprintSamplesForWaterOne = false;
                if (Mathf.Approximately(ocean.shipToOceanSnapHeightOffset, 0f))
                {
                    ocean.shipToOceanSnapHeightOffset = -0.05f;
                }

                if (ocean.waterOne != null)
                {
                    OceanWaveMeshDeformer deformer = ocean.waterOne.GetComponent<OceanWaveMeshDeformer>();
                    if (deformer != null)
                    {
                        deformer.enabled = true;
                        deformer.useWorldSpaceWaveCoordinates = true;
                    }

                    OceanWavePresetHotkeys hotkeys = ocean.waterOne.GetComponent<OceanWavePresetHotkeys>();
                    if (hotkeys == null)
                    {
                        hotkeys = ocean.waterOne.gameObject.AddComponent<OceanWavePresetHotkeys>();
                    }
                    hotkeys.enableHotkeys = true;
                    hotkeys.applyDefaultPresetOnStart = false;
                    hotkeys.copyToWaterGridTiles = true;

                    WaterOneGrid3x3 grid = ocean.waterOne.GetComponent<WaterOneGrid3x3>();
                    if (grid == null)
                    {
                        grid = ocean.waterOne.gameObject.AddComponent<WaterOneGrid3x3>();
                    }
                    grid.gridRadius = 1;
                    grid.BuildGrid();
                    hotkeys.SyncGridTilesFromCurrentValues();
                }
            }
        }

        private void EnsureDebugEnemySpawner()
        {
            if (!forceOceanEntitySpawner)
            {
                return;
            }

            OceanEntitySpawner spawner = FindObjectOfType<OceanEntitySpawner>();
            if (spawner == null)
            {
                GameObject host = new GameObject("LRP Debug Ocean Entity Spawner");
                spawner = host.AddComponent<OceanEntitySpawner>();
            }

            spawner.gameObject.SetActive(true);
            spawner.enabled = true;
            spawner.spawnInterval = debugEnemySpawnInterval;
            spawner.maxSpawned = debugEnemyMaxSpawned;
            spawner.minDistance = 8f;
            spawner.maxDistance = 18f;

            if (spawner.prefabs == null || spawner.prefabs.Length == 0)
            {
                spawner.prefabs = new[] { EnsureDebugEnemyPrefab() };
            }
        }

        private GameObject EnsureDebugEnemyPrefab()
        {
            if (_debugEnemyPrefab != null)
            {
                return _debugEnemyPrefab;
            }

            _debugEnemyPrefab = new GameObject("LRP_Debug_EnemyShip_Prefab");
            _debugEnemyPrefab.SetActive(false);

            GameObject hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.name = "EnemyHull";
            hull.transform.SetParent(_debugEnemyPrefab.transform, false);
            hull.transform.localScale = new Vector3(1.2f, 0.25f, 2.0f);
            hull.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "EnemyMast";
            mast.transform.SetParent(_debugEnemyPrefab.transform, false);
            mast.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);
            mast.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            GameObject sail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sail.name = "EnemySail";
            sail.transform.SetParent(_debugEnemyPrefab.transform, false);
            sail.transform.localScale = new Vector3(0.9f, 0.75f, 0.03f);
            sail.transform.localPosition = new Vector3(0f, 0.85f, 0.02f);

            _debugEnemyPrefab.AddComponent<DebugEnemyShipMover>();
            DontDestroyOnLoad(_debugEnemyPrefab);
            return _debugEnemyPrefab;
        }


        private static T[] FindSceneObjectsIncludingInactive<T>() where T : UnityEngine.Object
        {
            T[] all = UnityEngine.Resources.FindObjectsOfTypeAll<T>();
            System.Collections.Generic.List<T> sceneObjects = new System.Collections.Generic.List<T>(all.Length);

            foreach (T obj in all)
            {
                if (obj == null)
                {
                    continue;
                }

                GameObject go = null;
                Component component = obj as Component;
                if (component != null)
                {
                    go = component.gameObject;
                }
                else
                {
                    go = obj as GameObject;
                }

                if (go == null || !go.scene.IsValid())
                {
                    continue;
                }

                if ((obj.hideFlags & HideFlags.HideAndDontSave) != 0)
                {
                    continue;
                }

                sceneObjects.Add(obj);
            }

            return sceneObjects.ToArray();
        }

        private void DisableNetworkingComponents()
        {
            foreach (MonoBehaviour behaviour in FindSceneObjectsIncludingInactive<MonoBehaviour>())
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                string fullName = behaviour.GetType().FullName ?? typeName;
                if (typeName.Contains("Network") || typeName.Contains("Photon") || fullName.Contains("Photon"))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private void EnableNonNetworkBehaviours()
        {
            foreach (MonoBehaviour behaviour in FindSceneObjectsIncludingInactive<MonoBehaviour>())
            {
                if (behaviour == null)
                {
                    continue;
                }

                string typeName = behaviour.GetType().Name;
                string fullName = behaviour.GetType().FullName ?? typeName;
                if (typeName.Contains("Network") || typeName.Contains("Photon") || fullName.Contains("Photon"))
                {
                    continue;
                }

                behaviour.gameObject.SetActive(true);
                behaviour.enabled = true;
            }
        }

        private void AnimateNamedStationParts()
        {
            float t = Time.timeSinceLevelLoad;
            foreach (Transform tr in FindObjectsOfType<Transform>())
            {
                string n = tr.name.ToLowerInvariant();

                if (n.Contains("anchor") && n.Contains("handle"))
                {
                    tr.localRotation = Quaternion.Euler(Mathf.Lerp(-55f, 15f, Mathf.PingPong(t * 0.35f, 1f)), 0f, 0f);
                }
                else if (n.Contains("sail") || n.Contains("rope"))
                {
                    Vector3 p = tr.localPosition;
                    p.y = Mathf.Max(p.y, 0.1f) + Mathf.Sin(t * 0.9f) * 0.01f;
                    tr.localPosition = p;
                }
                else if (n.Contains("steering") || n.Contains("wheel"))
                {
                    tr.Rotate(Vector3.forward, 35f * Time.deltaTime, Space.Self);
                }
            }
        }

        private void FireAllCannons()
        {
            foreach (CannonController cannon in FindObjectsOfType<CannonController>())
            {
                if (cannon == null || !cannon.enabled || !cannon.gameObject.activeInHierarchy)
                {
                    continue;
                }

                cannon.Fire();
            }
        }
    }

    public class DebugEnemyShipMover : MonoBehaviour
    {
        public float circleSpeed = 12f;
        public float bobAmount = 0.25f;

        private Vector3 _spawn;
        private float _phase;

        private void OnEnable()
        {
            _spawn = transform.position;
            _phase = UnityEngine.Random.Range(0f, 100f);
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, circleSpeed * Time.deltaTime, Space.World);
            Vector3 p = transform.position;
            p.y = _spawn.y + Mathf.Sin(Time.timeSinceLevelLoad + _phase) * bobAmount;
            transform.position = p;
        }
    }
}
