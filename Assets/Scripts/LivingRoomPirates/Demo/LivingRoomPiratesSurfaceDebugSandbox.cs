using System.Collections.Generic;
using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Primitive-only VR mechanics sandbox. The ship/player are locked in room scale;
    /// Water1 tiles move under them. All stations are physical primitive controls with
    /// keyboard debug still available.
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
        public bool startWithOverlayHidden = true;

        [Header("VR Layout Polish")]
        public Vector2 baseStationFootprint = new Vector2(2.85f, 3.15f);
        public float largeShipStationScale = 1.15f;
        public float smallShipMinimumStationScale = 0.48f;
        public float railContentPadding = 0.28f;

        [Header("Sailing / Steering")]
        public bool waterTravelEnabled = false;
        public float waterTravelSpeed = 0.65f;
        public Vector2 waterTravelDirection = new Vector2(0f, 1f);
        [Range(0f, 1f)] public float sailPercent = 0.35f;
        public float sailFullSpeedMultiplier = 3f;
        public float maxWheelTurns = 2.5f;
        public float maxTurnRateDegreesPerSecond = 42f;
        public float wheelReturnTurnsPerSecond = 0.55f;
        public bool useKeyboardForWaterTravel = true;

        [Header("Debris")]
        public float debrisRecyclerHalfRange = 120f;
        public int extraDebrisCount = 90;

        [Header("Cannons")]
        public bool autoFireCannons = false;
        public float autoFireInterval = 4f;
        public float cannonballSpeed = 13.5f;
        public float cannonballLifetime = 7f;
        public bool cannonsStartLoaded = false;

        [Header("Ship Health / Repair")]
        public float shipHealth = 100f;
        public float leakDamagePerSecond = 1.5f;
        public int leakCount = 3;

        [Header("Debug UI")]
        public bool showOverlay = false;

        private readonly List<PrimitiveCannon> _cannons = new List<PrimitiveCannon>();
        private readonly List<Transform> _floaters = new List<Transform>();
        private readonly List<Transform> _leaks = new List<Transform>();
        private readonly List<Renderer> _leakSprays = new List<Renderer>();
        private Transform _debugRoot;
        private Transform _wheel;
        private Transform _goldKnob;
        private Transform _sail;
        private Transform _sailRope;
        private Transform _cleat;
        private Transform _anchor;
        private Transform _anchorWheel;
        private Renderer _sailIndicator;
        private Renderer _anchorIndicator;
        private Renderer _healthIndicator;
        private Vector3 _shipStartPosition;
        private Quaternion _shipStartRotation;
        private Vector3 _shipStartScale;
        private float _layoutScale = 1f;
        private bool _anchorLowered;
        private float _wheelTurns;
        private float _headingDegrees;
        private float _autoFireTimer;

        private void Start()
        {
            ResolveReferences();
            CaptureShipLock();
            if (startWithOverlayHidden) showOverlay = false;
            if (buildOnStart) BuildOrRepair();
            Debug.Log("[LRP SurfaceDebug] Manual VR stations active. A/D steer wheel, Q/E sail %, T full/reef, R anchor, B load nearest cannon, SPACE fire nearest loaded cannon, H repair, L overlay.");
        }

        private void Update()
        {
            ResolveReferences();
            DisableLegacyShipMovementControls();
            HandleKeyboard();
            UpdateSailingModel();
            ApplyWaterEscalatorSettings();
            MoveFloatersWithWaterTravel();
            AnimateStations();
            UpdateLeaksAndHealth();

            if (autoFireCannons)
            {
                _autoFireTimer += Time.deltaTime;
                if (_autoFireTimer >= autoFireInterval)
                {
                    _autoFireTimer = 0f;
                    FireNearestCannonPublic(shipRoot != null ? shipRoot.position : transform.position);
                }
            }
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
            if (shipRoot == null) { Debug.LogWarning("[LRP SurfaceDebug] Missing shipRoot."); return; }

            if (clearOldPrimitiveDebugSystems)
            {
                ClearChild(shipRoot, "LRP_PrimitiveDebugSystems");
                ClearChild(shipRoot, "LRP_SurfaceDebugSystems");
            }

            _debugRoot = new GameObject("LRP_SurfaceDebugSystems").transform;
            _debugRoot.SetParent(shipRoot, false);
            _debugRoot.localPosition = Vector3.zero;
            _debugRoot.localRotation = Quaternion.identity;
            _layoutScale = ComputeVrLayoutScale();
            _debugRoot.localScale = Vector3.one * _layoutScale;

            _cannons.Clear();
            _floaters.Clear();
            _leaks.Clear();
            _leakSprays.Clear();

            RemoveNonFunctionalGeneratedProps();
            BuildSteeringWheel();
            BuildSailRig();
            BuildAnchorCapstan();
            BuildCannonsAndAmmo();
            BuildRepairToolsAndLeaks();
            BuildFloatingSurfaceProps();
            DisableOldEnemyRaftsButKeepWaterTwo();
            ApplyWaterEscalatorSettings();
        }

        public void ToggleSail()
        {
            sailPercent = sailPercent > 0.05f ? 0f : 1f;
        }

        public void AdjustSailPercent(float delta)
        {
            sailPercent = Mathf.Clamp01(sailPercent + delta);
        }

        public void ToggleAnchor()
        {
            _anchorLowered = !_anchorLowered;
        }

        public void NudgeSteering(float turnDelta)
        {
            _wheelTurns = Mathf.Clamp(_wheelTurns + turnDelta, -maxWheelTurns, maxWheelTurns);
        }

        public void CenterSteering()
        {
            _wheelTurns = 0f;
        }

        public void FireAllCannonsPublic()
        {
            // Kept only for old callers; gameplay now fires only the nearest loaded cannon.
            FireNearestCannonPublic(shipRoot != null ? shipRoot.position : transform.position);
        }

        public void LoadAllCannonsPublic()
        {
            // Kept only for old callers; gameplay now loads only the nearest cannon/load zone.
            LoadNearestCannonPublic(shipRoot != null ? shipRoot.position : transform.position);
        }

        public void LoadNearestCannonPublic(Vector3 sourcePosition)
        {
            PrimitiveCannon cannon = FindNearestCannon(sourcePosition, includeLoaded: false);
            if (cannon == null)
            {
                Debug.Log("[LRP SurfaceDebug] No unloaded cannon found near this ammo/load zone.");
                return;
            }
            cannon.loaded = true;
            SetCannonLoadedVisual(cannon, true);
            SpawnLoadPuff(cannon.loadZone != null ? cannon.loadZone.position : cannon.root.position);
        }

        public void FireNearestCannonPublic(Vector3 sourcePosition)
        {
            PrimitiveCannon cannon = FindNearestCannon(sourcePosition, includeLoaded: true);
            if (cannon == null)
            {
                SpawnDryClick(sourcePosition);
                return;
            }
            FireCannon(cannon);
        }

        public void RepairLeaksPublic()
        {
            shipHealth = Mathf.Min(100f, shipHealth + 25f);
            if (_leaks.Count > 0)
            {
                int i = _leaks.Count - 1;
                Transform leak = _leaks[i];
                _leaks.RemoveAt(i);
                if (i < _leakSprays.Count) _leakSprays.RemoveAt(i);
                if (leak != null) Destroy(leak.gameObject);
            }
        }

        private void ResolveReferences()
        {
            if (shipRoot == null)
            {
                GameObject ship = GameObject.Find("shipGeneratedRoot");
                if (ship != null) shipRoot = ship.transform;
            }
            if (ocean == null) ocean = FindObjectOfType<OceanWorldController>();
            if (waterOne == null)
            {
                GameObject w = GameObject.Find("Water1");
                if (w != null) waterOne = w.transform;
            }
        }

        private void CaptureShipLock()
        {
            if (shipRoot == null) return;
            _shipStartPosition = shipRoot.position;
            _shipStartRotation = shipRoot.rotation;
            _shipStartScale = shipRoot.localScale;
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.L)) showOverlay = !showOverlay;
            if (Input.GetKeyDown(KeyCode.Space)) FireNearestCannonPublic(shipRoot != null ? shipRoot.position : transform.position);
            if (Input.GetKeyDown(KeyCode.B)) LoadNearestCannonPublic(shipRoot != null ? shipRoot.position : transform.position);
            if (Input.GetKeyDown(KeyCode.C)) autoFireCannons = !autoFireCannons;
            if (Input.GetKeyDown(KeyCode.T)) ToggleSail();
            if (Input.GetKey(KeyCode.Q)) AdjustSailPercent(-Time.deltaTime * 0.55f);
            if (Input.GetKey(KeyCode.E)) AdjustSailPercent(Time.deltaTime * 0.55f);
            if (Input.GetKeyDown(KeyCode.R)) ToggleAnchor();
            if (Input.GetKeyDown(KeyCode.H)) RepairLeaksPublic();

            if (!useKeyboardForWaterTravel) return;

            bool steeringInput = false;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) { NudgeSteering(-Time.deltaTime * 1.25f); steeringInput = true; }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) { NudgeSteering(Time.deltaTime * 1.25f); steeringInput = true; }
            if (!steeringInput)
            {
                _wheelTurns = Mathf.MoveTowards(_wheelTurns, 0f, wheelReturnTurnsPerSecond * Time.deltaTime);
            }

            // Debug convenience only: W/S changes sail instead of moving the ship.
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) AdjustSailPercent(Time.deltaTime * 0.35f);
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) AdjustSailPercent(-Time.deltaTime * 0.35f);
        }

        private void UpdateSailingModel()
        {
            if (_anchorLowered || sailPercent <= 0.01f)
            {
                waterTravelEnabled = false;
                return;
            }

            waterTravelEnabled = true;
            float steeringNormalized = Mathf.Clamp(_wheelTurns / Mathf.Max(0.001f, maxWheelTurns), -1f, 1f);
            _headingDegrees += steeringNormalized * maxTurnRateDegreesPerSecond * Time.deltaTime;
            Quaternion heading = Quaternion.Euler(0f, _headingDegrees, 0f);
            Vector3 forward = heading * Vector3.forward;
            waterTravelDirection = new Vector2(forward.x, forward.z).normalized;
        }

        private float EffectiveTravelSpeed()
        {
            if (_anchorLowered || sailPercent <= 0.01f) return 0f;
            return waterTravelSpeed * Mathf.Lerp(0f, sailFullSpeedMultiplier, Mathf.Clamp01(sailPercent));
        }

        private void ApplyWaterEscalatorSettings()
        {
            float speed = EffectiveTravelSpeed();
            bool enabledTravel = waterTravelEnabled && speed > 0.001f;
            if (ocean != null)
            {
                ocean.enableWaterEscalatorTravel = enabledTravel;
                ocean.waterEscalatorSpeed = speed;
                ocean.waterEscalatorDirection = waterTravelDirection;
            }
            if (waterOne != null)
            {
                WaterOneGrid3x3 grid = waterOne.GetComponent<WaterOneGrid3x3>();
                if (grid != null)
                {
                    grid.recycleTilesAroundAnchor = true;
                    grid.recycleAnchor = shipRoot;
                    grid.waterTravelEnabled = enabledTravel;
                    grid.waterTravelSpeed = speed;
                    grid.waterTravelDirection = waterTravelDirection;
                }
            }
        }

        private float ComputeVrLayoutScale()
        {
            float usableW = 2.5f, usableD = 2.5f;
            BoundaryShipGenerator generator = shipRoot != null ? shipRoot.GetComponentInParent<BoundaryShipGenerator>() : FindObjectOfType<BoundaryShipGenerator>();
            if (generator != null) { usableW = Mathf.Max(0.5f, generator.UsableWidth); usableD = Mathf.Max(0.5f, generator.UsableDepth); }
            float insideW = Mathf.Max(0.35f, usableW - railContentPadding * 2f);
            float insideD = Mathf.Max(0.35f, usableD - railContentPadding * 2f);
            float fit = Mathf.Min(insideW / Mathf.Max(0.1f, baseStationFootprint.x), insideD / Mathf.Max(0.1f, baseStationFootprint.y));
            float largeBias = Mathf.InverseLerp(2.2f, 4.5f, Mathf.Min(usableW, usableD));
            return Mathf.Clamp(Mathf.Min(Mathf.Lerp(1.0f, largeShipStationScale, largeBias), fit), smallShipMinimumStationScale, largeShipStationScale);
        }

        private Vector3 KeepInsidePlayableDeck(Vector3 p)
        {
            BoundaryShipGenerator generator = shipRoot != null ? shipRoot.GetComponentInParent<BoundaryShipGenerator>() : FindObjectOfType<BoundaryShipGenerator>();
            if (generator == null) return p;
            float maxX = Mathf.Max(0.18f, generator.UsableWidth * 0.5f - railContentPadding);
            float maxZ = Mathf.Max(0.18f, generator.UsableDepth * 0.5f - railContentPadding);
            p.x = Mathf.Clamp(p.x, -maxX, maxX);
            p.z = Mathf.Clamp(p.z, -maxZ, maxZ);
            return p;
        }

        private void RemoveNonFunctionalGeneratedProps()
        {
            if (shipRoot == null) return;
            string[] tokens = { "spyglass", "treasure", "chest", "repairbucket", "bucket", "oar", "doohicky", "debugoscill", "redwhite", "red_and_white" };
            for (int i = shipRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = shipRoot.GetChild(i);
                if (child == null || child == _debugRoot) continue;
                string n = child.name.ToLowerInvariant();
                for (int t = 0; t < tokens.Length; t++)
                {
                    if (n.Contains(tokens[t])) { Destroy(child.gameObject); break; }
                }
            }
        }

        private void BuildSteeringWheel()
        {
            Cube("SteeringPedestal", KeepInsidePlayableDeck(new Vector3(0f, 0.54f, -0.72f)), new Vector3(0.12f, 0.62f, 0.12f), new Color(0.35f, 0.18f, 0.06f));
            _wheel = new GameObject("SteeringWheel_SPINS_WITH_GRABBABLE_KNOBS").transform;
            _wheel.SetParent(_debugRoot, false);
            _wheel.localPosition = KeepInsidePlayableDeck(new Vector3(0f, 0.92f, -0.72f));
            _wheel.localRotation = Quaternion.identity;
            CylinderUnder(_wheel, "WheelHub", Vector3.zero, new Vector3(0.08f, 0.06f, 0.08f), new Color(0.42f, 0.22f, 0.08f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f + Mathf.PI * 0.5f;
                Vector3 knobPos = new Vector3(Mathf.Cos(a) * 0.34f, Mathf.Sin(a) * 0.34f, 0f);
                CubeUnder(_wheel, "WheelSpoke_" + i, knobPos * 0.5f, new Vector3(0.035f, knobPos.magnitude, 0.035f), new Color(0.40f, 0.20f, 0.07f)).transform.localRotation = Quaternion.Euler(0f, 0f, -i * 45f);
                GameObject knob = SphereUnder(_wheel, "WheelKnob_GRAB_STEER_" + i, knobPos, Vector3.one * 0.085f, i == 0 ? new Color(1f, 0.72f, 0.12f) : new Color(0.58f, 0.32f, 0.10f));
                AddStationButton(knob, i < 4 ? LrpDebugStationButton.Action.SteerRight : LrpDebugStationButton.Action.SteerLeft);
                if (i == 0) _goldKnob = knob.transform;
            }
        }

        private void BuildSailRig()
        {
            float z = -1.42f;
            Cube("MastBehindSteering", new Vector3(0f, 1.15f, z), new Vector3(0.12f, 2.1f, 0.12f), new Color(0.36f, 0.18f, 0.07f));
            Cube("YardArm", new Vector3(0f, 1.82f, z), new Vector3(1.55f, 0.08f, 0.08f), new Color(0.36f, 0.18f, 0.07f));
            _sail = Cube("VariableSail_SAIL_PERCENT_CONTROLS_SPEED", new Vector3(0f, 1.33f, z + 0.03f), new Vector3(1.20f, 1.05f, 0.035f), new Color(0.92f, 0.86f, 0.64f)).transform;
            _sailRope = Cylinder("SailMainRope_PULL_DOWN_TO_RAISE", new Vector3(-0.86f, 0.92f, z + 0.10f), new Vector3(0.035f, 0.62f, 0.035f), new Color(0.80f, 0.68f, 0.42f)).transform;
            AddStationButton(_sailRope.gameObject, LrpDebugStationButton.Action.ToggleSail);
            AddStationButton(Cylinder("SailRaiseRope_GRAB_OR_E", new Vector3(-1.08f, 0.86f, z + 0.10f), new Vector3(0.03f, 0.48f, 0.03f), new Color(0.86f, 0.75f, 0.50f)), LrpDebugStationButton.Action.RaiseSail);
            AddStationButton(Cylinder("SailLowerRope_GRAB_OR_Q", new Vector3(-0.64f, 0.86f, z + 0.10f), new Vector3(0.03f, 0.48f, 0.03f), new Color(0.86f, 0.75f, 0.50f)), LrpDebugStationButton.Action.LowerSail);
            _cleat = Cube("SailCleat_TIE_ROPE_HERE_TO_HOLD_PERCENT", new Vector3(-0.86f, 0.48f, z + 0.14f), new Vector3(0.32f, 0.08f, 0.10f), new Color(0.45f, 0.23f, 0.08f)).transform;
            for (int i = 0; i < 5; i++) RopeBetween("SailRiggingRope_" + i, new Vector3(-0.7f + i * 0.35f, 1.80f, z + 0.05f), new Vector3(-1.0f + i * 0.5f, 0.62f, z + 0.45f), 0.018f);
            GameObject lantern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lantern.name = "SailPercentLantern_round_indicator";
            lantern.transform.SetParent(_debugRoot, false);
            lantern.transform.localPosition = new Vector3(-1.35f, 0.47f, z + 0.10f);
            lantern.transform.localScale = Vector3.one * 0.09f;
            SetColor(lantern, new Color(0.9f, 0.75f, 0.18f));
            _sailIndicator = lantern.GetComponent<Renderer>();
        }

        private void BuildAnchorCapstan()
        {
            _anchor = new GameObject("AnchorAssembly_ANCHOR_DOWN_STOPS_MOVEMENT").transform;
            _anchor.SetParent(_debugRoot, false);
            _anchor.localPosition = new Vector3(0f, 0.16f, 1.08f);
            _anchor.localRotation = Quaternion.identity;
            _anchorWheel = Cylinder("HorizontalAnchorCapstan_GRAB_OR_R", new Vector3(0.72f, 0.62f, 0.98f), new Vector3(0.30f, 0.06f, 0.30f), new Color(0.18f, 0.18f, 0.20f)).transform;
            _anchorWheel.localRotation = Quaternion.identity;
            AddStationButton(_anchorWheel.gameObject, LrpDebugStationButton.Action.ToggleAnchor);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.28f, 0f, Mathf.Sin(a) * 0.28f);
                GameObject knob = SphereUnder(_anchorWheel, "AnchorCapstanKnob_GRAB_" + i, p, Vector3.one * 0.075f, new Color(0.35f, 0.35f, 0.38f));
                AddStationButton(knob, LrpDebugStationButton.Action.ToggleAnchor);
            }
            CylinderUnder(_anchor, "AnchorChain", new Vector3(0f, 0.12f, 0f), new Vector3(0.035f, 0.65f, 0.035f), Color.gray);
            CubeUnder(_anchor, "AnchorHook", new Vector3(0f, -0.28f, 0f), new Vector3(0.26f, 0.22f, 0.08f), new Color(0.08f, 0.08f, 0.08f));
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "AnchorStatusLantern_round_indicator";
            indicator.transform.SetParent(_debugRoot, false);
            indicator.transform.localPosition = new Vector3(1.05f, 0.82f, 0.98f);
            indicator.transform.localScale = Vector3.one * 0.09f;
            SetColor(indicator, Color.green);
            _anchorIndicator = indicator.GetComponent<Renderer>();
        }

        private void BuildCannonsAndAmmo()
        {
            AddCannon("BowCannon", new Vector3(0f, 0.42f, 1.0f), Quaternion.identity);
            AddCannon("PortCannon", new Vector3(-0.95f, 0.42f, 0f), Quaternion.Euler(0f, -90f, 0f));
            AddCannon("StarboardCannon", new Vector3(0.95f, 0.42f, 0f), Quaternion.Euler(0f, 90f, 0f));
            AddCannon("SternCannon", new Vector3(0f, 0.42f, -1.0f), Quaternion.Euler(0f, 180f, 0f));
            BuildCannonballPile("InfiniteAmmoPile_Port_GRAB_SPAWNS_BALL", new Vector3(-0.55f, 0.18f, -0.42f));
            BuildCannonballPile("InfiniteAmmoPile_Starboard_GRAB_SPAWNS_BALL", new Vector3(0.55f, 0.18f, -0.42f));
        }

        private void AddCannon(string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject pivot = new GameObject(name + "_manual_load_then_fuse");
            pivot.transform.SetParent(_debugRoot, false);
            pivot.transform.localPosition = KeepInsidePlayableDeck(localPosition);
            pivot.transform.localRotation = localRotation;
            pivot.transform.localScale = Vector3.one * 1.18f;
            CubeUnder(pivot.transform, name + "_Base", Vector3.zero, new Vector3(0.42f, 0.2f, 0.36f), new Color(0.25f, 0.12f, 0.04f));
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
            Transform loadZone = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            loadZone.name = name + "_MuzzleLoadZone_release_ball_here";
            loadZone.SetParent(pivot.transform, false);
            loadZone.localPosition = new Vector3(0f, 0.09f, 0.58f);
            loadZone.localScale = Vector3.one * 0.20f;
            SetColor(loadZone.gameObject, new Color(0.10f, 0.55f, 0.95f, 0.35f));
            AddStationButton(loadZone.gameObject, LrpDebugStationButton.Action.LoadCannons);
            GameObject fuse = CylinderUnder(pivot.transform, name + "_FusePullRope_FIRE_LOADED_ONLY", new Vector3(0.25f, 0.13f, 0.05f), new Vector3(0.035f, 0.24f, 0.035f), new Color(0.76f, 0.58f, 0.25f));
            AddStationButton(fuse, LrpDebugStationButton.Action.FireCannons);
            GameObject light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            light.name = name + "_LoadedLantern_round_indicator";
            light.transform.SetParent(pivot.transform, false);
            light.transform.localPosition = new Vector3(-0.25f, 0.18f, 0.05f);
            light.transform.localScale = Vector3.one * 0.085f;
            SetColor(light, cannonsStartLoaded ? Color.green : new Color(0.18f,0.18f,0.18f));
            GameObject loadedBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            loadedBall.name = name + "_VisibleBallInBarrel_when_loaded";
            loadedBall.transform.SetParent(pivot.transform, false);
            loadedBall.transform.localPosition = new Vector3(0f, 0.09f, 0.51f);
            loadedBall.transform.localScale = Vector3.one * 0.13f;
            SetColor(loadedBall, Color.black);
            loadedBall.SetActive(cannonsStartLoaded);
            _cannons.Add(new PrimitiveCannon { root = pivot.transform, firePoint = firePoint, loadZone = loadZone, loaded = cannonsStartLoaded, loadedIndicator = light.GetComponent<Renderer>(), loadedBall = loadedBall.transform });
        }

        private void BuildCannonballPile(string name, Vector3 localPosition)
        {
            Transform pile = new GameObject(name).transform;
            pile.SetParent(_debugRoot, false);
            pile.localPosition = KeepInsidePlayableDeck(localPosition);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "InfinitePileVisualBall_grab_source_" + i;
                ball.transform.SetParent(pile, false);
                ball.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.16f, (i/4)*0.10f, Mathf.Sin(a) * 0.11f);
                ball.transform.localScale = Vector3.one * 0.13f;
                SetColor(ball, Color.black);
                AddStationButton(ball, LrpDebugStationButton.Action.LoadCannons);
            }
            GameObject source = CubeUnder(pile, "AMMO_SOURCE_GRAB_B_SPAWNS_AND_LOADS_NEAREST", new Vector3(0f, -0.13f, 0f), new Vector3(0.55f, 0.06f, 0.40f), new Color(0.10f, 0.16f, 0.10f));
            AddStationButton(source, LrpDebugStationButton.Action.LoadCannons);
        }

        private void BuildRepairToolsAndLeaks()
        {
            GameObject hammer = Cube("RepairHammer_GRAB_OR_H", new Vector3(-0.42f, 0.36f, -0.18f), new Vector3(0.12f, 0.12f, 0.55f), new Color(0.45f, 0.25f, 0.10f));
            CubeUnder(hammer.transform, "RepairHammerHead", new Vector3(0f, 0.10f, 0.24f), new Vector3(0.30f, 0.18f, 0.16f), new Color(0.45f, 0.45f, 0.48f));
            AddStationButton(hammer, LrpDebugStationButton.Action.RepairLeaks);
            _healthIndicator = Cube("SHIP_HEALTH_INDICATOR_PHYSICAL_BAR", new Vector3(0f, 0.22f, -0.18f), new Vector3(0.90f, 0.08f, 0.04f), Color.green).GetComponent<Renderer>();
            Vector3[] positions = { new Vector3(-0.65f,0.20f,0.62f), new Vector3(0.70f,0.20f,0.28f), new Vector3(-0.18f,0.20f,-0.66f), new Vector3(0.25f,0.20f,0.82f) };
            int count = Mathf.Clamp(leakCount, 0, positions.Length);
            for (int i = 0; i < count; i++)
            {
                Transform leak = new GameObject("VisibleTopDeckLeak_WATER_SPRAY_REPAIR_" + i).transform;
                leak.SetParent(_debugRoot, false);
                leak.localPosition = positions[i];
                GameObject hole = CubeUnder(leak, "LeakHole", Vector3.zero, new Vector3(0.22f,0.035f,0.22f), Color.black);
                AddStationButton(hole, LrpDebugStationButton.Action.RepairLeaks);
                GameObject spray = CylinderUnder(leak, "LeakWaterSpray", new Vector3(0f,0.26f,0f), new Vector3(0.045f,0.38f,0.045f), new Color(0.35f,0.70f,1f,0.85f));
                _leaks.Add(leak);
                _leakSprays.Add(spray.GetComponent<Renderer>());
            }
        }

        private void BuildFloatingSurfaceProps()
        {
            AddFloater("FloatingBarrel_A", PrimitiveType.Cylinder, new Vector3(-3.0f,0f,3.2f), new Vector3(0.35f,0.55f,0.35f), new Color(0.44f,0.22f,0.08f));
            AddFloater("FloatingCrate_A", PrimitiveType.Cube, new Vector3(2.8f,0f,2.5f), new Vector3(0.55f,0.32f,0.55f), new Color(0.50f,0.28f,0.10f));
            Random.InitState(1817);
            for (int i = 0; i < extraDebrisCount; i++)
            {
                float x = Random.Range(-debrisRecyclerHalfRange * 0.85f, debrisRecyclerHalfRange * 0.85f);
                float z = Random.Range(-debrisRecyclerHalfRange * 0.85f, debrisRecyclerHalfRange * 0.85f);
                PrimitiveType type = i % 3 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                Vector3 scale = type == PrimitiveType.Cylinder ? new Vector3(Random.Range(0.20f,0.42f), Random.Range(0.30f,0.75f), Random.Range(0.20f,0.42f)) : new Vector3(Random.Range(0.25f,1.15f), Random.Range(0.06f,0.35f), Random.Range(0.18f,0.85f));
                AddFloater("FloatingDebris_Extra_" + i, type, new Vector3(x,0f,z), scale, i % 4 == 0 ? new Color(0.50f,0.28f,0.10f) : new Color(0.35f,0.18f,0.06f));
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
            follower.moveWithWaterConveyor = false;
            _floaters.Add(go.transform);
        }

        private void MoveFloatersWithWaterTravel()
        {
            if (!waterTravelEnabled || _floaters.Count == 0) return;
            Vector2 dir = waterTravelDirection.sqrMagnitude > 0.0001f ? waterTravelDirection.normalized : Vector2.up;
            Vector3 step = new Vector3(-dir.x, 0f, -dir.y) * EffectiveTravelSpeed() * Time.deltaTime;
            Vector3 anchor = shipRoot != null ? shipRoot.position : Vector3.zero;
            float half = Mathf.Max(8f, debrisRecyclerHalfRange);
            float full = half * 2f;
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                Transform f = _floaters[i];
                if (f == null) { _floaters.RemoveAt(i); continue; }
                Vector3 p = f.position + step;
                while (p.x - anchor.x > half) p.x -= full;
                while (p.x - anchor.x < -half) p.x += full;
                while (p.z - anchor.z > half) p.z -= full;
                while (p.z - anchor.z < -half) p.z += full;
                f.position = p;
            }
        }

        private void AnimateStations()
        {
            if (_wheel != null)
            {
                _wheel.localRotation = Quaternion.Euler(0f, 0f, -_wheelTurns * 360f);
            }
            if (_sail != null)
            {
                float pct = Mathf.Clamp01(sailPercent);
                Vector3 s = _sail.localScale;
                s.y = Mathf.Lerp(s.y, Mathf.Lerp(0.08f, 1.05f, pct), Time.deltaTime * 5f);
                _sail.localScale = s;
                _sail.localPosition = Vector3.Lerp(_sail.localPosition, new Vector3(0f, Mathf.Lerp(1.82f, 1.33f, pct), -1.39f), Time.deltaTime * 5f);
            }
            if (_sailRope != null)
            {
                Vector3 p = _sailRope.localPosition;
                p.y = Mathf.Lerp(0.68f, 1.05f, 1f - sailPercent);
                _sailRope.localPosition = Vector3.Lerp(_sailRope.localPosition, p, Time.deltaTime * 5f);
            }
            if (_anchor != null)
            {
                _anchor.localPosition = Vector3.Lerp(_anchor.localPosition, new Vector3(0f, _anchorLowered ? -0.78f : 0.16f, 1.08f), Time.deltaTime * 4f);
            }
            if (_anchorWheel != null)
            {
                _anchorWheel.localRotation = Quaternion.Euler(0f, Time.time * (_anchorLowered ? 0f : 0f) + (_anchorLowered ? 220f : 0f), 0f);
            }
            if (_sailIndicator != null) _sailIndicator.material.color = Color.Lerp(new Color(0.55f,0.45f,0.12f), new Color(0.15f,0.85f,0.20f), Mathf.Clamp01(sailPercent));
            if (_anchorIndicator != null) _anchorIndicator.material.color = _anchorLowered ? new Color(0.85f,0.12f,0.08f) : new Color(0.15f,0.85f,0.20f);
        }

        private void UpdateLeaksAndHealth()
        {
            int activeLeaks = 0;
            for (int i = _leaks.Count - 1; i >= 0; i--)
            {
                Transform leak = _leaks[i];
                if (leak == null) { _leaks.RemoveAt(i); continue; }
                activeLeaks++;
                leak.localScale = new Vector3(1f, 0.75f + Mathf.Sin(Time.time * 8f + i) * 0.25f, 1f);
            }
            if (activeLeaks > 0) shipHealth = Mathf.Max(0f, shipHealth - activeLeaks * leakDamagePerSecond * Time.deltaTime);
            if (_healthIndicator != null)
            {
                float t = Mathf.Clamp01(shipHealth / 100f);
                _healthIndicator.transform.localScale = new Vector3(Mathf.Lerp(0.08f,0.90f,t), 0.08f, 0.04f);
                _healthIndicator.material.color = Color.Lerp(Color.red, Color.green, t);
            }
        }

        private PrimitiveCannon FindNearestCannon(Vector3 source, bool includeLoaded)
        {
            PrimitiveCannon best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < _cannons.Count; i++)
            {
                PrimitiveCannon c = _cannons[i];
                if (c == null || c.root == null) continue;
                if (includeLoaded && !c.loaded) continue;
                if (!includeLoaded && c.loaded) continue;
                Vector3 refPos = includeLoaded ? c.root.position : (c.loadZone != null ? c.loadZone.position : c.root.position);
                float d = (refPos - source).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        private void FireCannon(PrimitiveCannon cannon)
        {
            if (cannon == null || cannon.firePoint == null || !cannon.loaded) { SpawnDryClick(cannon != null && cannon.root != null ? cannon.root.position : transform.position); return; }
            cannon.loaded = false;
            SetCannonLoadedVisual(cannon, false);
            SpawnBoom(cannon.firePoint.position, cannon.root.TransformDirection(Vector3.forward));
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "VisibleManualCannonball";
            ball.transform.position = cannon.firePoint.position;
            ball.transform.localScale = Vector3.one * 0.18f;
            SetColor(ball, Color.black);
            Collider col = ball.GetComponent<Collider>(); if (col != null) col.enabled = false;
            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.mass = 0.55f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 dir = cannon.root.TransformDirection(Vector3.forward);
            dir.y = 0.08f;
            rb.AddForce(dir.normalized * cannonballSpeed, ForceMode.Impulse);
            TrailRenderer trail = ball.AddComponent<TrailRenderer>();
            trail.time = 0.45f; trail.startWidth = 0.10f; trail.endWidth = 0.01f;
            Material mat = new Material(Shader.Find("Sprites/Default")); mat.color = new Color(0.18f,0.18f,0.18f,0.8f); trail.material = mat;
            CannonballWaterImpact impact = ball.AddComponent<CannonballWaterImpact>();
            impact.ocean = ocean; impact.lifetime = cannonballLifetime;
        }

        private void SetCannonLoadedVisual(PrimitiveCannon cannon, bool loaded)
        {
            if (cannon == null) return;
            if (cannon.loadedIndicator != null) cannon.loadedIndicator.material.color = loaded ? new Color(0.15f,0.85f,0.20f) : new Color(0.18f,0.18f,0.18f);
            if (cannon.loadedBall != null) cannon.loadedBall.gameObject.SetActive(loaded);
        }

        private void SpawnDryClick(Vector3 position)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "CannonUnloadedSoftPuff_no_ammo";
            puff.transform.position = position + Vector3.up * 0.08f;
            puff.transform.localScale = Vector3.one * 0.07f;
            SetColor(puff, new Color(0.35f,0.35f,0.35f,0.65f));
            BoomPulse pulse = puff.AddComponent<BoomPulse>(); pulse.duration = 0.35f; pulse.maxScale = 0.22f;
        }

        private void SpawnLoadPuff(Vector3 position)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "CannonLoadedGreenPuff";
            puff.transform.position = position;
            puff.transform.localScale = Vector3.one * 0.06f;
            SetColor(puff, new Color(0.15f,0.85f,0.20f,0.65f));
            BoomPulse pulse = puff.AddComponent<BoomPulse>(); pulse.duration = 0.25f; pulse.maxScale = 0.28f;
        }

        private void SpawnBoom(Vector3 position, Vector3 forward)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "CannonBoomFlash_round";
            flash.transform.position = position + forward.normalized * 0.12f;
            flash.transform.localScale = Vector3.one * 0.12f;
            SetColor(flash, new Color(1f,0.55f,0.12f,0.85f));
            BoomPulse pulse = flash.AddComponent<BoomPulse>(); pulse.duration = 0.35f; pulse.maxScale = 1.05f;
        }

        private void DisableOldEnemyRaftsButKeepWaterTwo()
        {
            foreach (GameObject go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null) continue;
                string n = go.name.ToLowerInvariant();
                if (n.Contains("enemyraft") || n.Contains("debugenemy")) go.SetActive(false);
            }
            GameObject waterTwo = GameObject.Find("Water2");
            if (waterTwo != null) waterTwo.SetActive(true);
            foreach (OceanEntitySpawner spawner in FindObjectsOfType<OceanEntitySpawner>()) spawner.enabled = false;
        }

        private void DisableLegacyShipMovementControls()
        {
            foreach (LivingRoomPiratesShipKeyboardDebugControls controls in FindObjectsOfType<LivingRoomPiratesShipKeyboardDebugControls>())
            {
                if (controls != null && controls.enabled) { controls.showOverlay = false; controls.enabled = false; }
            }
            foreach (LivingRoomPiratesPrimitiveDebugSandbox sandbox in FindObjectsOfType<LivingRoomPiratesPrimitiveDebugSandbox>())
            {
                if (sandbox != null && sandbox.enabled) sandbox.enabled = false;
            }
        }

        private GameObject RopeBetween(string name, Vector3 fromLocal, Vector3 toLocal, float thickness)
        {
            Vector3 delta = toLocal - fromLocal;
            if (delta.magnitude < 0.001f) return null;
            GameObject rope = Cube(name, fromLocal + delta * 0.5f, new Vector3(thickness, thickness, delta.magnitude), new Color(0.80f,0.68f,0.42f));
            rope.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            return rope;
        }

        private GameObject Cube(string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(_debugRoot, false); go.transform.localPosition = localPosition; go.transform.localRotation = Quaternion.identity; go.transform.localScale = localScale; SetColor(go, color); return go;
        }
        private GameObject Cylinder(string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name; go.transform.SetParent(_debugRoot, false); go.transform.localPosition = localPosition; go.transform.localRotation = Quaternion.identity; go.transform.localScale = localScale; SetColor(go, color); return go;
        }
        private GameObject CubeUnder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = localPosition; go.transform.localRotation = Quaternion.identity; go.transform.localScale = localScale; SetColor(go, color); return go;
        }
        private GameObject CylinderUnder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = localPosition; go.transform.localRotation = Quaternion.identity; go.transform.localScale = localScale; SetColor(go, color); return go;
        }
        private GameObject SphereUnder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = localPosition; go.transform.localRotation = Quaternion.identity; go.transform.localScale = localScale; SetColor(go, color); return go;
        }

        private void AddStationButton(GameObject go, LrpDebugStationButton.Action action)
        {
            if (go == null) return;
            Collider c = go.GetComponent<Collider>(); if (c != null) c.isTrigger = true;
            Rigidbody rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            LrpDebugStationButton b = go.GetComponent<LrpDebugStationButton>(); if (b == null) b = go.AddComponent<LrpDebugStationButton>();
            b.action = action; b.sandbox = this;
            TryAddXrSimpleInteractable(go);
        }

        private static void TryAddXrSimpleInteractable(GameObject go)
        {
            if (go == null) return;
            string[] typeNames = { "UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit", "UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable, Unity.XR.Interaction.Toolkit" };
            for (int i = 0; i < typeNames.Length; i++)
            {
                System.Type t = System.Type.GetType(typeNames[i]);
                if (t == null) continue;
                if (go.GetComponent(t) == null) go.AddComponent(t);
                return;
            }
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>(); if (r == null) return;
            Material mat = new Material(Shader.Find("Standard")); mat.color = color; r.material = mat;
        }

        private static void ClearChild(Transform parent, string name)
        {
            Transform child = parent.Find(name); if (child == null) return;
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            GUI.Box(new Rect(12f, 12f, 520f, 190f),
                "LRP Manual Debug\n" +
                "A/D steer wheel, wheel slowly returns; W/S or Q/E adjusts sail; T full/reef; R anchor\n" +
                "B/load zones load nearest cannon; SPACE/fuse fires nearest loaded cannon; H repair\n" +
                $"Sail {sailPercent:P0} Anchor {(_anchorLowered ? "DOWN/STOP" : "UP")} WheelTurns {_wheelTurns:F2} Heading {_headingDegrees:F0}\n" +
                $"Travel {(waterTravelEnabled ? "ON" : "OFF")} Speed {EffectiveTravelSpeed():F2} Dir {waterTravelDirection.x:F2},{waterTravelDirection.y:F2}\n" +
                $"Cannons {_cannons.Count} Floaters {_floaters.Count} Health {shipHealth:F0} Leaks {_leaks.Count}");
        }

        private sealed class PrimitiveCannon
        {
            public Transform root;
            public Transform firePoint;
            public Transform loadZone;
            public bool loaded;
            public Renderer loadedIndicator;
            public Transform loadedBall;
        }
    }
}
