using System.Collections.Generic;
using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// One-stop primitive debug layer for the no-prefab Living Room Pirates scene.
    /// It does NOT move/rotate the ship. It demonstrates things riding the deforming Water1 plane,
    /// visible cannon fire/boom effects, anchor/sail hotkeys, and water-tile conveyor travel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivingRoomPiratesSurfaceDebugSandbox : MonoBehaviour
    {
        [Header("References")]
        public Transform shipRoot;
        public Transform waterOne;
        public OceanWorldController ocean;

        [Header("Build")]
        public bool buildOnStart = true;
        public bool clearOldPrimitiveDebugSystems = true;
        public bool lockShipTransform = true;

        [Header("Water Escalator Travel")]
        public bool waterTravelEnabled = false;
        public float waterTravelSpeed = 0.8f;
        public Vector2 waterTravelDirection = new Vector2(0f, 1f);
        public float debrisRecyclerHalfRange = 35f;
        public int extraDebrisCount = 45;
        [Tooltip("When true, WASD/arrow keys steer the Water1 tile conveyor instead of moving the ship.")]
        public bool useKeyboardForWaterTravel = true;

        [Header("Cannons")]
        public bool autoFireCannons = false;
        public float autoFireInterval = 4f;
        public float cannonballSpeed = 12f;
        public float cannonballLifetime = 6f;

        [Header("Debug UI")]
        public bool showOverlay = true;

        private readonly List<PrimitiveCannon> _cannons = new List<PrimitiveCannon>();
        private readonly List<Transform> _floaters = new List<Transform>();
        private Transform _debugRoot;
        private Transform _sail;
        private Transform _anchor;
        private Transform _wheel;
        private Vector3 _shipStartPosition;
        private Quaternion _shipStartRotation;
        private Vector3 _shipStartScale;
        private bool _sailDropped = true;
        private bool _anchorLowered;
        private float _autoFireTimer;

        private void Start()
        {
            ResolveReferences();
            CaptureShipLock();

            if (buildOnStart)
            {
                BuildOrRepair();
            }

            Debug.Log("[LRP SurfaceDebug] Active. SPACE fire, C auto-fire, T sail, R anchor, M water travel, L overlay, 1-6 wave presets.");
        }

        private void Update()
        {
            ResolveReferences();

            if (Input.GetKeyDown(KeyCode.L)) showOverlay = !showOverlay;
            if (Input.GetKeyDown(KeyCode.Space)) FireAllCannons();
            if (Input.GetKeyDown(KeyCode.C)) autoFireCannons = !autoFireCannons;
            if (Input.GetKeyDown(KeyCode.T)) ToggleSail();
            if (Input.GetKeyDown(KeyCode.R)) ToggleAnchor();
            if (Input.GetKeyDown(KeyCode.M)) waterTravelEnabled = !waterTravelEnabled;

            DisableLegacyShipMovementControls();
            HandleWaterKeyboardInput();

            if (autoFireCannons)
            {
                _autoFireTimer += Time.deltaTime;
                if (_autoFireTimer >= autoFireInterval)
                {
                    _autoFireTimer = 0f;
                    FireAllCannons();
                }
            }

            ApplyWaterEscalatorSettings();
            MoveFloatersWithWaterTravel();
            AnimateStations();
            FloatLooseProps();
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

        [ContextMenu("Build / Repair Surface Debug Sandbox")]
        public void BuildOrRepair()
        {
            ResolveReferences();

            if (shipRoot == null)
            {
                Debug.LogWarning("[LRP SurfaceDebug] Cannot build because shipRoot is missing.");
                return;
            }

            if (clearOldPrimitiveDebugSystems)
            {
                ClearChild(shipRoot, "LRP_PrimitiveDebugSystems");
                ClearChild(shipRoot, "LRP_SurfaceDebugSystems");
            }

            _debugRoot = new GameObject("LRP_SurfaceDebugSystems").transform;
            _debugRoot.SetParent(shipRoot, false);
            _debugRoot.localPosition = Vector3.zero;
            _debugRoot.localRotation = Quaternion.identity;
            _debugRoot.localScale = Vector3.one;

            _cannons.Clear();
            _floaters.Clear();

            RemoveNonFunctionalGeneratedProps();
            BuildWheel();
            BuildSail();
            BuildAnchor();
            BuildCannonsAndAmmo();
            BuildFloatingSurfaceProps();
            DisableOldEnemyRaftsButKeepWaterTwo();
            ApplyWaterEscalatorSettings();

            Debug.Log("[LRP SurfaceDebug] Built visible cannons, cannonball piles, sail, anchor, and floating surface props. Enemy rafts disabled; Water2 kept as background.");
        }

        public void ToggleSail()
        {
            _sailDropped = !_sailDropped;
        }

        public void ToggleAnchor()
        {
            _anchorLowered = !_anchorLowered;
        }

        public void FireAllCannonsPublic()
        {
            FireAllCannons();
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

            if (waterOne == null)
            {
                GameObject water = GameObject.Find("Water1");
                if (water != null) waterOne = water.transform;
            }
        }

        private void CaptureShipLock()
        {
            if (shipRoot == null) return;
            _shipStartPosition = shipRoot.position;
            _shipStartRotation = shipRoot.rotation;
            _shipStartScale = shipRoot.localScale;
        }

        private void DisableLegacyShipMovementControls()
        {
            foreach (LivingRoomPiratesShipKeyboardDebugControls controls in FindObjectsOfType<LivingRoomPiratesShipKeyboardDebugControls>())
            {
                if (controls != null && controls.enabled)
                {
                    controls.showOverlay = false;
                    controls.enabled = false;
                }
            }

            foreach (LivingRoomPiratesPrimitiveDebugSandbox sandbox in FindObjectsOfType<LivingRoomPiratesPrimitiveDebugSandbox>())
            {
                if (sandbox != null && sandbox.enabled)
                {
                    sandbox.enabled = false;
                }
            }
        }

        private void HandleWaterKeyboardInput()
        {
            if (!useKeyboardForWaterTravel)
            {
                return;
            }

            Vector2 input = Vector2.zero;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            if (input.sqrMagnitude > 0.0001f && !_anchorLowered)
            {
                waterTravelDirection = input;
                waterTravelEnabled = true;
            }
            else
            {
                // No automatic drifting: water only travels while the debug controls are held.
                // Anchor down always wins, even if the sail is dropped.
                waterTravelEnabled = false;
            }
        }

        private float EffectiveTravelSpeed()
        {
            if (_anchorLowered) return 0f;
            return waterTravelSpeed * (_sailDropped ? 3f : 1f);
        }

        private void ApplyWaterEscalatorSettings()
        {
            float effectiveSpeed = EffectiveTravelSpeed();
            bool effectiveEnabled = waterTravelEnabled && effectiveSpeed > 0.001f;

            if (ocean != null)
            {
                // The ocean root stays locked; WaterOneGrid3x3 moves/recycles the individual generated tiles.
                ocean.enableWaterEscalatorTravel = effectiveEnabled;
                ocean.waterEscalatorSpeed = effectiveSpeed;
                ocean.waterEscalatorDirection = waterTravelDirection;
            }

            if (waterOne != null)
            {
                WaterOneGrid3x3 grid = waterOne.GetComponent<WaterOneGrid3x3>();
                if (grid != null)
                {
                    grid.recycleTilesAroundAnchor = true;
                    grid.recycleAnchor = shipRoot;
                    grid.waterTravelEnabled = effectiveEnabled;
                    grid.waterTravelSpeed = effectiveSpeed;
                    grid.waterTravelDirection = waterTravelDirection;
                }
            }
        }

        private void RemoveNonFunctionalGeneratedProps()
        {
            if (shipRoot == null) return;
            string[] removeTokens = new string[]
            {
                "spyglass", "treasure", "chest", "repair", "bucket",
                "oar", "doohicky", "debugoscill", "redwhite", "red_and_white"
            };

            for (int i = shipRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = shipRoot.GetChild(i);
                if (child == null || child == _debugRoot) continue;
                string n = child.name.ToLowerInvariant();
                bool remove = false;
                for (int t = 0; t < removeTokens.Length; t++)
                {
                    if (n.Contains(removeTokens[t]))
                    {
                        remove = true;
                        break;
                    }
                }

                if (remove)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void BuildWheel()
        {
            Cube("DebugWheelStand", new Vector3(0f, 0.55f, -0.82f), new Vector3(0.12f, 0.65f, 0.12f), new Color(0.35f, 0.18f, 0.06f));
            _wheel = Cylinder("DebugSteeringWheel", new Vector3(0f, 0.92f, -0.72f), new Vector3(0.35f, 0.06f, 0.35f), new Color(0.45f, 0.23f, 0.08f)).transform;
            _wheel.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void BuildSail()
        {
            Cube("DebugMast", new Vector3(0f, 1.15f, 0.12f), new Vector3(0.12f, 2.1f, 0.12f), new Color(0.36f, 0.18f, 0.07f));
            _sail = Cube("DebugAnimatedSail_BIG_OBVIOUS_SPEED_X3", new Vector3(0f, 1.33f, 0.15f), new Vector3(1.15f, 1.05f, 0.035f), new Color(0.92f, 0.86f, 0.64f)).transform;

            GameObject rope = Cylinder("SailControlRope_GRAB_OR_T", new Vector3(-0.78f, 0.92f, 0.22f), new Vector3(0.035f, 0.62f, 0.035f), new Color(0.80f, 0.68f, 0.42f));
            rope.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            AddStationButton(rope, LrpDebugStationButton.Action.ToggleSail);
            Cube("SIGN_SAIL_T_OR_GRAB_SPEED_X3", new Vector3(-0.78f, 0.46f, 0.22f), new Vector3(0.62f, 0.18f, 0.035f), new Color(0.08f, 0.10f, 0.12f));
        }

        private void BuildAnchor()
        {
            _anchor = new GameObject("DebugAnchorAssembly_ANCHOR_DOWN_STOPS_MOVEMENT").transform;
            _anchor.SetParent(_debugRoot, false);
            _anchor.localPosition = new Vector3(0f, 0.16f, 1.08f);
            _anchor.localRotation = Quaternion.identity;

            GameObject anchorWheel = Cylinder("AnchorWheel_GRAB_OR_R_STOP", new Vector3(0.72f, 0.62f, 0.98f), new Vector3(0.28f, 0.05f, 0.28f), new Color(0.18f, 0.18f, 0.20f));
            anchorWheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            AddStationButton(anchorWheel, LrpDebugStationButton.Action.ToggleAnchor);
            Cube("SIGN_ANCHOR_R_OR_GRAB_STOPS", new Vector3(0.72f, 0.28f, 0.98f), new Vector3(0.75f, 0.18f, 0.035f), new Color(0.08f, 0.10f, 0.12f));

            CubeUnder(_anchor, "DebugAnchorChain", new Vector3(0f, 0.25f, 0f), new Vector3(0.035f, 0.55f, 0.035f), Color.gray);
            CubeUnder(_anchor, "DebugAnimatedAnchor", new Vector3(0f, -0.08f, 0f), new Vector3(0.24f, 0.24f, 0.08f), new Color(0.08f, 0.08f, 0.08f));
        }

        private void BuildCannonsAndAmmo()
        {
            AddCannon("BowCannon", new Vector3(0f, 0.42f, 1.0f), Quaternion.identity);
            AddCannon("PortCannon", new Vector3(-0.95f, 0.42f, 0f), Quaternion.Euler(0f, -90f, 0f));
            AddCannon("StarboardCannon", new Vector3(0.95f, 0.42f, 0f), Quaternion.Euler(0f, 90f, 0f));
            AddCannon("SternCannon", new Vector3(0f, 0.42f, -1.0f), Quaternion.Euler(0f, 180f, 0f));

            BuildCannonballPile("AmmoPile_Port", new Vector3(-0.55f, 0.18f, -0.42f));
            BuildCannonballPile("AmmoPile_Starboard", new Vector3(0.55f, 0.18f, -0.42f));
        }

        private void AddCannon(string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(_debugRoot, false);
            pivot.transform.localPosition = localPosition;
            pivot.transform.localRotation = localRotation;

            GameObject baseBlock = CubeUnder(pivot.transform, name + "_Base", Vector3.zero, new Vector3(0.42f, 0.2f, 0.36f), new Color(0.25f, 0.12f, 0.04f));
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = name + "_Barrel";
            barrel.transform.SetParent(pivot.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.09f, 0.20f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.16f, 0.42f, 0.16f);
            SetColor(barrel, Color.black);

            Transform firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(pivot.transform, false);
            firePoint.localPosition = new Vector3(0f, 0.09f, 0.78f);
            firePoint.localRotation = Quaternion.identity;

            _cannons.Add(new PrimitiveCannon { root = pivot.transform, firePoint = firePoint });
        }

        private void BuildCannonballPile(string name, Vector3 localPosition)
        {
            Transform pile = new GameObject(name).transform;
            pile.SetParent(_debugRoot, false);
            pile.localPosition = localPosition;
            pile.localRotation = Quaternion.identity;

            Vector3[] offsets =
            {
                new Vector3(-0.12f,0f,-0.06f), new Vector3(0.0f,0f,-0.06f), new Vector3(0.12f,0f,-0.06f),
                new Vector3(-0.06f,0f,0.06f), new Vector3(0.06f,0f,0.06f),
                new Vector3(0f,0.10f,0f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "LoadableCannonball_" + i;
                ball.transform.SetParent(pile, false);
                ball.transform.localPosition = offsets[i];
                ball.transform.localScale = Vector3.one * 0.13f;
                SetColor(ball, Color.black);
            }
        }

        private void BuildFloatingSurfaceProps()
        {
            AddFloater("FloatingBarrel_A", PrimitiveType.Cylinder, new Vector3(-3.0f, 0f, 3.2f), new Vector3(0.35f, 0.55f, 0.35f), new Color(0.44f, 0.22f, 0.08f));
            AddFloater("FloatingCrate_A", PrimitiveType.Cube, new Vector3(2.8f, 0f, 2.5f), new Vector3(0.55f, 0.32f, 0.55f), new Color(0.50f, 0.28f, 0.10f));
            AddFloater("FloatingDebris_A", PrimitiveType.Cube, new Vector3(1.6f, 0f, -3.4f), new Vector3(1.0f, 0.08f, 0.22f), new Color(0.35f, 0.18f, 0.06f));
            AddFloater("FloatingDebris_B", PrimitiveType.Cube, new Vector3(-4.6f, 0f, -2.2f), new Vector3(0.9f, 0.08f, 0.18f), new Color(0.35f, 0.18f, 0.06f));
            AddFloater("FloatingCrate_B", PrimitiveType.Cube, new Vector3(4.5f, 0f, -3.0f), new Vector3(0.48f, 0.28f, 0.48f), new Color(0.50f, 0.28f, 0.10f));

            Random.InitState(1817);
            for (int i = 0; i < extraDebrisCount; i++)
            {
                float x = Random.Range(-debrisRecyclerHalfRange * 0.85f, debrisRecyclerHalfRange * 0.85f);
                float z = Random.Range(-debrisRecyclerHalfRange * 0.85f, debrisRecyclerHalfRange * 0.85f);
                PrimitiveType type = (i % 3 == 0) ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                Vector3 scale = type == PrimitiveType.Cylinder
                    ? new Vector3(Random.Range(0.20f, 0.42f), Random.Range(0.30f, 0.75f), Random.Range(0.20f, 0.42f))
                    : new Vector3(Random.Range(0.25f, 1.15f), Random.Range(0.06f, 0.35f), Random.Range(0.18f, 0.85f));
                Color color = (i % 4 == 0) ? new Color(0.50f, 0.28f, 0.10f) : new Color(0.35f, 0.18f, 0.06f);
                AddFloater("FloatingDebris_Extra_" + i, type, new Vector3(x, 0f, z), scale, color);
            }
        }

        private void AddFloater(string name, PrimitiveType type, Vector3 localXZFromShip, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = shipRoot.TransformPoint(localXZFromShip);
            go.transform.localScale = scale;
            SetColor(go, color);
            SurfaceFloatFollower follower = go.AddComponent<SurfaceFloatFollower>();
            follower.ocean = ocean;
            follower.contactMode = SurfaceFloatFollower.ContactMode.RendererBottom;
            follower.heightOffset = -0.03f;
            follower.snapInstantly = true;
            follower.alignToNormal = true;
            follower.moveWithWaterConveyor = false; // sandbox moves X/Z once, follower only solves Y/normal
            _floaters.Add(go.transform);
        }

        private void FireAllCannons()
        {
            if (_cannons.Count == 0)
            {
                BuildOrRepair();
            }

            for (int i = 0; i < _cannons.Count; i++)
            {
                FireCannon(_cannons[i]);
            }
        }

        private void FireCannon(PrimitiveCannon cannon)
        {
            if (cannon == null || cannon.firePoint == null) return;

            SpawnBoom(cannon.firePoint.position, cannon.firePoint.forward);

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "VisibleDebugCannonball";
            ball.transform.position = cannon.firePoint.position;
            ball.transform.rotation = cannon.firePoint.rotation;
            ball.transform.localScale = Vector3.one * 0.18f;
            SetColor(ball, Color.black);

            Collider ballCollider = ball.GetComponent<Collider>();
            if (ballCollider != null) ballCollider.enabled = false; // manual water-impact only; do not hit hidden planes/colliders

            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.55f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 shotDirection = cannon.root != null ? cannon.root.TransformDirection(Vector3.forward) : cannon.firePoint.forward;
            shotDirection.y = 0.08f;
            rb.AddForce(shotDirection.normalized * cannonballSpeed, ForceMode.Impulse);

            TrailRenderer trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.45f;
            trail.startWidth = 0.10f;
            trail.endWidth = 0.01f;
            Material trailMat = new Material(Shader.Find("Sprites/Default"));
            trailMat.color = new Color(0.18f, 0.18f, 0.18f, 0.8f);
            trail.material = trailMat;

            CannonballWaterImpact impact = ball.AddComponent<CannonballWaterImpact>();
            impact.ocean = ocean;
            impact.lifetime = cannonballLifetime;
        }

        private void SpawnBoom(Vector3 position, Vector3 forward)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "CannonBoomFlash";
            flash.transform.position = position + forward * 0.12f;
            flash.transform.localScale = Vector3.one * 0.08f;
            SetColor(flash, new Color(1f, 0.55f, 0.12f, 0.85f));
            BoomPulse pulse = flash.AddComponent<BoomPulse>();
            pulse.duration = 0.35f;
            pulse.maxScale = 0.85f;
        }

        private void MoveFloatersWithWaterTravel()
        {
            if (!waterTravelEnabled || _floaters.Count == 0)
            {
                return;
            }

            Vector2 dir = waterTravelDirection.sqrMagnitude > 0.0001f
                ? waterTravelDirection.normalized
                : Vector2.up;

            Vector3 step = new Vector3(-dir.x, 0f, -dir.y) * EffectiveTravelSpeed() * Time.deltaTime;
            Vector3 anchor = shipRoot != null ? shipRoot.position : Vector3.zero;
            float halfRange = Mathf.Max(8f, debrisRecyclerHalfRange);
            float fullRange = halfRange * 2f;

            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                Transform floater = _floaters[i];
                if (floater == null)
                {
                    _floaters.RemoveAt(i);
                    continue;
                }

                floater.position += step;

                Vector3 p = floater.position;
                while (p.x - anchor.x > halfRange) p.x -= fullRange;
                while (p.x - anchor.x < -halfRange) p.x += fullRange;
                while (p.z - anchor.z > halfRange) p.z -= fullRange;
                while (p.z - anchor.z < -halfRange) p.z += fullRange;
                floater.position = p;
            }
        }

        private void AnimateStations()
        {
            if (_wheel != null)
            {
                float wheelAngle = Mathf.Clamp(waterTravelDirection.x, -1f, 1f) * -45f;
                _wheel.localRotation = Quaternion.Euler(90f, 0f, wheelAngle);
            }

            if (_sail != null)
            {
                float targetScaleY = _sailDropped ? 1.05f : 0.08f;
                Vector3 s = _sail.localScale;
                s.y = Mathf.Lerp(s.y, targetScaleY, Time.deltaTime * 5f);
                _sail.localScale = s;

                Vector3 targetPosition = new Vector3(0f, _sailDropped ? 1.33f : 1.82f, 0.15f);
                _sail.localPosition = Vector3.Lerp(_sail.localPosition, targetPosition, Time.deltaTime * 5f);
            }

            if (_anchor != null)
            {
                Vector3 target = new Vector3(0f, _anchorLowered ? -0.78f : 0.16f, 1.08f);
                _anchor.localPosition = Vector3.Lerp(_anchor.localPosition, target, Time.deltaTime * 4f);
            }
        }

        private void FloatLooseProps()
        {
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                if (_floaters[i] == null) _floaters.RemoveAt(i);
            }
        }

        private float CurrentWaterYAtShip()
        {
            if (ocean == null || shipRoot == null) return 0f;
            return ocean.SampleHeight(shipRoot.position);
        }

        private void DisableOldEnemyRaftsButKeepWaterTwo()
        {
            foreach (GameObject go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null) continue;
                string n = go.name.ToLowerInvariant();
                if (n.Contains("enemyraft") || n.Contains("debugenemy"))
                {
                    go.SetActive(false);
                }
            }

            GameObject waterTwo = GameObject.Find("Water2");
            if (waterTwo != null)
            {
                waterTwo.SetActive(true);
            }

            foreach (OceanEntitySpawner spawner in FindObjectsOfType<OceanEntitySpawner>())
            {
                spawner.enabled = false;
            }
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
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            SetColor(go, color);
            return go;
        }

        private GameObject CubeUnder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            SetColor(go, color);
            return go;
        }

        private void AddStationButton(GameObject go, LrpDebugStationButton.Action action)
        {
            if (go == null) return;
            Collider c = go.GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
            LrpDebugStationButton button = go.GetComponent<LrpDebugStationButton>();
            if (button == null) button = go.AddComponent<LrpDebugStationButton>();
            button.action = action;
            button.sandbox = this;
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            renderer.material = mat;
        }

        private static void ClearChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null) return;
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            OceanWaveMeshDeformer deformer = waterOne != null ? waterOne.GetComponent<OceanWaveMeshDeformer>() : null;
            string wave = deformer != null
                ? $"Wave amp:{deformer.heightAmplitude:F2} spatial:{deformer.spatialFrequency:F2} speed:{deformer.primaryFrequency:F2}/{deformer.secondaryFrequency:F2}"
                : "Wave: no deformer";

            GUI.Box(new Rect(12f, 12f, 540f, 315f),
                "Living Room Pirates Debug\n\n" +
                "WASD/Arrows: move Water1 tiles under locked ship\n" +
                "1-6: wave presets\n" +
                "SPACE: fire visible cannonballs + boom\n" +
                "C: toggle auto fire\n" +
                "T: toggle sail raise/drop\n" +
                "R: toggle anchor raise/lower\n" +
                "M: toggle water escalator travel\n" +
                "L: toggle this menu\n\n" +
                $"Cannons: {_cannons.Count}  Floating props: {_floaters.Count}\n" +
                $"Sail: {(_sailDropped ? "Dropped" : "Raised")}  Anchor: {(_anchorLowered ? "Lowered" : "Raised")}\n" +
                $"AutoFire: {(autoFireCannons ? "ON" : "OFF")}  WaterTravel: {(waterTravelEnabled ? "ON" : "OFF")} base:{waterTravelSpeed:F1} effective:{EffectiveTravelSpeed():F1} dir:{waterTravelDirection.x:F1},{waterTravelDirection.y:F1}\n" +
                $"WaterY@Ship: {CurrentWaterYAtShip():F2}\n" +
                wave + "\n" +
                "Ship transform: LOCKED");
        }

        private sealed class PrimitiveCannon
        {
            public Transform root;
            public Transform firePoint;
        }
    }
}
