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
        public float largeShipStationScale = 2.0f;
        public float smallShipMinimumStationScale = 0.90f;
        public float railContentPadding = 0.28f;

        [Header("Sailing / Steering")]
        public bool waterTravelEnabled = false;
        public float waterTravelSpeed = 0.45f;
        public Vector2 waterTravelDirection = new Vector2(0f, 1f);
        [Range(0f, 1f)] public float sailPercent = 0.35f;
        public float sailFullSpeedMultiplier = 3f;

        [Header("Wind / Sheet Trim")]
        public LrpWindController windController;
        [Range(-70f, 70f)] public float sailSheetAngleDegrees = 0f;
        public float sheetAdjustDegreesPerMeter = 85f;
        [HideInInspector] public float lastWindAlignment = 1f;
        [HideInInspector] public float lastSailTrimEfficiency = 1f;
        [HideInInspector] public float lastWindSpeedMultiplier = 1f;

        public float maxWheelTurns = 2.5f;
        public float maxTurnRateDegreesPerSecond = 42f;
        public float wheelReturnTurnsPerSecond = 0.55f;
        public bool useKeyboardForWaterTravel = true;

        [Header("Debris")]
        public float debrisRecyclerHalfRange = 180f;
        public int extraDebrisCount = 32;

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
        public Vector3 hammerGripRotationOffsetEuler = new Vector3(0f, 90f, 0f);

        [Header("Debug UI")]
        public bool showOverlay = false;

        private readonly List<PrimitiveCannon> _cannons = new List<PrimitiveCannon>();
        private readonly List<Transform> _floaters = new List<Transform>();
        private readonly Dictionary<Transform, Vector2> _floaterOceanPositions = new Dictionary<Transform, Vector2>();
        private readonly List<Transform> _leaks = new List<Transform>();
        private readonly List<Renderer> _leakSprays = new List<Renderer>();
        private readonly Dictionary<Transform, int> _leakHammerHits = new Dictionary<Transform, int>();
        private readonly HashSet<Transform> _hammerCurrentlyInsideLeak = new HashSet<Transform>();
        private const int HammerHitsRequiredPerLeak = 4;
        private const float HammerLeakHitRadius = 0.24f;
        private Transform _debugRoot;
        private Transform _wheel;
        private Transform _goldKnob;
        private Transform _sail;
        private Transform _sailRope;
        private Transform _cleat;
        private readonly List<Transform> _sheetGripSegments = new List<Transform>();
        private Transform _anchor;
        private Transform _anchorWheel;
        private Transform _anchorChain;
        private Transform _anchorHook;
        private Renderer _sailIndicator;
        private Renderer _anchorIndicator;
        private Renderer _healthIndicator;
        private Vector3 _shipStartPosition;
        private Quaternion _shipStartRotation;
        private Vector3 _shipStartScale;
        private float _layoutScale = 1f;
        private bool _stationaryDinghyLayout;
        private bool _anchorLowered;
        private float _wheelTurns;
        private float _headingDegrees;
        private Vector3 _currentOceanConveyorStepWorld;
        private float _currentTravelSpeed;
        private float _autoFireTimer;

        private sealed class PhysicalHandGrabState
        {
            public LrpDebugStationButton.Action action;
            public Transform handle;
            public Vector3 startWorld;
            public Vector3 lastWorld;
            public Vector3 velocityWorld;
            public Quaternion handRotation;
            public float startWheelAngle;
            public float startWheelTurns;
            public float startSailPercent;
            public float startSailY;
            public float startAnchorAngle;
            public float startAnchorPercent;
            public bool fuseFired;
            public GameObject heldCannonball;
            public GameObject heldHammer;
        }

        private readonly PhysicalHandGrabState[] _handGrabs = { new PhysicalHandGrabState(), new PhysicalHandGrabState() };
        private int _wheelDriverHand = -1;
        private int _sailDriverHand = -1;
        private int _anchorDriverHand = -1;
        private float _anchorPercent;
        private bool _sailRopeHeld;
        private readonly List<GameObject> _manualCannonballs = new List<GameObject>();
        private const int MaxManualCannonballs = 5;
        private GameObject _heldHammer;

        private PhysicalHandGrabState HandStateFor(int handId)
        {
            int i = Mathf.Clamp(handId, 0, _handGrabs.Length - 1);
            return _handGrabs[i];
        }

        private bool AnyHandHoldingSailRope()
        {
            for (int i = 0; i < _handGrabs.Length; i++)
                if (_handGrabs[i].action == LrpDebugStationButton.Action.SailRope && _handGrabs[i].handle != null) return true;
            return false;
        }
        private bool AnyHandHoldingCannonball()
        {
            for (int i = 0; i < _handGrabs.Length; i++)
                if (_handGrabs[i].heldCannonball != null) return true;
            return false;
        }


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
            UpdateContextualInteractionIndicators();
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
            _stationaryDinghyLayout = IsStationaryDinghyLayout();
            _debugRoot.localScale = Vector3.one * _layoutScale;

            _cannons.Clear();
            _floaters.Clear();
            _floaterOceanPositions.Clear();
            _leaks.Clear();
            _leakSprays.Clear();
            _leakHammerHits.Clear();
            _hammerCurrentlyInsideLeak.Clear();
            _sheetGripSegments.Clear();

            RemoveNonFunctionalGeneratedProps();
            EnsureVisibleDeckAndRailFrame();
            BuildSteeringWheel();
            BuildSailRig();
            BuildAnchorCapstan();
            BuildCannonsAndAmmo();
            BuildRepairToolsAndLeaks();
            BuildWindControllerAndSheetControls();
            BuildTinyInstructionSigns();
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

        public void AdjustSheetAngle(float deltaDegrees)
        {
            sailSheetAngleDegrees = Mathf.Clamp(sailSheetAngleDegrees + deltaDegrees, -70f, 70f);
        }

        public void ToggleAnchor()
        {
            _anchorLowered = !_anchorLowered;
            _anchorPercent = _anchorLowered ? 1f : 0f;
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
            // Loaded state is shown physically by the barrel ball/lantern; no green flash.
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
            if (windController == null) windController = FindObjectOfType<LrpWindController>();
            if (windController == null && shipRoot != null) windController = shipRoot.GetComponentInChildren<LrpWindController>();
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
            if (Input.GetKey(KeyCode.Z)) AdjustSheetAngle(-Time.deltaTime * 38f);
            if (Input.GetKey(KeyCode.X)) AdjustSheetAngle(Time.deltaTime * 38f);
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
            // Wheel sign fix: positive wheel rotation should make the apparent ship turn left/right correctly.
            // The ship itself stays locked; the ocean rotates/moves underneath it.
            _headingDegrees += steeringNormalized * maxTurnRateDegreesPerSecond * Time.deltaTime;
            Quaternion heading = Quaternion.Euler(0f, _headingDegrees, 0f);
            Vector3 forward = heading * Vector3.forward;
            waterTravelDirection = new Vector2(forward.x, forward.z).normalized;
        }

        private float EffectiveTravelSpeed()
        {
            if (_anchorLowered || sailPercent <= 0.01f) return 0f;

            float windMultiplier = 1f;
            lastWindAlignment = 1f;
            lastSailTrimEfficiency = 1f;
            if (windController != null)
            {
                Vector3 shipForward = new Vector3(waterTravelDirection.x, 0f, waterTravelDirection.y);
                if (shipForward.sqrMagnitude < 0.001f) shipForward = Vector3.forward;
                windMultiplier = windController.ComputeSailingMultiplier(shipForward.normalized, sailSheetAngleDegrees, out lastWindAlignment, out lastSailTrimEfficiency);
            }
            lastWindSpeedMultiplier = windMultiplier;
            return waterTravelSpeed * Mathf.Lerp(0f, sailFullSpeedMultiplier, Mathf.Clamp01(sailPercent)) * windMultiplier;
        }

        private void ApplyWaterEscalatorSettings()
        {
            float speed = EffectiveTravelSpeed();
            bool enabledTravel = waterTravelEnabled && speed > 0.001f;
            _currentTravelSpeed = enabledTravel ? speed : 0f;
            Vector2 dir2 = waterTravelDirection.sqrMagnitude > 0.0001f ? waterTravelDirection.normalized : Vector2.up;
            _currentOceanConveyorStepWorld = new Vector3(-dir2.x, 0f, -dir2.y) * _currentTravelSpeed * Time.deltaTime;
            if (ocean != null)
            {
                ocean.enableWaterEscalatorTravel = enabledTravel;
                ocean.waterEscalatorSpeed = speed;
                ocean.waterEscalatorDirection = dir2;
                ocean.shipHeadingDegrees = _headingDegrees;
            }
            if (waterOne != null)
            {
                WaterOneGrid3x3 grid = waterOne.GetComponent<WaterOneGrid3x3>();
                if (grid != null)
                {
                    grid.gridRadius = Mathf.Max(grid.gridRadius, 2);
                    grid.tileSize = Mathf.Max(grid.tileSize, 70f);
                    grid.forwardBiasTiles = 0f;
                    grid.earlyRecyclePaddingTiles = Mathf.Max(grid.earlyRecyclePaddingTiles, 0.2f);
                    grid.recycleTilesAroundAnchor = true;
                    grid.recycleAnchor = shipRoot;
                    grid.waterTravelEnabled = enabledTravel;
                    grid.waterTravelSpeed = speed;
                    grid.waterTravelDirection = waterTravelDirection;
                    // The water grid yaw is the virtual ocean/ship heading authority.
                    // Tile movement remains in grid-local -Z, so world movement becomes opposite ship-forward.
                    grid.visualYawDegrees = _headingDegrees;
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

        private bool IsStationaryDinghyLayout()
        {
            BoundaryShipGenerator generator = shipRoot != null ? shipRoot.GetComponentInParent<BoundaryShipGenerator>() : FindObjectOfType<BoundaryShipGenerator>();
            if (generator == null) return _layoutScale <= 0.72f;

            // Stationary Guardian/no-roomscale support: the player stands at the center,
            // so every required control must be arm-reachable from the origin. The mast/sail
            // may extend above/outside the boundary, and cannon barrels may point outside, but
            // wheel knobs, sail rope/cleat, anchor capstan, ammo pile, cannon breech, fuse,
            // hammer, and leak repair targets must remain inside the rails.
            float minDim = Mathf.Min(generator.UsableWidth, generator.UsableDepth);
            return generator.CurrentTier == ShipTier.Dinghy || minDim < 1.65f || _layoutScale <= 0.72f;
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

        private void EnsureVisibleDeckAndRailFrame()
        {
            if (shipRoot == null) return;
            if (shipRoot.Find("LRP_AlwaysVisibleDeckAndRails") != null) return;

            BoundaryShipGenerator generator = shipRoot.GetComponentInParent<BoundaryShipGenerator>();
            float width = generator != null && generator.UsableWidth > 0.5f ? generator.UsableWidth : baseStationFootprint.x;
            float depth = generator != null && generator.UsableDepth > 0.5f ? generator.UsableDepth : baseStationFootprint.y;
            width = Mathf.Clamp(width, 1.2f, 9.5f);
            depth = Mathf.Clamp(depth, 1.2f, 9.5f);

            Transform deckRoot = new GameObject("LRP_AlwaysVisibleDeckAndRails").transform;
            deckRoot.SetParent(shipRoot, false);
            deckRoot.localPosition = Vector3.zero;
            deckRoot.localRotation = Quaternion.identity;
            deckRoot.localScale = Vector3.one;

            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "DeckSurface_ALWAYS_VISIBLE_TOP_AT_WORLD_Y0";
            deck.transform.SetParent(deckRoot, false);
            deck.transform.localPosition = new Vector3(0f, -0.035f, 0f);
            deck.transform.localScale = new Vector3(width, 0.07f, depth);
            SetColor(deck, new Color(0.62f, 0.45f, 0.24f));
            Collider deckCol = deck.GetComponent<Collider>();
            if (deckCol != null) deckCol.isTrigger = true;

            int plankCount = Mathf.Max(4, Mathf.RoundToInt(width / 0.32f));
            float plankW = width / plankCount;
            for (int i = 0; i < plankCount; i++)
            {
                GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = "DeckPlank_VISIBLE_" + i;
                plank.transform.SetParent(deckRoot, false);
                plank.transform.localPosition = new Vector3(-width * 0.5f + plankW * (i + 0.5f), 0.01f, 0f);
                plank.transform.localScale = new Vector3(Mathf.Max(0.05f, plankW - 0.02f), 0.012f, depth * 0.98f);
                SetColor(plank, i % 2 == 0 ? new Color(0.66f, 0.50f, 0.28f) : new Color(0.54f, 0.38f, 0.20f));
                Collider pc = plank.GetComponent<Collider>();
                if (pc != null) pc.isTrigger = true;
            }

            float hw = width * 0.5f;
            float hd = depth * 0.5f;
            CreateRailBeam(deckRoot, "RailFront", new Vector3(0f, 0.62f, hd), new Vector3(width, 0.12f, 0.08f));
            CreateRailBeam(deckRoot, "RailBack", new Vector3(0f, 0.62f, -hd), new Vector3(width, 0.12f, 0.08f));
            CreateRailBeam(deckRoot, "RailLeft", new Vector3(-hw, 0.62f, 0f), new Vector3(0.08f, 0.12f, depth));
            CreateRailBeam(deckRoot, "RailRight", new Vector3(hw, 0.62f, 0f), new Vector3(0.08f, 0.12f, depth));

            Debug.Log($"[LRP SurfaceDebug] Ensured visible deck/rails {width:F2}m x {depth:F2}m.");
        }

        private void CreateRailBeam(Transform parent, string name, Vector3 localPos, Vector3 localScale)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = name;
            rail.transform.SetParent(parent, false);
            rail.transform.localPosition = localPos;
            rail.transform.localScale = localScale;
            SetColor(rail, new Color(0.36f, 0.22f, 0.10f));
            Collider col = rail.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void RemoveNonFunctionalGeneratedProps()
        {
            if (shipRoot == null) return;
            string[] tokens = { "spyglass", "treasure", "chest", "repairbucket", "bucket", "oar", "doohicky", "debugoscill", "redwhite", "red_and_white", "cannonstarmid", "cannon_star_mid", "midbarrel", "smallbarrel" };
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
            Vector3 wheelPos = _stationaryDinghyLayout ? new Vector3(0f, 0.86f, 0.34f) : new Vector3(0f, 0.92f, 0.52f);
            Cube("SteeringPedestal", KeepInsidePlayableDeck(new Vector3(wheelPos.x, 0.54f, wheelPos.z)), new Vector3(0.12f, 0.62f, 0.12f), new Color(0.35f, 0.18f, 0.06f));
            _wheel = new GameObject("SteeringWheel_SPINS_WITH_GRABBABLE_KNOBS").transform;
            _wheel.SetParent(_debugRoot, false);
            _wheel.localPosition = KeepInsidePlayableDeck(wheelPos);
            _wheel.localRotation = Quaternion.identity;
            CylinderUnder(_wheel, "WheelHub", Vector3.zero, new Vector3(0.14f, 0.08f, 0.14f), new Color(0.42f, 0.22f, 0.08f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f + Mathf.PI * 0.5f;
                Vector3 knobPos = new Vector3(Mathf.Cos(a) * 0.70f, Mathf.Sin(a) * 0.70f, 0f);
                GameObject spoke = CubeUnder(_wheel, "WheelWoodBoardSpoke_TO_KNOB_" + i, knobPos * 0.5f, new Vector3(knobPos.magnitude, 0.045f, 0.045f), new Color(0.40f, 0.20f, 0.07f));
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                GameObject knob = SphereUnder(_wheel, "WheelKnob_GRAB_STEER_" + i, knobPos, Vector3.one * 0.14f, i == 0 ? new Color(1f, 0.72f, 0.12f) : new Color(0.58f, 0.32f, 0.10f));
                AddStationButton(knob, LrpDebugStationButton.Action.WheelKnob);
                if (i == 0) _goldKnob = knob.transform;
            }
        }

        private void BuildSailRig()
        {
            float z = _stationaryDinghyLayout ? 0.56f : 1.05f;
            float ropeX = _stationaryDinghyLayout ? -0.42f : -0.78f;
            Cube("TallCenteredMast", new Vector3(0f, 3.35f, z), new Vector3(0.13f, 6.70f, 0.13f), new Color(0.36f, 0.18f, 0.07f));
            Cube("HighYardArm", new Vector3(0f, 6.15f, z), new Vector3(1.80f, 0.08f, 0.08f), new Color(0.36f, 0.18f, 0.07f));
            // Sail is anchored at the TOP yard and drops DOWN as it opens.
            _sail = Cube("VariableSail_TOP_ANCHORED_DROPS_DOWN_OPEN", new Vector3(0f, 5.10f, z + 0.03f), new Vector3(1.45f, 0.10f, 0.035f), new Color(0.92f, 0.86f, 0.64f)).transform;
            // Main control rope is a visible loop: mast top -> pulley -> grabbable tail -> cleat.
            RopeBetween("SailRopeAttachedToMast_top_to_pulley", new Vector3(0f, 6.15f, z + 0.02f), new Vector3(ropeX, 4.65f, z + 0.10f), 0.022f);
            RopeBetween("SailRopeAttachedToMast_pulley_to_tail", new Vector3(ropeX, 4.65f, z + 0.10f), new Vector3(ropeX, 0.08f, z + 0.10f), 0.022f);
            Cube("SailPulleyBlock_touching_rope", new Vector3(ropeX, 4.65f, z + 0.10f), new Vector3(0.13f, 0.10f, 0.08f), new Color(0.36f, 0.18f, 0.07f));
            _sailRope = Cylinder("SailMainRope_HANDLE_PULL_DOWN_TO_RAISE", new Vector3(ropeX, 0.72f, z + 0.10f), new Vector3(0.045f, 0.26f, 0.045f), new Color(0.80f, 0.68f, 0.42f)).transform;
            AddStationButton(_sailRope.gameObject, LrpDebugStationButton.Action.SailRope);
            // Multiple hand-over-hand rope grips so the player can pull, release, grab higher, and pull again.
            for (int ropeGrip = 0; ropeGrip < 6; ropeGrip++)
            {
                float gy = Mathf.Lerp(0.18f, 1.55f, ropeGrip / 5f);
                GameObject grip = Cylinder("SailPulleyRopeGrip_HAND_OVER_HAND_" + ropeGrip, new Vector3(ropeX, gy, z + 0.10f), new Vector3(0.038f, 0.18f, 0.038f), new Color(0.86f, 0.75f, 0.50f));
                AddStationButton(grip, LrpDebugStationButton.Action.SailRope);
            }
            _cleat = Cube("SailCleat_TIE_ROPE_HERE_TO_HOLD_PERCENT", new Vector3(ropeX, 0.12f, z + 0.14f), new Vector3(0.32f, 0.08f, 0.10f), new Color(0.45f, 0.23f, 0.08f)).transform;
            AddStationButton(_cleat.gameObject, LrpDebugStationButton.Action.SailCleat);
            RopeBetween("SailRopeTail_to_cleat", new Vector3(ropeX, 0.08f, z + 0.10f), new Vector3(ropeX, 0.12f, z + 0.14f), 0.02f);
            Cube("PortRiggingTieBoard", new Vector3(-0.88f, 0.08f, z + 0.44f), new Vector3(0.36f, 0.08f, 0.08f), new Color(0.36f, 0.18f, 0.07f));
            Cube("StarboardRiggingTieBoard", new Vector3(0.88f, 0.08f, z + 0.44f), new Vector3(0.36f, 0.08f, 0.08f), new Color(0.36f, 0.18f, 0.07f));
            Cube("MastRiggingUpperBoard", new Vector3(0f, 1.92f, z + 0.05f), new Vector3(1.5f, 0.06f, 0.06f), new Color(0.36f, 0.18f, 0.07f));
            for (int i = 0; i < 5; i++) RopeBetween("SailRiggingRope_" + i, new Vector3(-0.7f + i * 0.35f, 1.90f, z + 0.05f), new Vector3(-0.9f + i * 0.45f, 0.08f, z + 0.45f), 0.018f);
            GameObject lantern = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lantern.name = "SailPercentCube_status_indicator";
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
            _anchor.localPosition = new Vector3(0f, 0.16f, -0.92f);
            _anchor.localRotation = Quaternion.identity;
            Vector3 anchorWheelPos = _stationaryDinghyLayout ? new Vector3(0.46f, 0.62f, -0.18f) : new Vector3(0.52f, 0.62f, -0.86f);
            _anchorWheel = Cylinder("HorizontalAnchorCapstan_GRAB_OR_R", anchorWheelPos, new Vector3(0.30f, 0.06f, 0.30f), new Color(0.18f, 0.18f, 0.20f)).transform;
            _anchorWheel.localRotation = Quaternion.identity;
            AddStationButton(_anchorWheel.gameObject, LrpDebugStationButton.Action.AnchorCapstan);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 2f / 4f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.34f, 0f, Mathf.Sin(a) * 0.34f);
                GameObject handle = CubeUnder(_anchorWheel, "AnchorCapstanSideHandle_GRAB_" + i, p, new Vector3(0.18f, 0.055f, 0.055f), new Color(0.35f, 0.22f, 0.10f));
                handle.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                AddStationButton(handle, LrpDebugStationButton.Action.AnchorCapstan);
            }
            _anchorChain = CylinderUnder(_anchor, "AnchorChain_SHRINKS_WHEN_RAISED", new Vector3(0f, 0.02f, 0f), new Vector3(0.035f, 0.10f, 0.035f), Color.gray).transform;
            _anchorHook = CubeUnder(_anchor, "AnchorHook", new Vector3(0f, -0.08f, 0f), new Vector3(0.26f, 0.22f, 0.08f), new Color(0.08f, 0.08f, 0.08f)).transform;
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "AnchorStatusCube_status_indicator";
            indicator.transform.SetParent(_debugRoot, false);
            indicator.transform.localPosition = _stationaryDinghyLayout ? new Vector3(0.54f, 0.82f, -0.18f) : new Vector3(0.85f, 0.82f, -0.86f);
            indicator.transform.localScale = Vector3.one * 0.09f;
            SetColor(indicator, Color.green);
            _anchorIndicator = indicator.GetComponent<Renderer>();
        }

        private void BuildCannonsAndAmmo()
        {
            bool tinyBoat = _stationaryDinghyLayout || _layoutScale <= 0.72f;
            Vector3 bowCannonPos = tinyBoat ? new Vector3(0f, 0.38f, 0.54f) : new Vector3(0f, 0.42f, 0.82f);
            AddCannon("BowCannon", bowCannonPos, Quaternion.identity);
            if (!tinyBoat)
            {
                AddCannon("PortCannon", new Vector3(-0.95f, 0.42f, 0f), Quaternion.Euler(0f, -90f, 0f));
                AddCannon("StarboardCannon", new Vector3(0.95f, 0.42f, 0f), Quaternion.Euler(0f, 90f, 0f));
                AddCannon("SternCannon", new Vector3(0f, 0.42f, -1.0f), Quaternion.Euler(0f, 180f, 0f));
            }
            BuildCannonballPile("AmmoPile_MAX_5_BALLS_RECYCLED", _stationaryDinghyLayout ? new Vector3(-0.36f, 0.18f, -0.30f) : new Vector3(-0.55f, 0.18f, -0.78f));
        }

        private void AddCannon(string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject pivot = new GameObject(name + "_manual_load_then_fuse");
            pivot.transform.SetParent(_debugRoot, false);
            pivot.transform.localPosition = KeepInsidePlayableDeck(localPosition);
            pivot.transform.localRotation = localRotation;
            pivot.transform.localScale = Vector3.one * (_layoutScale <= 0.72f ? 0.82f : 1.0f);
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = name + "_Barrel";
            barrel.transform.SetParent(pivot.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.12f, 0.25f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.16f, 0.46f, 0.16f);
            LrpPrimitiveMaterialLibrary.ApplyDarkMetal(barrel);
            Transform firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(pivot.transform, false);
            firePoint.localPosition = new Vector3(0f, 0.12f, 0.92f);
            Transform loadZone = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            loadZone.name = name + "_BreechLoadZone_release_ball_here";
            loadZone.SetParent(pivot.transform, false);
            loadZone.localPosition = new Vector3(0f, 0.16f, 0.16f);
            loadZone.localScale = Vector3.one * 0.24f;
            // Invisible load zone: keep the collider/interactable, remove the distracting blue ball visual.
            SetColor(loadZone.gameObject, new Color(1f, 0.76f, 0.18f, 0.10f));
            Renderer loadZoneRenderer = loadZone.GetComponent<Renderer>();
            if (loadZoneRenderer != null) loadZoneRenderer.enabled = false;
            AddStationButton(loadZone.gameObject, LrpDebugStationButton.Action.CannonLoadZone);
            GameObject fuse = CylinderUnder(pivot.transform, name + "_FusePullRope_FIRE_LOADED_ONLY", new Vector3(0.25f, 0.13f, 0.05f), new Vector3(0.035f, 0.24f, 0.035f), new Color(0.76f, 0.58f, 0.25f));
            AddStationButton(fuse, LrpDebugStationButton.Action.CannonFuse);
            GameObject light = GameObject.CreatePrimitive(PrimitiveType.Cube);
            light.name = name + "_LoadedCube_status_indicator";
            light.transform.SetParent(pivot.transform, false);
            light.transform.localPosition = new Vector3(-0.25f, 0.18f, 0.05f);
            light.transform.localScale = Vector3.one * 0.13f;
            SetColor(light, cannonsStartLoaded ? Color.green : new Color(0.18f,0.18f,0.18f));
            GameObject loadedBall = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            loadedBall.name = name + "_VisibleBallInBarrel_when_loaded";
            loadedBall.transform.SetParent(pivot.transform, false);
            loadedBall.transform.localPosition = new Vector3(0f, 0.09f, 0.51f);
            loadedBall.transform.localScale = Vector3.one * 0.13f;
            LrpPrimitiveMaterialLibrary.ApplyCannonballMetal(loadedBall);
            loadedBall.SetActive(cannonsStartLoaded);
            _cannons.Add(new PrimitiveCannon { root = pivot.transform, firePoint = firePoint, loadZone = loadZone, loaded = cannonsStartLoaded, loadedIndicator = light.GetComponent<Renderer>(), loadedBall = loadedBall.transform });
        }

        private void BuildCannonballPile(string name, Vector3 localPosition)
        {
            Transform pile = new GameObject(name).transform;
            pile.SetParent(_debugRoot, false);
            pile.localPosition = KeepInsidePlayableDeck(localPosition);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                bool top = i == 6;
                GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = "InfinitePileVisualSphere_grab_source_" + i;
                ball.transform.SetParent(pile, false);
                ball.transform.localPosition = top ? new Vector3(0f, 0.115f, 0f) : new Vector3(Mathf.Cos(a) * 0.15f, 0.035f, Mathf.Sin(a) * 0.12f);
                ball.transform.localScale = Vector3.one * 0.13f;
                LrpPrimitiveMaterialLibrary.ApplyCannonballMetal(ball);
                AddStationButton(ball, LrpDebugStationButton.Action.AmmoSource);
            }
            GameObject source = CubeUnder(pile, "AMMO_SOURCE_GRAB_B_SPAWNS_AND_LOADS_NEAREST", new Vector3(0f, -0.13f, 0f), new Vector3(0.55f, 0.06f, 0.40f), new Color(0.10f, 0.16f, 0.10f));
            AddStationButton(source, LrpDebugStationButton.Action.AmmoSource);
        }

        private void BuildRepairToolsAndLeaks()
        {
            // v63: real primitive hammer proportions. Root is the wooden grip, so hand pose directly drives it.
            GameObject hammer = Cube("PhysicalRepairHammer_WOOD_HANDLE_GRAB", _stationaryDinghyLayout ? new Vector3(0.42f, 0.34f, -0.42f) : new Vector3(0.48f, 0.34f, -0.82f), new Vector3(0.075f, 0.075f, 0.42f), new Color(0.65f, 0.36f, 0.16f));
            GameObject head = CubeUnder(hammer.transform, "RepairHammerHead_UNTINTED_METAL_HIT_END", new Vector3(0f, 0.00f, 0.245f), new Vector3(0.34f, 0.16f, 0.13f), new Color(0.78f, 0.78f, 0.80f));
            LrpPrimitiveMaterialLibrary.ApplyUntintedMetal(head);
            Rigidbody hammerBody = hammer.AddComponent<Rigidbody>();
            hammerBody.mass = 0.32f;
            hammerBody.useGravity = true;
            hammerBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            AddStationButton(hammer, LrpDebugStationButton.Action.RepairHammer);
            Collider hammerCollider = hammer.GetComponent<Collider>();
            if (hammerCollider != null) hammerCollider.isTrigger = false;
            Rigidbody hammerRbAfterButton = hammer.GetComponent<Rigidbody>();
            if (hammerRbAfterButton != null)
            {
                hammerRbAfterButton.isKinematic = false;
                hammerRbAfterButton.useGravity = true;
                hammerRbAfterButton.constraints = RigidbodyConstraints.None;
            }
            LrpRespawnIfLost hammerRespawn = hammer.AddComponent<LrpRespawnIfLost>();
            hammerRespawn.respawnBelowY = -0.75f;
            hammerRespawn.respawnDelay = 0.4f;
            _healthIndicator = Cube("SHIP_HEALTH_INDICATOR_PHYSICAL_BAR", _stationaryDinghyLayout ? new Vector3(0f, 0.22f, -0.30f) : new Vector3(0f, 0.22f, -0.18f), new Vector3(0.90f, 0.08f, 0.04f), Color.green).GetComponent<Renderer>();
            Vector3[] positions = { new Vector3(-0.65f,0.20f,0.62f), new Vector3(0.70f,0.20f,0.28f), new Vector3(-0.18f,0.20f,-0.66f), new Vector3(0.25f,0.20f,0.82f) };
            int count = Mathf.Clamp(leakCount, 0, positions.Length);
            for (int i = 0; i < count; i++)
            {
                Transform leak = new GameObject("VisibleTopDeckLeak_WATER_SPRAY_REPAIR_" + i).transform;
                leak.SetParent(_debugRoot, false);
                leak.localPosition = positions[i];
                GameObject hole = CubeUnder(leak, "LeakHole", Vector3.zero, new Vector3(0.22f,0.035f,0.22f), Color.black);
                AddStationButton(hole, LrpDebugStationButton.Action.RepairLeak);
                GameObject spray = CylinderUnder(leak, "LeakWaterSpray", new Vector3(0f,0.26f,0f), new Vector3(0.045f,0.38f,0.045f), new Color(0.35f,0.70f,1f,0.85f));
                _leaks.Add(leak);
                _leakSprays.Add(spray.GetComponent<Renderer>());
                _leakHammerHits[leak] = 0;
            }
        }



        private void BuildWindControllerAndSheetControls()
        {
            if (windController == null)
            {
                windController = _debugRoot.gameObject.AddComponent<LrpWindController>();
            }
            windController.sandbox = this;
            windController.ocean = ocean;
            windController.waterOne = waterOne;

            float z = _stationaryDinghyLayout ? 0.30f : 0.52f;
            // Sheets are now rope controls like the main sail rope: several hand-over-hand
            // grabbable rope segments per side, plus a visible line to the yard.
            for (int i = 0; i < 5; i++)
            {
                float y = Mathf.Lerp(0.25f, 1.15f, i / 4f);
                GameObject port = Cylinder("PortSheetRopeGrip_HAND_OVER_HAND_" + i, new Vector3(-0.72f, y, z), new Vector3(0.04f, 0.18f, 0.04f), new Color(0.82f, 0.70f, 0.45f));
                AddStationButton(port, LrpDebugStationButton.Action.PortSheet);
                _sheetGripSegments.Add(port.transform);

                GameObject star = Cylinder("StarboardSheetRopeGrip_HAND_OVER_HAND_" + i, new Vector3(0.72f, y, z), new Vector3(0.04f, 0.18f, 0.04f), new Color(0.82f, 0.70f, 0.45f));
                AddStationButton(star, LrpDebugStationButton.Action.StarboardSheet);
                _sheetGripSegments.Add(star.transform);
            }

            RopeBetween("PortSheet_rope_to_yard", new Vector3(-0.72f, 0.25f, z), new Vector3(-0.55f, 5.15f, 1.08f), 0.018f);
            RopeBetween("StarboardSheet_rope_to_yard", new Vector3(0.72f, 0.25f, z), new Vector3(0.55f, 5.15f, 1.08f), 0.018f);
        }

        private void BuildTinyInstructionSigns()
        {
            SmallWoodSign("Sign_Wheel", "GRAB KNOB\nTURN WHEEL", _stationaryDinghyLayout ? new Vector3(0.28f, 0.44f, 0.26f) : new Vector3(0.42f, 0.42f, -0.72f));
            SmallWoodSign("Sign_Sail", "PULL ROPE\nTIE CLEAT", _stationaryDinghyLayout ? new Vector3(-0.24f, 0.46f, -0.02f) : new Vector3(-0.35f, 0.46f, 0.14f));
            SmallWoodSign("Sign_Anchor", "ANCHOR\nSTOPS", _stationaryDinghyLayout ? new Vector3(0.28f, 0.42f, 0.06f) : new Vector3(0.36f, 0.42f, 0.88f));
            SmallWoodSign("Sign_Cannon", "BALL→BREECH\nPULL FUSE", _stationaryDinghyLayout ? new Vector3(0.30f, 0.30f, 0.52f) : new Vector3(0.42f, 0.30f, 0.38f));
            SmallWoodSign("Sign_Sheets", "SHEETS TRIM\nSAIL ANGLE", _stationaryDinghyLayout ? new Vector3(0f, 0.54f, 0.12f) : new Vector3(0f, 0.54f, 0.72f));
            SmallWoodSign("Sign_Wind", "WATCH WIND\nMATCH SAIL", _stationaryDinghyLayout ? new Vector3(0f, 0.70f, -0.45f) : new Vector3(0f, 0.70f, -1.10f));
        }

        private GameObject SmallWoodSign(string name, string text, Vector3 localPosition)
        {
            GameObject back = Cube(name + "_board", localPosition, new Vector3(0.52f, 0.28f, 0.035f), new Color(0.42f, 0.22f, 0.08f));
            GameObject label = new GameObject(name + "_small_text");
            label.transform.SetParent(back.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.52f);
            label.transform.localRotation = Quaternion.identity;
            TextMesh tm = label.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 26;
            tm.characterSize = 0.026f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.black;
            Renderer textRenderer = label.GetComponent<Renderer>();
            if (textRenderer != null)
            {
                textRenderer.sharedMaterial = CreateDepthSafeTextMaterial(tm);
                textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
                textRenderer.sortingOrder = 0;
            }
            LrpSignTextDepthGuard guard = label.AddComponent<LrpSignTextDepthGuard>();
            guard.boardRoot = back.transform;
            return back;
        }


        private Material CreateDepthSafeTextMaterial(TextMesh text)
        {
            Shader shader = Shader.Find("Unlit/Transparent Cutout");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            if (text != null && text.font != null && text.font.material != null && mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", text.font.material.mainTexture);

            Color ink = new Color(0.035f, 0.025f, 0.015f, 1f);
            mat.color = ink;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", ink);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", ink);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.35f);
            mat.renderQueue = 2450;
            if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 1);
            return mat;
        }

        private void UpdateContextualInteractionIndicators()
        {
            // Cannon load zones should guide you ONLY while holding a cannonball. Empty hands should not see them.
            // The sail cleat should guide you ONLY while holding a sail rope.
            for (int i = LrpXrInteractableBridge.All.Count - 1; i >= 0; i--)
            {
                LrpXrInteractableBridge bridge = LrpXrInteractableBridge.All[i];
                if (bridge == null || bridge.button == null) continue;

                bool force = false;
                Color color = new Color(1f, 0.92f, 0.15f, 0.30f);
                switch (bridge.button.action)
                {
                    case LrpDebugStationButton.Action.CannonLoadZone:
                        bridge.showHoverIndicator = AnyHandHoldingCannonball();
                        force = AnyHandHoldingCannonball();
                        color = new Color(0.20f, 1f, 0.25f, 0.28f);
                        break;
                    case LrpDebugStationButton.Action.SailCleat:
                        bridge.showHoverIndicator = _sailRopeHeld;
                        force = _sailRopeHeld;
                        color = new Color(1f, 0.92f, 0.15f, 0.32f);
                        break;
                    default:
                        bridge.showHoverIndicator = true;
                        break;
                }
                bridge.forceShowIndicator = force;
                bridge.forcedIndicatorColor = color;
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
            _floaterOceanPositions[go.transform] = WorldToOcean2(go.transform.position);
        }

        private void MoveFloatersWithWaterTravel()
        {
            if (_floaters.Count == 0) return;

            // Ground-truth model:
            // 1) Each debris object owns a fixed ocean-space coordinate.
            // 2) The virtual ship position advances through that ocean-space.
            // 3) Rendered world position is oceanCoord - virtualShipOceanPosition, rotated by -heading.
            // Therefore debris does NOT rotate with the ship and does NOT get manually shoved by a fake conveyor.
            Vector2 shipOcean = GetVirtualShipOceanPosition();
            float heading = _headingDegrees;
            Vector3 center = shipRoot != null ? shipRoot.position : Vector3.zero;
            float half = Mathf.Max(8f, debrisRecyclerHalfRange);
            float full = half * 2f;

            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                Transform f = _floaters[i];
                if (f == null)
                {
                    _floaters.RemoveAt(i);
                    continue;
                }

                if (f.parent != null) f.SetParent(null, true);

                SurfaceFloatFollower follower = f.GetComponent<SurfaceFloatFollower>();
                if (follower != null) follower.moveWithWaterConveyor = false;

                if (!_floaterOceanPositions.TryGetValue(f, out Vector2 oceanPos))
                {
                    oceanPos = WorldToOcean2(f.position);
                    _floaterOceanPositions[f] = oceanPos;
                }

                // Recycle in ship/ocean-relative coordinates, not by world-space parent rotation.
                Vector2 rel = oceanPos - shipOcean;
                while (rel.x > half) { rel.x -= full; oceanPos.x -= full; }
                while (rel.x < -half) { rel.x += full; oceanPos.x += full; }
                while (rel.y > half) { rel.y -= full; oceanPos.y -= full; }
                while (rel.y < -half) { rel.y += full; oceanPos.y += full; }
                _floaterOceanPositions[f] = oceanPos;

                Vector3 p = OceanToWorld(oceanPos, center, heading, shipOcean);
                f.position = p;
            }
        }

        private Vector2 GetVirtualShipOceanPosition()
        {
            if (waterOne != null)
            {
                WaterOneGrid3x3 grid = waterOne.GetComponent<WaterOneGrid3x3>();
                if (grid != null) return grid.virtualShipOceanPosition;
            }
            return Vector2.zero;
        }

        private Vector2 WorldToOcean2(Vector3 world)
        {
            Vector3 center = shipRoot != null ? shipRoot.position : Vector3.zero;
            Vector3 relWorld = world - center;
            // Render is R(-heading) * oceanRel, so inverse is R(+heading).
            Vector3 relOcean = Quaternion.Euler(0f, _headingDegrees, 0f) * relWorld;
            Vector2 shipOcean = GetVirtualShipOceanPosition();
            return shipOcean + new Vector2(relOcean.x, relOcean.z);
        }

        private static Vector3 OceanToWorld(Vector2 oceanPos, Vector3 center, float headingDegrees, Vector2 shipOcean)
        {
            Vector2 rel = oceanPos - shipOcean;
            Vector3 relWorld = Quaternion.Euler(0f, -headingDegrees, 0f) * new Vector3(rel.x, 0f, rel.y);
            return center + relWorld;
        }

        private void AnimateStations()
        {
            if (_wheel != null)
            {
                _wheel.localRotation = Quaternion.Euler(0f, 0f, _wheelTurns * 360f);
            }
            if (_sail != null)
            {
                float pct = Mathf.Clamp01(sailPercent);
                const float topY = 6.05f;
                const float fullHeight = 2.10f;
                const float minHeight = 0.08f;
                float sailHeight = Mathf.Lerp(minHeight, fullHeight, pct);
                Vector3 s = _sail.localScale;
                s.x = 1.45f;
                s.y = sailHeight;
                _sail.localScale = Vector3.Lerp(_sail.localScale, s, Time.deltaTime * 6f);
                Vector3 sailTarget = _sail.localPosition;
                sailTarget.x = 0f;
                // Top stays at the yard; the lower edge drops downward as sailPercent opens.
                sailTarget.y = topY - sailHeight * 0.5f;
                _sail.localPosition = Vector3.Lerp(_sail.localPosition, sailTarget, Time.deltaTime * 6f);
                _sail.localRotation = Quaternion.Lerp(_sail.localRotation, Quaternion.Euler(0f, sailSheetAngleDegrees, 0f), Time.deltaTime * 4f);
            }
            if (_sailRope != null)
            {
                Vector3 p = _sailRope.localPosition;
                p.y = Mathf.Lerp(0.18f, 1.05f, 1f - sailPercent);
                _sailRope.localPosition = Vector3.Lerp(_sailRope.localPosition, p, Time.deltaTime * 5f);
            }
            if (_anchor != null)
            {
                _anchor.localPosition = Vector3.Lerp(_anchor.localPosition, new Vector3(0f, 0.16f, 1.08f), Time.deltaTime * 4f);
            }
            if (_anchorChain != null)
            {
                float chainScale = Mathf.Lerp(0.06f, 0.65f, _anchorPercent);
                _anchorChain.localScale = Vector3.Lerp(_anchorChain.localScale, new Vector3(0.035f, chainScale, 0.035f), Time.deltaTime * 6f);
                _anchorChain.localPosition = Vector3.Lerp(_anchorChain.localPosition, new Vector3(0f, Mathf.Lerp(0.08f, -0.28f, _anchorPercent), 0f), Time.deltaTime * 6f);
            }
            if (_anchorHook != null)
            {
                _anchorHook.localPosition = Vector3.Lerp(_anchorHook.localPosition, new Vector3(0f, Mathf.Lerp(0.02f, -0.92f, _anchorPercent), 0f), Time.deltaTime * 6f);
            }
            if (_anchorWheel != null)
            {
                _anchorWheel.localRotation = Quaternion.Euler(0f, -_anchorPercent * 220f, 0f);
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
                int hits = _leakHammerHits.ContainsKey(leak) ? _leakHammerHits[leak] : 0;
                float remaining = Mathf.Clamp01(1f - (hits / (float)HammerHitsRequiredPerLeak));
                float pulse = 0.75f + Mathf.Sin(Time.time * 8f + i) * 0.25f;
                leak.localScale = new Vector3(Mathf.Lerp(0.35f, 1f, remaining), Mathf.Lerp(0.20f, pulse, remaining), Mathf.Lerp(0.35f, 1f, remaining));
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
            LrpPrimitiveMaterialLibrary.ApplyCannonballMetal(ball);
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


        public void BeginPhysicalInteraction(LrpDebugStationButton.Action action, Transform handle, Vector3 handWorld)
        {
            BeginPhysicalInteraction(action, handle, handWorld, 0);
        }

        public void BeginPhysicalInteraction(LrpDebugStationButton.Action action, Transform handle, Vector3 handWorld, int handId)
        {
            BeginPhysicalInteraction(action, handle, handWorld, handle != null ? handle.rotation : Quaternion.identity, handId);
        }

        public void BeginPhysicalInteraction(LrpDebugStationButton.Action action, Transform handle, Vector3 handWorld, Quaternion handRotation, int handId)
        {
            PhysicalHandGrabState h = HandStateFor(handId);
            h.action = action;
            h.handle = handle;
            h.startWorld = handWorld;
            h.lastWorld = handWorld;
            h.velocityWorld = Vector3.zero;
            h.handRotation = handRotation;
            h.fuseFired = false;

            switch (action)
            {
                case LrpDebugStationButton.Action.WheelKnob:
                    _wheelDriverHand = Mathf.Clamp(handId, 0, 1);
                    h.startWheelAngle = WheelHandAngle(handWorld);
                    h.startWheelTurns = _wheelTurns;
                    break;
                case LrpDebugStationButton.Action.SailRope:
                    _sailDriverHand = Mathf.Clamp(handId, 0, 1);
                    _sailRopeHeld = true;
                    h.startSailPercent = sailPercent;
                    h.startSailY = (_debugRoot != null ? _debugRoot.InverseTransformPoint(handWorld).y : handWorld.y);
                    break;
                case LrpDebugStationButton.Action.AnchorCapstan:
                    _anchorDriverHand = Mathf.Clamp(handId, 0, 1);
                    h.startAnchorAngle = AnchorHandAngle(handWorld);
                    h.startAnchorPercent = _anchorPercent;
                    break;
                case LrpDebugStationButton.Action.PortSheet:
                case LrpDebugStationButton.Action.StarboardSheet:
                    h.startSailY = (_debugRoot != null ? _debugRoot.InverseTransformPoint(handWorld).x : handWorld.x);
                    h.startSailPercent = sailSheetAngleDegrees;
                    break;
                case LrpDebugStationButton.Action.AmmoSource:
                    h.heldCannonball = SpawnHeldCannonball(handWorld);
                    break;
                case LrpDebugStationButton.Action.RepairHammer:
                    h.heldHammer = handle != null ? handle.gameObject : null;
                    _heldHammer = h.heldHammer;
                    _hammerCurrentlyInsideLeak.Clear();
                    if (h.heldHammer != null)
                    {
                        h.heldHammer.transform.position = handWorld;
                        h.heldHammer.transform.rotation = handRotation * Quaternion.Euler(hammerGripRotationOffsetEuler);
                        Rigidbody rb = h.heldHammer.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.isKinematic = true;
                            rb.useGravity = false;
                            rb.velocity = Vector3.zero;
                            rb.angularVelocity = Vector3.zero;
                        }
                    }
                    break;
                case LrpDebugStationButton.Action.RepairLeak:
                    // v63: leaks repair only from repeated hammer ENTER events, not from selecting/touching the leak.
                    break;
            }
        }

        public void UpdatePhysicalInteraction(LrpDebugStationButton.Action action, Transform handle, Vector3 handWorld)
        {
            UpdatePhysicalInteraction(action, handle, handWorld, 0);
        }

        public void UpdatePhysicalInteraction(LrpDebugStationButton.Action action, Transform handle, Vector3 handWorld, int handId)
        {
            UpdatePhysicalInteraction(action, handle, handWorld, handle != null ? handle.rotation : Quaternion.identity, handId);
        }

        public void UpdatePhysicalInteraction(LrpDebugStationButton.Action action, Transform handle, Vector3 handWorld, Quaternion handRotation, int handId)
        {
            PhysicalHandGrabState h = HandStateFor(handId);
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            h.velocityWorld = (handWorld - h.lastWorld) / dt;
            h.lastWorld = handWorld;
            h.handRotation = handRotation;

            switch (action)
            {
                case LrpDebugStationButton.Action.WheelKnob:
                {
                    if (_wheelDriverHand != Mathf.Clamp(handId, 0, 1)) break;
                    float current = WheelHandAngle(handWorld);
                    float delta = Mathf.DeltaAngle(h.startWheelAngle, current) / 360f;
                    // Clockwise hand movement should rotate the wheel clockwise.
                    _wheelTurns = Mathf.Clamp(h.startWheelTurns - delta, -maxWheelTurns, maxWheelTurns);
                    break;
                }
                case LrpDebugStationButton.Action.SailRope:
                {
                    if (_sailDriverHand != Mathf.Clamp(handId, 0, 1)) break;
                    float y = _debugRoot != null ? _debugRoot.InverseTransformPoint(handWorld).y : handWorld.y;
                    float pullDown = h.startSailY - y;
                    // Pulling down raises/reefs the lower yard; letting go un-cleated drops it open/full.
                    sailPercent = Mathf.Clamp01(h.startSailPercent - pullDown * 1.15f);
                    break;
                }
                case LrpDebugStationButton.Action.AnchorCapstan:
                {
                    if (_anchorDriverHand != Mathf.Clamp(handId, 0, 1)) break;
                    float current = AnchorHandAngle(handWorld);
                    float delta = Mathf.DeltaAngle(h.startAnchorAngle, current) / 220f;
                    _anchorPercent = Mathf.Clamp01(h.startAnchorPercent - delta);
                    _anchorLowered = _anchorPercent >= 0.5f;
                    break;
                }
                case LrpDebugStationButton.Action.PortSheet:
                case LrpDebugStationButton.Action.StarboardSheet:
                {
                    float x = _debugRoot != null ? _debugRoot.InverseTransformPoint(handWorld).x : handWorld.x;
                    float pullAcross = x - h.startSailY;
                    float sign = action == LrpDebugStationButton.Action.PortSheet ? -1f : 1f;
                    sailSheetAngleDegrees = Mathf.Clamp(h.startSailPercent + pullAcross * sheetAdjustDegreesPerMeter * sign, -70f, 70f);
                    break;
                }
                case LrpDebugStationButton.Action.CannonFuse:
                {
                    if (!h.fuseFired && Vector3.Distance(h.startWorld, handWorld) > 0.24f)
                    {
                        h.fuseFired = true;
                        FireNearestCannonPublic(handle != null ? handle.position : handWorld);
                    }
                    break;
                }
                case LrpDebugStationButton.Action.AmmoSource:
                case LrpDebugStationButton.Action.CannonLoadZone:
                    if (h.heldCannonball != null) h.heldCannonball.transform.position = handWorld;
                    break;
                case LrpDebugStationButton.Action.RepairHammer:
                    if (h.heldHammer != null)
                    {
                        h.heldHammer.transform.position = handWorld;
                        // Only the controller/hand rotation drives the hammer while held.
                        // Do not LookRotation toward velocity; that caused the wild spin/spasm.
                        h.heldHammer.transform.rotation = handRotation * Quaternion.Euler(hammerGripRotationOffsetEuler);
                        TryHammerLeakEnterHits(h.heldHammer);
                    }
                    break;
            }
        }

        public void EndPhysicalInteraction(LrpDebugStationButton.Action action, Transform handle, Vector3 handWorld)
        {
            EndPhysicalInteraction(action, handle, handWorld, 0);
        }

        public void EndPhysicalInteraction(LrpDebugStationButton.Action action, Transform handle, Vector3 handWorld, int handId)
        {
            PhysicalHandGrabState h = HandStateFor(handId);
            switch (action)
            {
                case LrpDebugStationButton.Action.SailRope:
                    if (_sailDriverHand == Mathf.Clamp(handId, 0, 1)) _sailDriverHand = -1;
                    // Only fall if no other hand is still holding the rope and it was not released at the cleat.
                    h.handle = null;
                    if (!AnyHandHoldingSailRope() && (_cleat == null || Vector3.Distance(handWorld, _cleat.position) > 0.34f))
                        sailPercent = 1f;
                    _sailRopeHeld = AnyHandHoldingSailRope();
                    break;
                case LrpDebugStationButton.Action.AmmoSource:
                case LrpDebugStationButton.Action.CannonLoadZone:
                    FinishHeldCannonball(h, handWorld);
                    break;
                case LrpDebugStationButton.Action.RepairHammer:
                    DropHeldHammer(h);
                    h.heldHammer = null;
                    break;
                case LrpDebugStationButton.Action.WheelKnob:
                    if (_wheelDriverHand == Mathf.Clamp(handId, 0, 1)) _wheelDriverHand = -1;
                    break;
                case LrpDebugStationButton.Action.AnchorCapstan:
                    if (_anchorDriverHand == Mathf.Clamp(handId, 0, 1)) _anchorDriverHand = -1;
                    break;
            }

            h.handle = null;
            h.fuseFired = false;
        }

        private float WheelHandAngle(Vector3 handWorld)
        {
            if (_wheel == null) return 0f;
            // IMPORTANT: sample in the ship/debug-root space, not wheel local space.
            // Wheel local space rotates with the wheel and causes runaway/spin feedback.
            Vector3 hand = _debugRoot != null ? _debugRoot.InverseTransformPoint(handWorld) : handWorld;
            Vector3 center = _debugRoot != null ? _debugRoot.InverseTransformPoint(_wheel.position) : _wheel.position;
            Vector3 d = hand - center;
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        private float AnchorHandAngle(Vector3 handWorld)
        {
            if (_anchorWheel == null) return 0f;
            Vector3 hand = _debugRoot != null ? _debugRoot.InverseTransformPoint(handWorld) : handWorld;
            Vector3 center = _debugRoot != null ? _debugRoot.InverseTransformPoint(_anchorWheel.position) : _anchorWheel.position;
            Vector3 d = hand - center;
            return Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
        }

        private GameObject SpawnHeldCannonball(Vector3 handWorld)
        {
            PruneManualCannonballs();
            while (_manualCannonballs.Count >= MaxManualCannonballs)
            {
                GameObject oldest = _manualCannonballs[0];
                _manualCannonballs.RemoveAt(0);
                if (oldest != null) Destroy(oldest);
            }

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "HeldManualCannonball_INDEPENDENT_PER_HAND_MAX5";
            ball.transform.position = handWorld;
            ball.transform.localScale = Vector3.one * 0.105f;
            LrpPrimitiveMaterialLibrary.ApplyCannonballMetal(ball);
            Collider col = ball.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            _manualCannonballs.Add(ball);
            return ball;
        }

        private void PruneManualCannonballs()
        {
            for (int i = _manualCannonballs.Count - 1; i >= 0; i--)
                if (_manualCannonballs[i] == null) _manualCannonballs.RemoveAt(i);
        }

        private void FinishHeldCannonball(PhysicalHandGrabState h, Vector3 handWorld)
        {
            GameObject held = h != null ? h.heldCannonball : null;
            if (held == null) return;
            PrimitiveCannon best = null;
            float bestD = 0.42f * 0.42f;
            for (int i = 0; i < _cannons.Count; i++)
            {
                PrimitiveCannon c = _cannons[i];
                if (c == null || c.loaded || c.loadZone == null) continue;
                float d = (c.loadZone.position - handWorld).sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }

            if (best != null)
            {
                best.loaded = true;
                SetCannonLoadedVisual(best, true);
                // Loaded state is shown physically by the barrel ball/lantern; no green flash.
                _manualCannonballs.Remove(held);
                Destroy(held);
            }
            else
            {
                Collider col = held.GetComponent<Collider>();
                if (col != null) col.isTrigger = false;
                Rigidbody rb = held.AddComponent<Rigidbody>();
                rb.mass = 0.35f;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                rb.velocity = h != null ? h.velocityWorld : Vector3.zero;
                Vector3 v = h != null ? h.velocityWorld : Vector3.zero;
                rb.angularVelocity = new Vector3(v.z, v.x, -v.y) * 0.6f;
                Destroy(held, 6f);
            }
            if (h != null) h.heldCannonball = null;
        }

        private void TryHammerLeakEnterHits(GameObject hammer)
        {
            if (hammer == null) return;

            Transform head = hammer.transform.Find("RepairHammerHead_UNTINTED_METAL_HIT_END");
            Vector3 hitPoint = head != null ? head.position : hammer.transform.position;
            HashSet<Transform> insideNow = new HashSet<Transform>();

            for (int i = _leaks.Count - 1; i >= 0; i--)
            {
                Transform leak = _leaks[i];
                if (leak == null)
                {
                    _leaks.RemoveAt(i);
                    continue;
                }

                bool inside = Vector3.Distance(leak.position, hitPoint) <= HammerLeakHitRadius;
                if (!inside) continue;

                insideNow.Add(leak);
                if (_hammerCurrentlyInsideLeak.Contains(leak)) continue;

                RegisterHammerLeakHit(leak);
            }

            _hammerCurrentlyInsideLeak.Clear();
            foreach (Transform leak in insideNow)
                if (leak != null) _hammerCurrentlyInsideLeak.Add(leak);
        }

        private void RegisterHammerLeakHit(Transform leak)
        {
            if (leak == null) return;
            int hits = _leakHammerHits.ContainsKey(leak) ? _leakHammerHits[leak] : 0;
            hits++;
            _leakHammerHits[leak] = hits;

            float remaining = Mathf.Clamp01(1f - (hits / (float)HammerHitsRequiredPerLeak));
            leak.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, remaining);

            if (hits >= HammerHitsRequiredPerLeak)
            {
                _hammerCurrentlyInsideLeak.Remove(leak);
                _leakHammerHits.Remove(leak);
                leak.gameObject.SetActive(false);
                _leaks.Remove(leak);
                shipHealth = Mathf.Min(100f, shipHealth + 12f);
            }
        }

        private void DropHeldHammer(PhysicalHandGrabState h = null)
        {
            GameObject hammer = h != null && h.heldHammer != null ? h.heldHammer : _heldHammer;
            if (hammer == null) return;
            _hammerCurrentlyInsideLeak.Clear();
            Collider[] cols = hammer.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].isTrigger = false;
            }
            Rigidbody rb = hammer.GetComponent<Rigidbody>();
            if (rb == null) rb = hammer.AddComponent<Rigidbody>();
            rb.mass = 0.25f;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Vector3 v = h != null ? h.velocityWorld : Vector3.zero;
            rb.velocity = v;
            rb.angularVelocity = new Vector3(v.z, v.x, -v.y) * 0.8f;
            if (_heldHammer == hammer) _heldHammer = null;
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

            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                // XR rays often ignore trigger-only colliders depending on interactor settings.
                // Keep station colliders solid and kinematic so the red laser can select them.
                c.isTrigger = true;
            }

            Rigidbody rb = go.GetComponent<Rigidbody>(); if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false; rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            LrpDebugStationButton b = go.GetComponent<LrpDebugStationButton>(); if (b == null) b = go.AddComponent<LrpDebugStationButton>();
            b.action = action; b.sandbox = this;

            LrpXrInteractableBridge bridge = go.GetComponent<LrpXrInteractableBridge>();
            if (bridge == null) bridge = go.AddComponent<LrpXrInteractableBridge>();
            bridge.button = b;
            bridge.invokeOnSelect = false;
            bridge.invokeOnActivate = false;
            bridge.continuousWhileSelected = action == LrpDebugStationButton.Action.WheelKnob
                || action == LrpDebugStationButton.Action.SailRope
                || action == LrpDebugStationButton.Action.AnchorCapstan
                || action == LrpDebugStationButton.Action.CannonFuse
                || action == LrpDebugStationButton.Action.RepairHammer
                || action == LrpDebugStationButton.Action.AmmoSource
                || action == LrpDebugStationButton.Action.CannonLoadZone;
            bridge.continuousInterval = 0.06f;

            TryAddXrSimpleInteractable(go);
        }

        private static void TryAddXrSimpleInteractable(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRBaseInteractable>() != null) return;
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
            LrpPrimitiveMaterialLibrary.Apply(go, color);
        }

        private static void ClearChild(Transform parent, string name)
        {
            Transform child = parent.Find(name); if (child == null) return;
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }

        private void OnGUI()
        {
            return; // Debug overlay removed; use physical ship signs only.
            if (!showOverlay) return;
            GUI.Box(new Rect(12f, 12f, 520f, 190f),
                "LRP Manual Debug\n" +
                "A/D steer wheel, wheel slowly returns; W/S or Q/E adjusts sail; T full/reef; R anchor\n" +
                "B/load zones load nearest cannon; SPACE/fuse fires nearest loaded cannon; H repair\n" +
                $"Sail {sailPercent:P0} Sheet {sailSheetAngleDegrees:F0}° Wind x{lastWindSpeedMultiplier:F2} Align {lastWindAlignment:F2} Trim {lastSailTrimEfficiency:F2}\n" +
                $"Anchor {(_anchorLowered ? "DOWN/STOP" : "UP")} WheelTurns {_wheelTurns:F2} Heading {_headingDegrees:F0}\n" +
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
