using UnityEngine;
using UnityEngine.SceneManagement;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Zero-prefab scene installer for the current Lobby scene.
    /// Drop this script anywhere under Assets and it will create/repair the
    /// Living Room Pirates runtime scene from primitives when Play starts.
    ///
    /// It intentionally keeps the player ship locked in place. Only ocean visuals,
    /// debug enemies, cannonballs, sails/anchor/wheel visuals, and water roots move.
    /// </summary>
    public sealed class LivingRoomPiratesPrimitiveSceneInstaller : MonoBehaviour
    {
        public const string RootName = "LivingRoomPiratesRoot";
        public const string ShipRootName = "shipGeneratedRoot";
        public const string BoundaryDebugRootName = "boundaryDebugRoot";
        public const string OceanRootName = "OceanWorldRoot";
        public const string WaterOneName = "Water1";
        public const string WaterTwoName = "Water2";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstallAfterSceneLoad()
        {
            // This is deliberately broad for editor testing. The installer is idempotent;
            // it will reuse existing objects when present.
            InstallOrRepairScene();
        }

        [ContextMenu("Install / Repair Primitive LRP Scene")]
        public void InstallFromInspector()
        {
            InstallOrRepairScene();
        }

        public static void InstallOrRepairScene()
        {
            GameObject root = FindOrCreate(RootName);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Transform shipRoot = FindOrCreateChild(root.transform, ShipRootName).transform;
            Transform boundaryDebugRoot = FindOrCreateChild(root.transform, BoundaryDebugRootName).transform;
            Transform oceanRoot = FindOrCreateChild(root.transform, OceanRootName).transform;

            // IMPORTANT: Water1 and Water2 are scene-authored objects now.
            // Do NOT create replacement water planes here. Place objects named exactly
            // "Water1" and "Water2" in the scene, then this installer only repairs
            // components/settings on those existing objects.
            GameObject waterOne = FindSceneObject(WaterOneName);
            if (waterOne == null)
            {
                Debug.LogError("[LRP PrimitiveSceneInstaller] Missing scene object named Water1. Place your Water1 plane in the scene. The installer will not create one anymore.");
                return;
            }

            if (!waterOne.transform.IsChildOf(oceanRoot))
            {
                waterOne.transform.SetParent(oceanRoot, true);
            }
            ConfigureWaterTile(waterOne, true);

            GameObject waterTwo = FindSceneObject(WaterTwoName);
            if (waterTwo != null)
            {
                if (!waterTwo.transform.IsChildOf(oceanRoot))
                {
                    waterTwo.transform.SetParent(oceanRoot, true);
                }
                waterTwo.SetActive(true);
                ConfigureWaterTwo(waterTwo, GetRendererColor(waterOne, new Color(0.05f, 0.38f, 0.58f, 0.82f)));
            }
            else
            {
                Debug.LogWarning("[LRP PrimitiveSceneInstaller] Missing scene object named Water2. Background ocean disabled. Place your Water2 plane in the scene if desired.");
            }

            DisableLegacyDebugObjectsAndControls();

            OceanWorldController ocean = Ensure<OceanWorldController>(root);
            ocean.oceanWorldRoot = oceanRoot;
            ocean.waterOne = waterOne.transform;
            ocean.waterTwo = waterTwo != null ? waterTwo.transform : null;
            AuthoritativeOceanSurface surface = Ensure<AuthoritativeOceanSurface>(waterOne);
            surface.deformer = waterOne.GetComponent<OceanWaveMeshDeformer>();
            surface.surfaceOffset = -0.05f;
            surface.snapWaterRootInstantly = true;
            surface.drawDebugContact = true;
            ocean.authoritativeSurface = surface;
            // Do not move OceanWorldRoot for travel. The generated Water1 tiles recycle individually.
            ocean.enableWaterEscalatorTravel = false;
            ocean.waterEscalatorSpeed = 0.8f;
            ocean.waterEscalatorDirection = new Vector2(0f, 1f);
            ocean.visualShipRoot = shipRoot;
            // LockedShipOceanSnapper is now the single Phase-1 ship/ocean snap solver.
            // Leave OceanWorldController as the sampler for debris/cannonballs, but do
            // not let its old bounds-based snap fight the dedicated solver.
            ocean.closeWaterOneGapToShip = false;
            ocean.sampleFromWaterOneDeformer = true;
            ocean.sampleFromRenderedWaterOneMesh = false;
            ocean.renderedSurfaceSearchRadius = 80f;
            ocean.useVisualShipBoundsBottomAsWaterOneTarget = true;
            ocean.useShipFootprintSamplesForWaterOne = false;
            ocean.waterOneFollowStrength = 0f;
            ocean.waterOneHeightOffset = 0f;
            ocean.shipToOceanSnapHeightOffset = -0.05f;
            ocean.enabled = true;

            BoundaryShipGenerator generator = Ensure<BoundaryShipGenerator>(root);
            generator.shipGeneratedRoot = shipRoot;
            generator.boundaryDebugRoot = boundaryDebugRoot;
#if UNITY_EDITOR
            generator.editorFallbackWidth = UnityEngine.Random.Range(0.5f, 10f);
            generator.editorFallbackDepth = UnityEngine.Random.Range(0.5f, 10f);
            generator.randomizeEditorFallbackBoundary = true;
#else
            // On Quest stationary/no-roomscale Guardian, generate the small supportable raft/dinghy
            // instead of randomly creating an oversized ship that cannot fit.
            generator.editorFallbackWidth = 0.8f;
            generator.editorFallbackDepth = 0.8f;
            generator.randomizeEditorFallbackBoundary = false;
#endif
            generator.regenerateOnStart = false;
            generator.stationPrefabScaleMultiplier = 2.0f;
            generator.forceEnableSpawnedStations = true;
            // Leave prefab references null. BoundaryShipGenerator now builds good primitive
            // placeholders itself when no real prefabs are assigned.
            generator.deckTilePrefab = null;
            generator.railingStraightPrefab = null;
            generator.railingCornerPrefab = null;
            generator.steeringWheelPrefab = null;
            generator.cannonForwardPrefab = null;
            generator.cannonSidePrefab = null;
            generator.anchorLeverPrefab = null;
            generator.sailRopePrefab = null;
            generator.spyglassPrefab = null;
            generator.oarPrefab = null;
            generator.repairBucketPrefab = null;
            generator.ammoCratePrefab = null;
            generator.treasureChestPrefab = null;
            generator.mastSmallPrefab = null;
            generator.enabled = true;

            Ensure<ShipStormMotionController>(root).enabled = true;

            LivingRoomPiratesEditorAutoActivator activator = root.GetComponent<LivingRoomPiratesEditorAutoActivator>();
            if (activator != null)
            {
                activator.enabled = false;
            }

            DemoRuntimeBuilder builder = Ensure<DemoRuntimeBuilder>(root);
            builder.SetShipRoot(shipRoot);
            builder.enabled = true;

            // The older keyboard station-focusing helper can move/rotate the ship with WASD/Q/E.
            // This game must keep the ship physically locked, so disable it and use the
            // primitive sandbox controls instead.
            LivingRoomPiratesShipKeyboardDebugControls keyboard = root.GetComponent<LivingRoomPiratesShipKeyboardDebugControls>();
            if (keyboard != null) keyboard.enabled = false;

            LivingRoomPiratesPrimitiveDebugSandbox oldSandbox = root.GetComponent<LivingRoomPiratesPrimitiveDebugSandbox>();
            if (oldSandbox != null) oldSandbox.enabled = false;

            LivingRoomPiratesSurfaceDebugSandbox sandbox = Ensure<LivingRoomPiratesSurfaceDebugSandbox>(root);
            sandbox.shipRoot = shipRoot;
            sandbox.waterOne = waterOne.transform;
            sandbox.ocean = ocean;
            sandbox.lockShipTransform = true;
            sandbox.waterTravelEnabled = false;
            sandbox.waterTravelSpeed = 0.8f;
            sandbox.debrisRecyclerHalfRange = 120f;
            sandbox.extraDebrisCount = 45;
            sandbox.autoFireCannons = false;
            sandbox.enabled = true;

            DisablePhotonInScene();

            LrpPrimitivePerformanceOptimizer optimizer = Ensure<LrpPrimitivePerformanceOptimizer>(root);
            optimizer.optimizeOnStart = true;
            optimizer.keepScanningBriefly = false;
            optimizer.enabled = true;

            LrpVrPhysicsSanitizer physicsSanitizer = Ensure<LrpVrPhysicsSanitizer>(root);
            physicsSanitizer.runOnStart = true;
            physicsSanitizer.keepScanningBriefly = true;
            physicsSanitizer.enabled = true;

            LrpXrRigAutoConfigurator xrRig = Ensure<LrpXrRigAutoConfigurator>(root);
            xrRig.configureOnStart = true;
            xrRig.keepScanningBriefly = true;
            xrRig.directGrabRadius = 0.18f;
            // Keep the old XRI repair conservative, then use LrpVrHandInteractionFallback
            // for actual grip/trigger station use. This matches the working XR starter
            // rig pattern without mutating controller input-action assets.
            xrRig.addDirectInteractorToHands = false;
            xrRig.addRayInteractorIfMissing = false;
            xrRig.addHandTriggerCollider = false;
            xrRig.addHandRigidbody = false;
            xrRig.keepScanningBriefly = false;
            xrRig.enabled = true;

            LrpVrHandInteractionFallback handFallback = Ensure<LrpVrHandInteractionFallback>(root);
            handFallback.enableVrHands = true;
            handFallback.directGrabRadius = 0.32f;
            handFallback.rescanInterval = 0.75f;
            handFallback.enabled = true;

            LrpOceanMotionVisuals motionVisuals = Ensure<LrpOceanMotionVisuals>(root);
            motionVisuals.shipRoot = shipRoot;
            motionVisuals.waterOne = waterOne.transform;
            motionVisuals.ocean = ocean;
            motionVisuals.windLineCount = 18;
            motionVisuals.enabled = true;

            // In Play mode Start() will also regenerate. In edit-time context menu this gives instant visuals.
            if (Application.isPlaying)
            {
                generator.RegenerateShip();
                AddPrimitiveBehavioursToGeneratedStations(shipRoot);
                CreateOrUpdateShipBottomContact(shipRoot, ocean);
            }

            ShipOceanSnapTargetMaintainer snapMaintainer = root.GetComponent<ShipOceanSnapTargetMaintainer>();
            if (snapMaintainer != null) snapMaintainer.enabled = false;

            LockedShipOceanSnapper snapper = Ensure<LockedShipOceanSnapper>(root);
            snapper.shipRoot = shipRoot;
            snapper.waterOne = waterOne.transform;
            snapper.ocean = ocean;
            snapper.shipToOceanSnapHeightOffset = -0.05f;
            snapper.autoOffsetFromShipSize = true;
            snapper.tinyShipSize = 0.5f;
            snapper.galleonShipSize = 10f;
            snapper.tinyShipOffset = -0.5f;
            snapper.galleonShipOffset = -3.0f;
            snapper.useEightPointHullAverage = true;
            snapper.hullSampleInsetPercent = 0.12f;
            snapper.enabled = true;

            Debug.Log("[LRP PrimitiveSceneInstaller] Installed Living Room Pirates using scene-authored Water1/Water2. Water planes are NOT instantiated. Water1 is the deforming/snap surface; Water2 is a deep backdrop; ship stays locked; WASD moves Water1 tiles under the player.");
        }

        private static void ConfigureWaterTile(GameObject waterOne, bool mainTile)
        {
            // Repair the existing scene Water1 object instead of replacing it.
            // If the placed object has no useful mesh, give that SAME object a
            // subdivided mesh so the deformer has vertices to animate.
            // Water1 is scene-authored, but code owns its EFFECTIVE ocean size.
            // Leave the scene object at scale 1 and replace/repair its mesh to a
            // 40m x 40m subdivided plane so generated tiles have no gaps.
            waterOne.transform.localScale = Vector3.one;
            EnsureMeshPlane(waterOne, 70f, 70f, GetRendererColor(waterOne, new Color(0.05f, 0.38f, 0.58f, 0.82f)), onlyIfMissingOrTiny: false);
            waterOne.transform.localRotation = Quaternion.identity;

            OceanWaveMeshDeformer deformer = Ensure<OceanWaveMeshDeformer>(waterOne);
            deformer.enabled = true;
            deformer.heightAmplitude = 0.65f;
            deformer.spatialFrequency = 0.42f;
            deformer.primaryFrequency = 0.08f;
            deformer.secondaryBlend = 0.30f;
            deformer.secondaryFrequency = 0.12f;
            deformer.seed = 17;
            deformer.useWorldSpaceWaveCoordinates = true;

            WaterOneGrid3x3 grid = Ensure<WaterOneGrid3x3>(waterOne);
            grid.gridRadius = 2;
            grid.tileSize = 70f;
            grid.forwardBiasTiles = 0f;
            grid.earlyRecyclePaddingTiles = 0.2f;
            grid.codeOwnsTileScale = true;
            grid.buildOnStart = true;
            grid.waterTravelEnabled = false;
            grid.waterTravelSpeed = 0.45f;
            grid.waterTravelDirection = new Vector2(0f, 1f);
            grid.visualYawDegrees = 0f;
            grid.enabled = true;

            AuthoritativeOceanSurface surface = Ensure<AuthoritativeOceanSurface>(waterOne);
            surface.deformer = deformer;
            surface.surfaceOffset = -0.05f;
            surface.snapWaterRootInstantly = true;

            OceanWavePresetHotkeys hotkeys = Ensure<OceanWavePresetHotkeys>(waterOne);
            hotkeys.enableHotkeys = true;
            hotkeys.applyDefaultPresetOnStart = false;
            hotkeys.copyToWaterGridTiles = true;
            hotkeys.enabled = true;
        }

        private static void ConfigureWaterTwo(GameObject waterTwo, Color waterOneColor)
        {
            // Water2 is scene-authored too. We only make the existing object huge, deep,
            // and the exact same material color as Water1 so it visually blends.
            EnsureMeshPlane(waterTwo, 50000f, 50000f, waterOneColor);
            Renderer water1Renderer = GameObject.Find(WaterOneName) != null ? GameObject.Find(WaterOneName).GetComponent<Renderer>() : null;
            Renderer water2Renderer = waterTwo.GetComponent<Renderer>();
            if (water1Renderer != null && water2Renderer != null && water1Renderer.sharedMaterial != null)
            {
                water2Renderer.sharedMaterial = water1Renderer.sharedMaterial;
            }
            waterTwo.transform.position = new Vector3(0f, -50f, 0f);
            waterTwo.transform.rotation = Quaternion.identity;
        }

        private static void EnsureMeshPlane(GameObject go, float width, float depth, Color color, bool onlyIfMissingOrTiny = false)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();

            bool needsMesh = mf.sharedMesh == null || mf.sharedMesh.vertexCount < 16;
            if (!onlyIfMissingOrTiny || needsMesh)
            {
                Mesh mesh = BuildGridMesh(width, depth, width > 1000f || depth > 1000f ? 4 : 16, width > 1000f || depth > 1000f ? 4 : 16);
                mesh.name = $"LRP Scene Water Grid Mesh {width:F0}x{depth:F0}";
                mf.sharedMesh = mesh;
            }

            Material mat = mr.sharedMaterial;
            if (mat == null || mat.name.StartsWith("Default"))
            {
                mat = new Material(Shader.Find("Standard"));
                mat.name = go.name + "_PrimitiveWater_Material";
                mr.sharedMaterial = mat;
            }

            mat.color = color;
        }

        private static Mesh BuildGridMesh(float width, float depth, int xSegments, int zSegments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "LRP Generated Water Grid Mesh";
            int xCount = xSegments + 1;
            int zCount = zSegments + 1;
            Vector3[] vertices = new Vector3[xCount * zCount];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[xSegments * zSegments * 6];

            for (int z = 0; z < zCount; z++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    int i = z * xCount + x;
                    float px = ((float)x / xSegments - 0.5f) * width;
                    float pz = ((float)z / zSegments - 0.5f) * depth;
                    vertices[i] = new Vector3(px, 0f, pz);
                    uvs[i] = new Vector2((float)x / xSegments, (float)z / zSegments);
                }
            }

            int ti = 0;
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int a = z * xCount + x;
                    int b = a + 1;
                    int c = a + xCount;
                    int d = c + 1;
                    triangles[ti++] = a;
                    triangles[ti++] = c;
                    triangles[ti++] = b;
                    triangles[ti++] = b;
                    triangles[ti++] = c;
                    triangles[ti++] = d;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateOrUpdateShipBottomContact(Transform shipRoot, OceanWorldController ocean)
        {
            if (shipRoot == null || ocean == null)
            {
                return;
            }

            Transform contact = shipRoot.Find("ShipBottomOceanContact");
            if (contact == null)
            {
                GameObject contactObject = new GameObject("ShipBottomOceanContact");
                contact = contactObject.transform;
                contact.SetParent(shipRoot, false);
            }

            Bounds? bounds = ResolveShipBoundsForContact(shipRoot);
            if (bounds.HasValue)
            {
                Vector3 world = bounds.Value.center;
                world.y = bounds.Value.min.y;
                contact.position = world;
            }
            else
            {
                contact.localPosition = Vector3.zero;
            }

            ocean.waterOneFollowTarget = contact;
            ocean.useVisualShipBoundsBottomAsWaterOneTarget = false;
            ocean.closeWaterOneGapToShip = true;
            ocean.waterOneFollowStrength = 0f;
            ocean.waterOneHeightOffset = 0f;
            ocean.shipToOceanSnapHeightOffset = -0.05f;
        }

        private static Bounds? ResolveShipBoundsForContact(Transform shipRoot)
        {
            Renderer[] renderers = shipRoot.GetComponentsInChildren<Renderer>();
            Bounds bounds = default;
            bool found = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || ShouldIgnoreForShipContact(r.transform))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            return found ? bounds : (Bounds?)null;
        }

        private static bool ShouldIgnoreForShipContact(Transform transform)
        {
            while (transform != null)
            {
                string n = transform.name.ToLowerInvariant();
                if (n.Contains("water") || n.Contains("ocean") || n.Contains("debug") || n.Contains("floating") || n.Contains("cannonball") || n.Contains("boom"))
                {
                    return true;
                }
                transform = transform.parent;
            }

            return false;
        }

        private static void DisableLegacyDebugObjectsAndControls()
        {
            foreach (LivingRoomPiratesShipKeyboardDebugControls controls in Object.FindObjectsOfType<LivingRoomPiratesShipKeyboardDebugControls>())
            {
                controls.enabled = false;
                controls.showOverlay = false;
            }

            foreach (LivingRoomPiratesPrimitiveDebugSandbox sandbox in Object.FindObjectsOfType<LivingRoomPiratesPrimitiveDebugSandbox>())
            {
                sandbox.enabled = false;
            }

            foreach (LivingRoomPiratesEditorAutoActivator activator in Object.FindObjectsOfType<LivingRoomPiratesEditorAutoActivator>())
            {
                activator.enabled = false;
            }

            GameObject oldRoot = GameObject.Find("LivingRoomPiratesDebugRoot");
            if (oldRoot != null && oldRoot.name != RootName)
            {
                oldRoot.SetActive(false);
            }
        }

        private static void AddPrimitiveBehavioursToGeneratedStations(Transform shipRoot)
        {
            if (shipRoot == null) return;

            foreach (Transform tr in shipRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name.ToLowerInvariant();
                if (n.Contains("cannon") && tr.GetComponent<CannonController>() == null)
                {
                    CannonController cannon = tr.gameObject.AddComponent<CannonController>();
                    cannon.fireCooldown = 1.5f;
                    cannon.fireForce = 8f;
                    cannon.cannonballLifetime = 4f;
                    cannon.firePoint = CreateFirePoint(tr);
                    cannon.cannonballPrefab = CreateCannonballPrefab();
                }
            }
        }

        private static Transform CreateFirePoint(Transform cannon)
        {
            Transform existing = cannon.Find("FirePoint");
            if (existing != null) return existing;

            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(cannon, false);
            fp.transform.localPosition = new Vector3(0f, 0.25f, 0.55f);
            fp.transform.localRotation = Quaternion.identity;
            return fp.transform;
        }

        private static GameObject _cannonballPrefab;
        private static GameObject CreateCannonballPrefab()
        {
            if (_cannonballPrefab != null) return _cannonballPrefab;
            _cannonballPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _cannonballPrefab.name = "LRP_Primitive_Cannonball_Prefab";
            _cannonballPrefab.transform.localScale = Vector3.one * 0.16f;
            Rigidbody rb = _cannonballPrefab.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            Renderer r = _cannonballPrefab.GetComponent<Renderer>();
            if (r != null) r.material.color = Color.black;
            _cannonballPrefab.SetActive(false);
            Object.DontDestroyOnLoad(_cannonballPrefab);
            return _cannonballPrefab;
        }

        private static void DisablePhotonInScene()
        {
            MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null) continue;
                string typeName = behaviour.GetType().Name;
                string fullName = behaviour.GetType().FullName ?? typeName;
                if (typeName.Contains("Photon") || typeName.Contains("Network") || fullName.Contains("Photon"))
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static GameObject FindSceneObject(string name)
        {
            // GameObject.Find is intentional here: Water1/Water2 must be real, active,
            // scene-authored objects. We do not auto-create missing water anymore.
            GameObject found = GameObject.Find(name);
            return found;
        }

        private static Color GetRendererColor(GameObject go, Color fallback)
        {
            if (go == null) return fallback;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null) return r.sharedMaterial.color;
            return fallback;
        }

        private static GameObject FindOrCreate(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) return existing;
            return new GameObject(name);
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child.gameObject;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            return component;
        }
    }
}
