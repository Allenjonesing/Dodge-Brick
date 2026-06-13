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
        [Tooltip("Default hides the bulky IMGUI debug menu. Keyboard controls still work; press L to show it.")]
        public bool startWithOverlayHidden = true;

        [Header("VR Layout Polish")]
        [Tooltip("Base primitive station footprint before auto-fit. Code scales it down for small boats and slightly up for big boats.")]
        public Vector2 baseStationFootprint = new Vector2(2.85f, 3.15f);
        public float largeShipStationScale = 1.15f;
        public float smallShipMinimumStationScale = 0.48f;
        public float railContentPadding = 0.28f;

        [Header("Water Escalator Travel")]
        public bool waterTravelEnabled = false;
        public float waterTravelSpeed = 0.8f;
        public Vector2 waterTravelDirection = new Vector2(0f, 1f);
        [Range(0f, 1f)] public float sailPercent = 0.35f;
        public float sailFullSpeedMultiplier = 3f;
        public float debrisRecyclerHalfRange = 35f;
        public int extraDebrisCount = 45;
        [Tooltip("When true, WASD/arrow keys steer the Water1 tile conveyor instead of moving the ship.")]
        public bool useKeyboardForWaterTravel = true;

        [Header("Cannons")]
        public bool autoFireCannons = false;
        public float autoFireInterval = 4f;
        public float cannonballSpeed = 12f;
        public float cannonballLifetime = 6f;
        public bool cannonsStartLoaded = false;

        [Header("Ship Health / Repair")]
        public float shipHealth = 100f;
        public float leakDamagePerSecond = 1.5f;
        public int leakCount = 3;

        [Header("Debug UI")]
        public bool showOverlay = true;

        private readonly List<PrimitiveCannon> _cannons = new List<PrimitiveCannon>();
        private readonly List<Transform> _floaters = new List<Transform>();
        private Transform _debugRoot;
        private Transform _sail;
        private Transform _anchor;
        private Transform _wheel;
        private Transform _anchorWheelVisual;
        private Transform _sailRopeVisual;
        private Renderer _sailIndicator;
        private Renderer _anchorIndicator;
        private readonly List<Transform> _leaks = new List<Transform>();
        private readonly List<Renderer> _leakRenderers = new List<Renderer>();
        private Renderer _healthIndicator;
        private Vector3 _shipStartPosition;
        private Quaternion _shipStartRotation;
        private Vector3 _shipStartScale;
        private bool _sailDropped = true;
        private bool _anchorLowered;
        private float _layoutScale = 1f;
        private float _autoFireTimer;

        private void Start()
        {
            ResolveReferences();
            CaptureShipLock();

            if (startWithOverlayHidden) showOverlay = false;

            if (buildOnStart)
            {
                BuildOrRepair();
            }

            Debug.Log("[LRP SurfaceDebug] Active. SPACE fire, B/load ammo, C auto-fire, T sail full/reef, Q/E sail %, R anchor, H repair, L overlay, 1-6 wave presets. Anchor up + sail >0% auto-travels.");
        }

        private void Update()
        {
            ResolveReferences();

            if (Input.GetKeyDown(KeyCode.L)) showOverlay = !showOverlay;
            if (Input.GetKeyDown(KeyCode.Space)) FireAllCannons();
            if (Input.GetKeyDown(KeyCode.B)) LoadAllCannonsPublic();
            if (Input.GetKeyDown(KeyCode.C)) autoFireCannons = !autoFireCannons;
            if (Input.GetKeyDown(KeyCode.T)) ToggleSail();
            if (Input.GetKey(KeyCode.Q)) AdjustSailPercent(-Time.deltaTime * 0.55f);
            if (Input.GetKey(KeyCode.E)) AdjustSailPercent(Time.deltaTime * 0.55f);
            if (Input.GetKeyDown(KeyCode.R)) ToggleAnchor();
            if (Input.GetKeyDown(KeyCode.H)) RepairLeaksPublic();

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
            UpdateLeaksAndHealth();
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
            _layoutScale = ComputeVrLayoutScale();
            _debugRoot.localScale = Vector3.one * _layoutScale;

            _cannons.Clear();
            _floaters.Clear();

            RemoveNonFunctionalGeneratedProps();
            BuildWheel();
            BuildSail();
            BuildAnchor();
            BuildCannonsAndAmmo();
            BuildRepairToolsAndLeaks();
            BuildFloatingSurfaceProps();
            DisableOldEnemyRaftsButKeepWaterTwo();
            ApplyWaterEscalatorSettings();

            Debug.Log($"[LRP SurfaceDebug] Built polished VR stations. layoutScale={_layoutScale:F2}; controls stay active, debug overlay hidden by default (L toggles).");
        }

        public void ToggleSail()
        {
            // Toggle between reefed/raised and fully dropped. The sail is also variable via Q/E or the rope controls.
            sailPercent = sailPercent > 0.05f ? 0f : 1f;
            _sailDropped = sailPercent > 0.05f;
        }

        public void AdjustSailPercent(float delta)
        {
            sailPercent = Mathf.Clamp01(sailPercent + delta);
            _sailDropped = sailPercent > 0.05f;
        }

        public void ToggleAnchor()
        {
            _anchorLowered = !_anchorLowered;
        }

        public void FireAllCannonsPublic()
        {
            FireAllCannons();
        }

        public void LoadAllCannonsPublic()
        {
            for (int i = 0; i < _cannons.Count; i++)
            {
                _cannons[i].loaded = true;
                SetCannonLoadedVisual(_cannons[i], true);
            }
            Debug.Log("[LRP SurfaceDebug] Cannons loaded. Grab/fire lever or press SPACE.");
        }

        public void RepairLeaksPublic()
        {
            shipHealth = Mathf.Min(100f, shipHealth + 25f);
            if (_leaks.Count > 0)
            {
                int removeIndex = _leaks.Count - 1;
                Transform leak = _leaks[removeIndex];
                _leaks.RemoveAt(removeIndex);
                if (removeIndex < _leakRenderers.Count) _leakRenderers.RemoveAt(removeIndex);
                if (leak != null) Destroy(leak.gameObject);
            }
            Debug.Log($"[LRP SurfaceDebug] Hammer repair used. Health={shipHealth:F0}, leaks={_leaks.Count}");
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

            if (input.sqrMagnitude > 0.0001f)
            {
                waterTravelDirection = input;
            }

            // Movement is sail-driven now: if anchor is up and sail is down even partially,
            // the water conveyor moves automatically. WASD/arrows only steer the travel direction.
            waterTravelEnabled = !_anchorLowered && sailPercent > 0.01f;
        }

        private float EffectiveTravelSpeed()
        {
            if (_anchorLowered) return 0f;
            return waterTravelSpeed * Mathf.Lerp(0f, sailFullSpeedMultiplier, Mathf.Clamp01(sailPercent));
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

        private float ComputeVrLayoutScale()
        {
            float usableW = 2.5f;
            float usableD = 2.5f;
            BoundaryShipGenerator generator = shipRoot != null ? shipRoot.GetComponentInParent<BoundaryShipGenerator>() : FindObjectOfType<BoundaryShipGenerator>();
            if (generator != null)
            {
                usableW = Mathf.Max(0.5f, generator.UsableWidth);
                usableD = Mathf.Max(0.5f, generator.UsableDepth);
            }

            float insideW = Mathf.Max(0.35f, usableW - railContentPadding * 2f);
            float insideD = Mathf.Max(0.35f, usableD - railContentPadding * 2f);
            float fitW = insideW / Mathf.Max(0.1f, baseStationFootprint.x);
            float fitD = insideD / Mathf.Max(0.1f, baseStationFootprint.y);
            float fit = Mathf.Min(fitW, fitD);

            // Large ships get slightly bigger/readable VR primitives; small ships shrink instead of breaking rails.
            float largeBias = Mathf.InverseLerp(2.2f, 4.5f, Mathf.Min(usableW, usableD));
            float desired = Mathf.Lerp(1.0f, largeShipStationScale, largeBias);
            float result = Mathf.Min(desired, fit);
            return Mathf.Clamp(result, smallShipMinimumStationScale, largeShipStationScale);
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
            _wheel = Cylinder("DebugSteeringWheel_WITH_KNOBS", new Vector3(0f, 0.92f, -0.72f), new Vector3(0.35f, 0.055f, 0.35f), new Color(0.45f, 0.23f, 0.08f)).transform;
            _wheel.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Primitive polish: spokes and hand knobs make the wheel readable in VR.
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector3 knobPos = new Vector3(Mathf.Cos(a) * 0.30f, 0f, Mathf.Sin(a) * 0.30f);
                GameObject knob = SphereUnder(_wheel, "WheelKnob_" + i, knobPos, Vector3.one * 0.075f, new Color(0.58f, 0.32f, 0.10f));
                knob.transform.localRotation = Quaternion.identity;

                GameObject spoke = CubeUnder(_wheel, "WheelSpoke_" + i, knobPos * 0.5f, new Vector3(0.035f, 0.035f, 0.32f), new Color(0.40f, 0.20f, 0.07f));
                spoke.transform.localRotation = Quaternion.Euler(0f, -i * 45f, 0f);
            }

            GameObject fireLever = Cylinder("FireCannonsLever_GRAB_OR_SPACE", new Vector3(0.55f, 0.52f, -0.74f), new Vector3(0.055f, 0.22f, 0.055f), new Color(0.75f, 0.10f, 0.08f));
            fireLever.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            AddStationButton(fireLever, LrpDebugStationButton.Action.FireCannons);
            Cube("SIGN_FIRE_SPACE_OR_GRAB", new Vector3(0.55f, 0.27f, -0.74f), new Vector3(0.62f, 0.16f, 0.035f), new Color(0.08f, 0.10f, 0.12f));
            Label("FIRE\nloaded only", new Vector3(0.55f, 0.33f, -0.70f), 0.08f);
        }

        private void BuildSail()
        {
            // Behind the steering wheel, high enough to be visible over the primitive deck.
            float z = -1.42f;
            Cube("DebugMast_BEHIND_WHEEL", new Vector3(0f, 1.15f, z), new Vector3(0.12f, 2.1f, 0.12f), new Color(0.36f, 0.18f, 0.07f));
            Cube("DebugYardArm", new Vector3(0f, 1.82f, z), new Vector3(1.55f, 0.08f, 0.08f), new Color(0.36f, 0.18f, 0.07f));
            _sail = Cube("DebugVariableSail_PERCENT_CONTROLS_SPEED", new Vector3(0f, 1.33f, z + 0.03f), new Vector3(1.20f, 1.05f, 0.035f), new Color(0.92f, 0.86f, 0.64f)).transform;

            _sailRopeVisual = Cylinder("SailRope_GRAB_MAIN_TOGGLE", new Vector3(-0.86f, 0.92f, z + 0.10f), new Vector3(0.035f, 0.62f, 0.035f), new Color(0.80f, 0.68f, 0.42f)).transform;
            AddStationButton(_sailRopeVisual.gameObject, LrpDebugStationButton.Action.ToggleSail);

            GameObject raiseRope = Cylinder("SailRaiseRope_GRAB_OR_E", new Vector3(-1.08f, 0.86f, z + 0.10f), new Vector3(0.03f, 0.48f, 0.03f), new Color(0.86f, 0.75f, 0.50f));
            AddStationButton(raiseRope, LrpDebugStationButton.Action.RaiseSail);
            GameObject lowerRope = Cylinder("SailLowerRope_GRAB_OR_Q", new Vector3(-0.64f, 0.86f, z + 0.10f), new Vector3(0.03f, 0.48f, 0.03f), new Color(0.86f, 0.75f, 0.50f));
            AddStationButton(lowerRope, LrpDebugStationButton.Action.LowerSail);

            for (int i = 0; i < 4; i++)
            {
                float x = -0.55f + i * 0.36f;
                GameObject rope = Cylinder("SailVerticalRiggingRope_" + i, new Vector3(x, 1.18f, z + 0.09f), new Vector3(0.015f, 0.78f, 0.015f), new Color(0.80f, 0.68f, 0.42f));
                rope.transform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -8f : 8f);
            }

            GameObject portStay = Cylinder("SailStayRope_Port", new Vector3(-0.50f, 1.10f, z + 0.08f), new Vector3(0.02f, 0.92f, 0.02f), new Color(0.80f, 0.68f, 0.42f));
            portStay.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            GameObject starStay = Cylinder("SailStayRope_Starboard", new Vector3(0.50f, 1.10f, z + 0.08f), new Vector3(0.02f, 0.92f, 0.02f), new Color(0.80f, 0.68f, 0.42f));
            starStay.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);

            // Extra rigging attaches visibly from mast/yard toward the rail area so ropes no longer look detached.
            RopeBetween("Rigging_To_PortRail_Front", new Vector3(-0.62f, 1.78f, z + 0.04f), new Vector3(-1.12f, 0.62f, -0.20f), 0.018f);
            RopeBetween("Rigging_To_StarRail_Front", new Vector3(0.62f, 1.78f, z + 0.04f), new Vector3(1.12f, 0.62f, -0.20f), 0.018f);
            RopeBetween("Rigging_To_PortRail_Back", new Vector3(-0.42f, 1.70f, z + 0.04f), new Vector3(-1.03f, 0.62f, -1.18f), 0.018f);
            RopeBetween("Rigging_To_StarRail_Back", new Vector3(0.42f, 1.70f, z + 0.04f), new Vector3(1.03f, 0.62f, -1.18f), 0.018f);

            GameObject indicator = Cube("SAIL_STATUS_INDICATOR_PERCENT", new Vector3(-1.35f, 0.47f, z + 0.10f), new Vector3(0.14f, 0.14f, 0.04f), Color.green);
            _sailIndicator = indicator.GetComponent<Renderer>();
            Cube("SIGN_SAIL_QE_T_GRAB_VARIABLE_SPEED", new Vector3(-0.86f, 0.46f, z + 0.10f), new Vector3(1.05f, 0.18f, 0.035f), new Color(0.08f, 0.10f, 0.12f));
            Label("SAIL %\nQ/E or ropes", new Vector3(-0.86f, 0.54f, z + 0.14f), 0.075f);
        }

        private void BuildAnchor()
        {
            _anchor = new GameObject("DebugAnchorAssembly_ANCHOR_DOWN_STOPS_MOVEMENT").transform;
            _anchor.SetParent(_debugRoot, false);
            _anchor.localPosition = new Vector3(0f, 0.16f, 1.08f);
            _anchor.localRotation = Quaternion.identity;

            _anchorWheelVisual = Cylinder("AnchorWheel_GRAB_OR_R_STOP", new Vector3(0.72f, 0.62f, 0.98f), new Vector3(0.28f, 0.05f, 0.28f), new Color(0.18f, 0.18f, 0.20f)).transform;
            _anchorWheelVisual.localRotation = Quaternion.Euler(90f, 0f, 0f);
            AddStationButton(_anchorWheelVisual.gameObject, LrpDebugStationButton.Action.ToggleAnchor);

            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                Vector3 knobPos = new Vector3(Mathf.Cos(a) * 0.23f, 0f, Mathf.Sin(a) * 0.23f);
                SphereUnder(_anchorWheelVisual, "AnchorWheelKnob_" + i, knobPos, Vector3.one * 0.07f, new Color(0.35f, 0.35f, 0.38f));
            }

            GameObject indicator = Cube("ANCHOR_STATUS_INDICATOR_RED_DOWN", new Vector3(1.13f, 0.29f, 0.98f), new Vector3(0.14f, 0.14f, 0.04f), Color.green);
            _anchorIndicator = indicator.GetComponent<Renderer>();
            Cube("SIGN_ANCHOR_R_OR_GRAB_STOPS", new Vector3(0.72f, 0.28f, 0.98f), new Vector3(0.82f, 0.18f, 0.035f), new Color(0.08f, 0.10f, 0.12f));
            Label("ANCHOR\nR = stop", new Vector3(0.72f, 0.36f, 1.02f), 0.075f);

            CubeUnder(_anchor, "DebugAnchorChain", new Vector3(0f, 0.25f, 0f), new Vector3(0.035f, 0.55f, 0.035f), Color.gray);
            CubeUnder(_anchor, "DebugAnimatedAnchor", new Vector3(0f, -0.08f, 0f), new Vector3(0.24f, 0.24f, 0.08f), new Color(0.08f, 0.08f, 0.08f));
            CubeUnder(_anchor, "DebugAnchorFlukeLeft", new Vector3(-0.13f, -0.20f, 0f), new Vector3(0.18f, 0.07f, 0.08f), new Color(0.08f, 0.08f, 0.08f));
            CubeUnder(_anchor, "DebugAnchorFlukeRight", new Vector3(0.13f, -0.20f, 0f), new Vector3(0.18f, 0.07f, 0.08f), new Color(0.08f, 0.08f, 0.08f));
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
            pivot.transform.localScale = Vector3.one * 1.18f; // readable in VR, still below 2x even on large layouts

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

            GameObject trigger = CubeUnder(pivot.transform, name + "_FIRE_GRAB_TRIGGER", new Vector3(0.25f, 0.12f, 0.05f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.75f, 0.10f, 0.08f));
            AddStationButton(trigger, LrpDebugStationButton.Action.FireCannons);

            GameObject loadedLight = CubeUnder(pivot.transform, name + "_LOADED_INDICATOR", new Vector3(-0.25f, 0.18f, 0.05f), new Vector3(0.10f, 0.10f, 0.10f), cannonsStartLoaded ? Color.green : Color.red);

            _cannons.Add(new PrimitiveCannon { root = pivot.transform, firePoint = firePoint, loaded = cannonsStartLoaded, loadedIndicator = loadedLight.GetComponent<Renderer>() });
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
                ball.name = "LoadableCannonball_GRAB_TO_LOAD_" + i;
                ball.transform.SetParent(pile, false);
                ball.transform.localPosition = offsets[i];
                ball.transform.localScale = Vector3.one * 0.13f;
                SetColor(ball, Color.black);
                AddStationButton(ball, LrpDebugStationButton.Action.LoadCannons);
            }

            GameObject loadBox = CubeUnder(pile, "LOAD_CANNONS_GRAB_ZONE_B", new Vector3(0f, -0.13f, 0f), new Vector3(0.55f, 0.06f, 0.40f), new Color(0.10f, 0.16f, 0.10f));
            AddStationButton(loadBox, LrpDebugStationButton.Action.LoadCannons);
            Label("AMMO\ngrab/load", pile.localPosition + new Vector3(0f, 0.20f, 0f), 0.065f);
        }


        private void BuildRepairToolsAndLeaks()
        {
            _leaks.Clear();
            _leakRenderers.Clear();

            GameObject hammer = Cube("RepairHammer_GRAB_OR_H", new Vector3(-0.42f, 0.36f, -0.18f), new Vector3(0.12f, 0.12f, 0.55f), new Color(0.45f, 0.25f, 0.10f));
            CubeUnder(hammer.transform, "RepairHammerHead", new Vector3(0f, 0.10f, 0.24f), new Vector3(0.30f, 0.18f, 0.16f), new Color(0.45f, 0.45f, 0.48f));
            AddStationButton(hammer, LrpDebugStationButton.Action.RepairLeaks);
            Cube("SIGN_HAMMER_REPAIR_H_OR_GRAB", new Vector3(-0.42f, 0.18f, -0.18f), new Vector3(0.78f, 0.15f, 0.035f), new Color(0.08f, 0.10f, 0.12f));
            Label("HAMMER\nrepair leaks", new Vector3(-0.42f, 0.25f, -0.14f), 0.07f);

            _healthIndicator = Cube("SHIP_HEALTH_INDICATOR", new Vector3(0f, 0.22f, -0.18f), new Vector3(0.90f, 0.08f, 0.04f), Color.green).GetComponent<Renderer>();

            Vector3[] leakPositions =
            {
                new Vector3(-0.65f, 0.20f, 0.62f),
                new Vector3(0.70f, 0.20f, 0.28f),
                new Vector3(-0.18f, 0.20f, -0.66f),
                new Vector3(0.25f, 0.20f, 0.82f)
            };

            int count = Mathf.Clamp(leakCount, 0, leakPositions.Length);
            for (int i = 0; i < count; i++)
            {
                Transform leak = new GameObject("VisibleTopDeckLeak_WATER_SPRAY_REPAIR_" + i).transform;
                leak.SetParent(_debugRoot, false);
                leak.localPosition = leakPositions[i];
                leak.localRotation = Quaternion.identity;

                GameObject hole = CubeUnder(leak, "LeakHole", Vector3.zero, new Vector3(0.22f, 0.035f, 0.22f), Color.black);
                AddStationButton(hole, LrpDebugStationButton.Action.RepairLeaks);
                GameObject spray = CylinderUnder(leak, "LeakWaterSpray", new Vector3(0f, 0.26f, 0f), new Vector3(0.045f, 0.38f, 0.045f), new Color(0.35f, 0.70f, 1f, 0.85f));
                _leaks.Add(leak);
                Renderer r = spray.GetComponent<Renderer>();
                if (r != null) _leakRenderers.Add(r);
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
            if (!cannon.loaded)
            {
                SpawnDryClick(cannon.firePoint.position);
                return;
            }
            cannon.loaded = false;
            SetCannonLoadedVisual(cannon, false);

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


        private void SetCannonLoadedVisual(PrimitiveCannon cannon, bool loaded)
        {
            if (cannon == null || cannon.loadedIndicator == null) return;
            cannon.loadedIndicator.material.color = loaded ? Color.green : Color.red;
        }

        private void SpawnDryClick(Vector3 position)
        {
            GameObject click = GameObject.CreatePrimitive(PrimitiveType.Cube);
            click.name = "CannonNeedsAmmoIndicator";
            click.transform.position = position + Vector3.up * 0.15f;
            click.transform.localScale = Vector3.one * 0.16f;
            SetColor(click, Color.red);
            BoomPulse pulse = click.AddComponent<BoomPulse>();
            pulse.duration = 0.45f;
            pulse.maxScale = 0.35f;
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
                float pct = Mathf.Clamp01(sailPercent);
                float targetScaleY = Mathf.Lerp(0.08f, 1.05f, pct);
                Vector3 s = _sail.localScale;
                s.y = Mathf.Lerp(s.y, targetScaleY, Time.deltaTime * 5f);
                _sail.localScale = s;

                Vector3 targetPosition = new Vector3(0f, Mathf.Lerp(1.82f, 1.33f, pct), -1.39f);
                _sail.localPosition = Vector3.Lerp(_sail.localPosition, targetPosition, Time.deltaTime * 5f);
            }

            if (_sailRopeVisual != null)
            {
                Vector3 s = _sailRopeVisual.localScale;
                s.y = Mathf.Lerp(s.y, Mathf.Lerp(0.38f, 0.72f, Mathf.Clamp01(sailPercent)), Time.deltaTime * 5f);
                _sailRopeVisual.localScale = s;
            }

            if (_anchor != null)
            {
                Vector3 target = new Vector3(0f, _anchorLowered ? -0.78f : 0.16f, 1.08f);
                _anchor.localPosition = Vector3.Lerp(_anchor.localPosition, target, Time.deltaTime * 4f);
            }

            if (_anchorWheelVisual != null)
            {
                float targetAngle = _anchorLowered ? 220f : 0f;
                _anchorWheelVisual.localRotation = Quaternion.Lerp(_anchorWheelVisual.localRotation, Quaternion.Euler(90f, 0f, targetAngle), Time.deltaTime * 5f);
            }

            if (_sailIndicator != null)
            {
                _sailIndicator.material.color = Color.Lerp(Color.yellow, Color.green, Mathf.Clamp01(sailPercent));
            }

            if (_anchorIndicator != null)
            {
                _anchorIndicator.material.color = _anchorLowered ? Color.red : Color.green;
            }
        }


        private void UpdateLeaksAndHealth()
        {
            int activeLeaks = 0;
            for (int i = _leaks.Count - 1; i >= 0; i--)
            {
                Transform leak = _leaks[i];
                if (leak == null)
                {
                    _leaks.RemoveAt(i);
                    continue;
                }
                activeLeaks++;
                float pulse = 0.75f + Mathf.Sin(Time.time * 8f + i) * 0.25f;
                leak.localScale = new Vector3(1f, pulse, 1f);
            }

            if (activeLeaks > 0)
            {
                shipHealth = Mathf.Max(0f, shipHealth - activeLeaks * leakDamagePerSecond * Time.deltaTime);
            }

            if (_healthIndicator != null)
            {
                float t = Mathf.Clamp01(shipHealth / 100f);
                _healthIndicator.transform.localScale = new Vector3(Mathf.Lerp(0.08f, 0.90f, t), 0.08f, 0.04f);
                _healthIndicator.material.color = Color.Lerp(Color.red, Color.green, t);
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


        private GameObject RopeBetween(string name, Vector3 fromLocal, Vector3 toLocal, float thickness)
        {
            Vector3 delta = toLocal - fromLocal;
            float length = delta.magnitude;
            if (length < 0.001f) return null;
            GameObject rope = Cube(name, fromLocal + delta * 0.5f, new Vector3(thickness, thickness, length), new Color(0.80f, 0.68f, 0.42f));
            rope.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            return rope;
        }

        private GameObject Label(string text, Vector3 localPosition, float size)
        {
            GameObject go = new GameObject("LABEL_" + text.Replace("\n", "_"));
            go.transform.SetParent(_debugRoot, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 48;
            tm.characterSize = size;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = new Material(Shader.Find("Sprites/Default"));
                r.material.color = Color.white;
            }
            return go;
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


        private GameObject CylinderUnder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            SetColor(go, color);
            return go;
        }

        private GameObject SphereUnder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
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
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            LrpDebugStationButton button = go.GetComponent<LrpDebugStationButton>();
            if (button == null) button = go.AddComponent<LrpDebugStationButton>();
            button.action = action;
            button.sandbox = this;
            TryAddXrSimpleInteractable(go);
        }

        private static void TryAddXrSimpleInteractable(GameObject go)
        {
            if (go == null) return;
            // Reflection keeps this compiling whether the project uses old XRIT, new XRIT, or only Oculus hands.
            string[] typeNames =
            {
                "UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, Unity.XR.Interaction.Toolkit",
                "UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable, Unity.XR.Interaction.Toolkit"
            };

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

            GUI.Box(new Rect(12f, 12f, 590f, 360f),
                "Living Room Pirates Debug\n\n" +
                "WASD/Arrows: move Water1 tiles under locked ship\n" +
                "1-6: wave presets\n" +
                "SPACE/fire lever: fire LOADED cannonballs + boom\n" +
                "B or grab ammo pile: load cannons | C: toggle auto fire\n" +
                "T main rope full/reef | Q/E or side ropes: variable sail speed\n" +
                "R or turn anchor wheel: toggle anchor, lowered = stop\n" +
                "H or grab hammer: repair leaks\n" +
                "Anchor up + sail >0% = automatic travel\n" +
                "L: toggle this menu\n\n" +
                $"Cannons: {_cannons.Count}  Floating props: {_floaters.Count}\n" +
                $"Sail: {sailPercent:P0}  Anchor: {(_anchorLowered ? "Lowered" : "Raised")} Health:{shipHealth:F0} Leaks:{_leaks.Count}\n" +
                $"AutoFire: {(autoFireCannons ? "ON" : "OFF")}  Travel: {(waterTravelEnabled ? "ON" : "OFF")} base:{waterTravelSpeed:F1} effective:{EffectiveTravelSpeed():F1} dir:{waterTravelDirection.x:F1},{waterTravelDirection.y:F1}\n" +
                $"WaterY@Ship: {CurrentWaterYAtShip():F2}\n" +
                wave + "\n" +
                "Ship transform: LOCKED | Ocean snap: 8 hull samples");
        }

        private sealed class PrimitiveCannon
        {
            public Transform root;
            public Transform firePoint;
            public bool loaded;
            public Renderer loadedIndicator;
        }
    }
}
