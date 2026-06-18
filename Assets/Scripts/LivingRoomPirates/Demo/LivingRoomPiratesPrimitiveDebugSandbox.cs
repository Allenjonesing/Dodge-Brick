using System.Collections.Generic;
using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Self-contained primitive gameplay sandbox for Editor testing.
    /// No prefabs, no XR, no Photon. The ship root is forcibly locked; only
    /// debug props, cannonballs, enemies, sail/anchor visuals, and water move.
    /// </summary>
    public sealed class LivingRoomPiratesPrimitiveDebugSandbox : MonoBehaviour
    {
        [Header("References")]
        public Transform shipRoot;
        public OceanWorldController ocean;

        [Header("Debug Startup")]
        public bool buildOnStart = true;
        public bool lockShipTransform = true;
        public bool autoFireCannons = true;
        public bool autoCycleSailAndAnchor = true;
        public bool autoSpawnEnemies = true;
        public bool showOverlay = true;

        [Header("Timings")]
        public float cannonFireInterval = 3f;
        public float enemySpawnInterval = 4f;
        public int maxEnemies = 8;
        public float enemyMoveSpeed = 0.7f;

        private readonly List<PrimitiveCannon> _cannons = new List<PrimitiveCannon>();
        private readonly List<Transform> _enemies = new List<Transform>();
        private Transform _debugRoot;
        private Transform _sail;
        private Transform _anchor;
        private Transform _wheel;
        private Vector3 _shipStartPosition;
        private Quaternion _shipStartRotation;
        private Vector3 _shipStartScale;
        private float _fireTimer;
        private float _enemyTimer;
        private bool _sailDropped;
        private bool _anchorLowered;

        private void Start()
        {
            ResolveReferences();
            CaptureShipLock();
            if (buildOnStart)
            {
                BuildOrRepairSandbox();
            }
            Debug.Log("[LRP DebugSandbox] Active. SPACE fires all cannons, T toggles sail, R toggles anchor, Y spawns enemy, L toggles overlay. Ship transform is locked.");
        }

        private void Update()
        {
            ResolveReferences();

            if (Input.GetKeyDown(KeyCode.L)) showOverlay = !showOverlay;
            if (Input.GetKeyDown(KeyCode.Space)) FireAllCannons();
            if (Input.GetKeyDown(KeyCode.T)) _sailDropped = !_sailDropped;
            if (Input.GetKeyDown(KeyCode.R)) _anchorLowered = !_anchorLowered;
            if (Input.GetKeyDown(KeyCode.Y)) SpawnEnemy();

            if (autoFireCannons)
            {
                _fireTimer += Time.deltaTime;
                if (_fireTimer >= cannonFireInterval)
                {
                    _fireTimer = 0f;
                    FireAllCannons();
                }
            }

            if (autoCycleSailAndAnchor)
            {
                _sailDropped = Mathf.Sin(Time.time * 0.45f) > 0f;
                _anchorLowered = Mathf.Sin(Time.time * 0.35f) > 0f;
            }

            if (autoSpawnEnemies)
            {
                _enemyTimer += Time.deltaTime;
                if (_enemyTimer >= enemySpawnInterval)
                {
                    _enemyTimer = 0f;
                    SpawnEnemy();
                }
            }

            AnimateStationVisuals();
            UpdateEnemies();
        }

        private void LateUpdate()
        {
            if (lockShipTransform && shipRoot != null)
            {
                shipRoot.position = _shipStartPosition;
                shipRoot.rotation = _shipStartRotation;
                shipRoot.localScale = _shipStartScale;
            }
        }

        [ContextMenu("Build / Repair Primitive Debug Sandbox")]
        public void BuildOrRepairSandbox()
        {
            ResolveReferences();
            if (shipRoot == null)
            {
                Debug.LogWarning("[LRP DebugSandbox] Cannot build: shipRoot is missing.");
                return;
            }

            Transform existing = shipRoot.Find("LRP_PrimitiveDebugSystems");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            _debugRoot = new GameObject("LRP_PrimitiveDebugSystems").transform;
            _debugRoot.SetParent(shipRoot, false);
            _debugRoot.localPosition = Vector3.zero;
            _debugRoot.localRotation = Quaternion.identity;
            _debugRoot.localScale = Vector3.one;

            _cannons.Clear();
            _enemies.Clear();

            BuildWheel();
            BuildSail();
            BuildAnchor();
            BuildCannons();

            Debug.Log($"[LRP DebugSandbox] Built primitive stations: {_cannons.Count} cannons, animated sail, animated anchor, decorative wheel, enemy spawner.");
        }

        private void ResolveReferences()
        {
            if (shipRoot == null)
            {
                GameObject ship = GameObject.Find("shipGeneratedRoot");
                if (ship != null) shipRoot = ship.transform;
            }

            if (ocean == null)
            {
                ocean = FindObjectOfType<OceanWorldController>();
            }
        }

        private void CaptureShipLock()
        {
            if (shipRoot == null) return;
            _shipStartPosition = shipRoot.position;
            _shipStartRotation = shipRoot.rotation;
            _shipStartScale = shipRoot.localScale;
        }

        private void BuildWheel()
        {
            GameObject stand = Cube("DebugWheelStand", new Vector3(0f, 0.55f, -0.82f), new Vector3(0.12f, 0.65f, 0.12f), new Color(0.35f, 0.18f, 0.06f));
            GameObject wheel = Cylinder("DebugSteeringWheel", new Vector3(0f, 0.92f, -0.72f), new Vector3(0.35f, 0.06f, 0.35f), new Color(0.45f, 0.23f, 0.08f));
            wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _wheel = wheel.transform;
        }

        private void BuildSail()
        {
            Cube("DebugMast", new Vector3(0f, 1.15f, 0.12f), new Vector3(0.12f, 2.1f, 0.12f), new Color(0.36f, 0.18f, 0.07f));
            _sail = Cube("DebugAnimatedSail", new Vector3(0f, 1.33f, 0.15f), new Vector3(1.05f, 1.05f, 0.035f), new Color(0.92f, 0.86f, 0.64f)).transform;
        }

        private void BuildAnchor()
        {
            GameObject chain = Cube("DebugAnchorChain", new Vector3(0f, 0.48f, 1.08f), new Vector3(0.035f, 0.55f, 0.035f), Color.gray);
            _anchor = Cube("DebugAnimatedAnchor", new Vector3(0f, 0.16f, 1.08f), new Vector3(0.24f, 0.24f, 0.08f), new Color(0.08f, 0.08f, 0.08f)).transform;
            chain.transform.SetParent(_anchor, true);
        }

        private void BuildCannons()
        {
            AddCannon("BowCannon", new Vector3(0f, 0.42f, 1.0f), Quaternion.identity);
            AddCannon("PortCannon", new Vector3(-0.95f, 0.42f, 0f), Quaternion.Euler(0f, -90f, 0f));
            AddCannon("StarboardCannon", new Vector3(0.95f, 0.42f, 0f), Quaternion.Euler(0f, 90f, 0f));
            AddCannon("SternCannon", new Vector3(0f, 0.42f, -1.0f), Quaternion.Euler(0f, 180f, 0f));
        }

        private void AddCannon(string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(_debugRoot, false);
            pivot.transform.localPosition = localPosition;
            pivot.transform.localRotation = localRotation;

            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = name + "_Barrel";
            barrel.transform.SetParent(pivot.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.05f, 0.18f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.13f, 0.36f, 0.13f);
            SetColor(barrel, Color.black);

            GameObject baseBlock = Cube(name + "_Base", Vector3.zero, new Vector3(0.36f, 0.18f, 0.34f), new Color(0.25f, 0.12f, 0.04f));
            baseBlock.transform.SetParent(pivot.transform, false);

            Transform firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(pivot.transform, false);
            firePoint.localPosition = new Vector3(0f, 0.05f, 0.68f);
            firePoint.localRotation = Quaternion.identity;

            _cannons.Add(new PrimitiveCannon { root = pivot.transform, firePoint = firePoint });
        }

        private void FireAllCannons()
        {
            if (_cannons.Count == 0) BuildOrRepairSandbox();
            for (int i = 0; i < _cannons.Count; i++)
            {
                FireCannon(_cannons[i]);
            }
            Debug.Log($"[LRP DebugSandbox] Fired {_cannons.Count} primitive cannons.");
        }

        private void FireCannon(PrimitiveCannon cannon)
        {
            if (cannon == null || cannon.firePoint == null) return;

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "DebugCannonball";
            ball.transform.position = cannon.firePoint.position;
            ball.transform.rotation = cannon.firePoint.rotation;
            ball.transform.localScale = Vector3.one * 0.16f;
            SetColor(ball, Color.black);
            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.AddForce(cannon.firePoint.forward * 9.5f, ForceMode.Impulse);
            Destroy(ball, 5f);
        }

        private void AnimateStationVisuals()
        {
            if (_wheel != null)
            {
                _wheel.localRotation = Quaternion.Euler(90f, 0f, Mathf.Sin(Time.time * 1.6f) * 32f);
            }

            if (_sail != null)
            {
                float targetScaleY = _sailDropped ? 1.05f : 0.12f;
                Vector3 s = _sail.localScale;
                s.y = Mathf.Lerp(s.y, targetScaleY, Time.deltaTime * 4f);
                _sail.localScale = s;
                Vector3 p = _sail.localPosition;
                p.y = _sailDropped ? 1.33f : 1.78f;
                _sail.localPosition = Vector3.Lerp(_sail.localPosition, p, Time.deltaTime * 4f);
            }

            if (_anchor != null)
            {
                Vector3 target = new Vector3(0f, _anchorLowered ? -0.72f : 0.16f, 1.08f);
                _anchor.localPosition = Vector3.Lerp(_anchor.localPosition, target, Time.deltaTime * 3f);
            }
        }

        private void SpawnEnemy()
        {
            CleanupEnemies();
            if (_enemies.Count >= maxEnemies || shipRoot == null) return;

            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(8f, 15f);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
            Vector3 world = shipRoot.position + offset;
            world.y = SampleWaterY(world) + 0.18f;

            GameObject enemy = new GameObject("DebugEnemyRaft");
            enemy.transform.position = world;
            enemy.transform.rotation = Quaternion.LookRotation((shipRoot.position - world).normalized, Vector3.up);

            GameObject hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.name = "EnemyHull";
            hull.transform.SetParent(enemy.transform, false);
            hull.transform.localScale = new Vector3(0.9f, 0.18f, 0.55f);
            SetColor(hull, new Color(0.45f, 0.08f, 0.06f));

            GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mast.name = "EnemyMast";
            mast.transform.SetParent(enemy.transform, false);
            mast.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            mast.transform.localScale = new Vector3(0.06f, 0.9f, 0.06f);
            SetColor(mast, new Color(0.25f, 0.12f, 0.04f));

            GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "EnemyFlag";
            flag.transform.SetParent(enemy.transform, false);
            flag.transform.localPosition = new Vector3(0.22f, 0.82f, 0f);
            flag.transform.localScale = new Vector3(0.36f, 0.18f, 0.035f);
            SetColor(flag, Color.red);

            _enemies.Add(enemy.transform);
            Debug.Log($"[LRP DebugSandbox] Spawned primitive enemy {_enemies.Count}/{maxEnemies}.");
        }

        private void UpdateEnemies()
        {
            if (shipRoot == null) return;
            CleanupEnemies();
            for (int i = 0; i < _enemies.Count; i++)
            {
                Transform enemy = _enemies[i];
                Vector3 toShip = shipRoot.position - enemy.position;
                toShip.y = 0f;
                if (toShip.sqrMagnitude > 1.7f * 1.7f)
                {
                    enemy.position += toShip.normalized * enemyMoveSpeed * Time.deltaTime;
                }
                enemy.position = new Vector3(enemy.position.x, SampleWaterY(enemy.position) + 0.18f, enemy.position.z);
                if (toShip.sqrMagnitude > 0.01f)
                {
                    enemy.rotation = Quaternion.Slerp(enemy.rotation, Quaternion.LookRotation(toShip.normalized, Vector3.up), Time.deltaTime * 2f);
                }
            }
        }

        private float SampleWaterY(Vector3 world)
        {
            return ocean != null ? ocean.SampleHeight(world) : 0f;
        }

        private void CleanupEnemies()
        {
            _enemies.RemoveAll(t => t == null);
        }

        private GameObject Cube(string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(_debugRoot, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            SetColor(go, color);
            return go;
        }

        private GameObject Cylinder(string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(_debugRoot, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            SetColor(go, color);
            return go;
        }

        private static void SetColor(GameObject go, Color color)
        {
            LrpPrimitiveMaterialLibrary.Apply(go, color);
        }

        private void OnGUI()
        {
            return; // Debug overlay removed; use physical ship signs only.
            if (!showOverlay) return;
            GUI.Box(new Rect(12f, 250f, 410f, 190f),
                "LRP Primitive Debug Sandbox\n\n" +
                "SPACE: fire all primitive cannons\n" +
                "T: toggle sail drop/raise\n" +
                "R: toggle anchor lower/raise\n" +
                "Y: spawn enemy raft\n" +
                "L: toggle this overlay\n" +
                $"Cannons: {_cannons.Count}\n" +
                $"Enemies: {_enemies.Count}/{maxEnemies}\n" +
                $"Sail: {(_sailDropped ? "Dropped" : "Raised")}  Anchor: {(_anchorLowered ? "Lowered" : "Raised")}\n" +
                "Ship transform: LOCKED");
        }

        private sealed class PrimitiveCannon
        {
            public Transform root;
            public Transform firePoint;
        }
    }
}
