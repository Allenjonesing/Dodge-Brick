using UnityEngine;

[DefaultExecutionOrder(100)]
public class OceanWorldController : MonoBehaviour
{
    public static OceanWorldController Instance { get; private set; }

    [Header("Scene Roots")]
    public Transform oceanWorldRoot;
    public Transform waterOne;
    public Transform waterTwo;
    public Transform visualShipRoot;

    [Header("Water 1 Alignment")]
    public bool closeWaterOneGapToShip = true;
    public Transform waterOneFollowTarget;
    public bool useVisualShipBoundsBottomAsWaterOneTarget = true;
    [Tooltip("Legacy extra water-root offset. Leave at 0 and use shipToOceanSnapHeightOffset below for the ship/ocean gap.")]
    public float waterOneHeightOffset = 0f;
    [Tooltip("<= 0 means snap instantly. Positive values smooth the vertical water root movement.")]
    public float waterOneFollowStrength = 0f;
    [Tooltip("When true, SampleHeight uses Water1/OceanWaveMeshDeformer values instead of OceanStormSettings, so inspector wave values are authoritative.")]
    public bool sampleFromWaterOneDeformer = true;
    [Tooltip("Default false: use one stable sample at the ship center. Footprint sampling can over-correct if bounds include rails/stations/debug objects.")]
    public bool useShipFootprintSamplesForWaterOne = false;
    [Tooltip("Extra debug lift. Negative lowers water relative to hull, making the stationary ship appear slightly raised.")]
    public float shipToOceanSnapHeightOffset = -0.05f;
    [Tooltip("Use the actual deformed mesh vertices from the Water1_Tile_* renderers. This makes ships, debris, and cannonballs match what is visibly rendered instead of an approximation.")]
    public bool sampleFromRenderedWaterOneMesh = false;
    [Tooltip("Maximum horizontal search distance for nearest rendered Water1 vertex. If too small, falls back to wave math.")]
    public float renderedSurfaceSearchRadius = 80f;
    [Tooltip("Authoritative Water1 contact sampler. Usually auto-resolved from Water1.")]
    public AuthoritativeOceanSurface authoritativeSurface;

    [Header("Water 1 Alignment Debug")]
    public bool logWaterOneSnapDebug = false;
    public float waterOneSnapDebugInterval = 1f;

    private float _nextWaterOneSnapDebugTime;

    [Header("Storm")]
    public StormMotionPreset preset = StormMotionPreset.HeavySwell;
    public int seed = 17;

    [Header("Ship Movement")]
    public Vector2 simulatedVelocity;
    public float acceleration = 3f;
    public float maxSpeed = 8f;
    public float turnSpeed = 55f;

    [Header("Runtime")]
    public Vector2 oceanOffset;
    public float shipHeadingDegrees;

    [Header("Water Escalator Travel")]
    [Tooltip("Moves the ocean/water under the locked ship. The ship itself never moves.")]
    public bool enableWaterEscalatorTravel = true;
    public float waterEscalatorSpeed = 1.4f;
    public Vector2 waterEscalatorDirection = new Vector2(0f, 1f);

    private OceanStormSettings _settings;
    private Renderer[] _waterOneFollowRenderers;
    private Transform _waterOneFollowRendererRoot;

    public OceanStormSettings Settings => _settings;

    private void Awake()
    {
        Instance = this;
        ApplyPreset();
        ResolveAuthoritativeSurface();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (_settings == null)
        {
            ApplyPreset();
        }

        ResolveAuthoritativeSurface();

        UpdateFakeTravel();
        LockShipVisualRoot();
        UpdateWaterRoots(false);
    }

    private void LateUpdate()
    {
        LockShipVisualRoot();
        UpdateWaterRoots(true);
    }

    public void ApplyPreset()
    {
        _settings = OceanStormSettings.FromPreset(preset);
    }

    private void UpdateFakeTravel()
    {
        // Living Room Pirates is room-scale. The player/ship must never be moved
        // or rotated by the ocean system. Instead, the ocean offset moves the
        // Water1 grid under the stationary ship like an escalator/conveyor.
        simulatedVelocity = Vector2.zero;

        if (!enableWaterEscalatorTravel)
        {
            return;
        }

        Vector2 direction = waterEscalatorDirection.sqrMagnitude > 0.0001f
            ? waterEscalatorDirection.normalized
            : Vector2.up;

        oceanOffset += direction * waterEscalatorSpeed * Time.deltaTime;
    }

    private void LockShipVisualRoot()
    {
        if (visualShipRoot == null)
        {
            return;
        }

        visualShipRoot.localPosition = Vector3.zero;
        visualShipRoot.localRotation = Quaternion.identity;
    }

    private void UpdateWaterRoots(bool updateHeight)
    {
        // Keep the ocean root itself locked. Earlier debug builds moved OceanWorldRoot,
        // which made it look like one Water1 plane was sliding while the cloned tiles
        // sat around as a strange blue grid. The WaterOneGrid3x3 component now moves
        // and recycles the individual Water1 visual tiles instead.
        if (oceanWorldRoot != null)
        {
            oceanWorldRoot.localRotation = Quaternion.identity;
        }

        if (waterTwo != null)
        {
            waterTwo.localRotation = Quaternion.identity;
        }

        if (updateHeight)
        {
            UpdateWaterOneHeight();
        }
    }

    private void UpdateWaterOneHeight()
    {
        if (!closeWaterOneGapToShip || waterOne == null)
        {
            return;
        }

        float? targetWaterHeight = ResolveTargetWaterOneHeight();

        if (!targetWaterHeight.HasValue)
        {
            return;
        }

        Vector3 waterPosition = waterOne.position;

        if (waterOneFollowStrength <= 0f)
        {
            waterPosition.y = targetWaterHeight.Value;
        }
        else
        {
            float interpolation = 1f - Mathf.Exp(-waterOneFollowStrength * Time.deltaTime);
            waterPosition.y = Mathf.Lerp(waterPosition.y, targetWaterHeight.Value, interpolation);
        }

        waterOne.position = waterPosition;

        if (logWaterOneSnapDebug && Time.time >= _nextWaterOneSnapDebugTime)
        {
            _nextWaterOneSnapDebugTime = Time.time + Mathf.Max(0.1f, waterOneSnapDebugInterval);
            Debug.Log($"[OceanWorldController] Water1 rootY={waterOne.position.y:F3}, targetRootY={targetWaterHeight.Value:F3}, offset={shipToOceanSnapHeightOffset:F3}", this);
        }
    }

    private float? ResolveTargetWaterOneHeight()
    {
        if (waterOneFollowTarget != null)
        {
            return ComputeTargetWaterHeightAtPoint(waterOneFollowTarget.position);
        }

        if (visualShipRoot == null)
        {
            return null;
        }

        if (!useVisualShipBoundsBottomAsWaterOneTarget)
        {
            return ComputeTargetWaterHeightAtPoint(visualShipRoot.position);
        }

        Renderer[] renderers = GetWaterOneFollowRenderers();

        if (renderers == null || renderers.Length == 0)
        {
            return ComputeTargetWaterHeightAtPoint(visualShipRoot.position);
        }

        Bounds? shipBounds = ResolveVisualShipBounds(renderers);

        if (!shipBounds.HasValue)
        {
            return ComputeTargetWaterHeightAtPoint(visualShipRoot.position);
        }

        // Stable default: solve water-root Y so the visible wave surface directly
        // under the center of the ship bottom matches the bottom contact height.
        // This avoids a bad feedback loop where far-away rails, generated debug
        // objects, or ocean tiles distort the bounds/footprint calculation.
        if (!useShipFootprintSamplesForWaterOne)
        {
            Vector3 anchorPosition = shipBounds.Value.center;
            anchorPosition.y = shipBounds.Value.min.y;
            return ComputeTargetWaterHeightAtPoint(anchorPosition);
        }

        return ComputeTargetWaterHeightFromFootprint(shipBounds.Value);
    }

    private Vector3? ResolveWaterOneAnchorPosition()
    {
        if (waterOneFollowTarget != null)
        {
            return waterOneFollowTarget.position;
        }

        if (visualShipRoot == null)
        {
            return null;
        }

        if (!useVisualShipBoundsBottomAsWaterOneTarget)
        {
            return visualShipRoot.position;
        }

        Renderer[] renderers = GetWaterOneFollowRenderers();

        if (renderers == null || renderers.Length == 0)
        {
            return visualShipRoot.position;
        }

        Bounds bounds = renderers[0].bounds;
        bool foundRenderer = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!foundRenderer)
            {
                bounds = renderer.bounds;
                foundRenderer = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        if (!foundRenderer)
        {
            return visualShipRoot.position;
        }

        Vector3 anchorPosition = bounds.center;
        anchorPosition.y = bounds.min.y;
        return anchorPosition;
    }

    private float ComputeTargetWaterHeightAtPoint(Vector3 point)
    {
        ResolveAuthoritativeSurface();
        float offset = waterOneHeightOffset + shipToOceanSnapHeightOffset;
        if (authoritativeSurface != null)
        {
            return authoritativeSurface.SolveWaterRootYForContact(point, offset);
        }

        float waveOffset = SampleWaterOffsetOnly(point);
        return point.y + offset - waveOffset;
    }

    private float SampleWaterOneSurfaceOffset(Vector3 point)
    {
        if (waterOne != null)
        {
            OceanWaveMeshDeformer deformer = waterOne.GetComponent<OceanWaveMeshDeformer>();

            if (deformer != null)
            {
                return deformer.SampleHeightAtWorldPosition(point);
            }
        }

        return SampleHeight(point) * GetWaterOneVerticalScale();
    }

    private float GetWaterOneVerticalScale()
    {
        if (waterOne == null)
        {
            return 1f;
        }

        return Mathf.Abs(waterOne.lossyScale.y);
    }

    private float ComputeTargetWaterHeightFromFootprint(Bounds bounds)
    {
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minZ = bounds.min.z;
        float maxZ = bounds.max.z;
        float y = bounds.min.y;
        float centerX = bounds.center.x;
        float centerZ = bounds.center.z;

        Vector3[] samplePoints = new Vector3[]
        {
            new Vector3(centerX, y, centerZ),
            new Vector3(minX, y, minZ),
            new Vector3(minX, y, centerZ),
            new Vector3(minX, y, maxZ),
            new Vector3(centerX, y, minZ),
            new Vector3(centerX, y, maxZ),
            new Vector3(maxX, y, minZ),
            new Vector3(maxX, y, centerZ),
            new Vector3(maxX, y, maxZ)
        };

        float targetWaterHeight = float.MinValue;

        for (int i = 0; i < samplePoints.Length; i++)
        {
            float sampleTarget = ComputeTargetWaterHeightAtPoint(samplePoints[i]);
            targetWaterHeight = Mathf.Max(targetWaterHeight, sampleTarget);
        }

        return targetWaterHeight;
    }

    private static Bounds? ResolveVisualShipBounds(Renderer[] renderers)
    {
        Bounds bounds = default;
        bool foundRenderer = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || !renderer.enabled || ShouldIgnoreRendererForShipContact(renderer))
            {
                continue;
            }

            if (!foundRenderer)
            {
                bounds = renderer.bounds;
                foundRenderer = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        return foundRenderer ? bounds : (Bounds?)null;
    }

    private static bool ShouldIgnoreRendererForShipContact(Renderer renderer)
    {
        if (renderer == null)
        {
            return true;
        }

        Transform t = renderer.transform;
        while (t != null)
        {
            string name = t.name.ToLowerInvariant();
            if (name.Contains("water") || name.Contains("ocean") || name.Contains("boundarydebug") || name.Contains("debug"))
            {
                return true;
            }

            t = t.parent;
        }

        return false;
    }

    private Renderer[] GetWaterOneFollowRenderers()
    {
        if (_waterOneFollowRendererRoot != visualShipRoot || _waterOneFollowRenderers == null)
        {
            _waterOneFollowRendererRoot = visualShipRoot;
            _waterOneFollowRenderers = visualShipRoot != null
                ? visualShipRoot.GetComponentsInChildren<Renderer>()
                : null;
        }

        return _waterOneFollowRenderers;
    }

    public float SampleHeight(Vector3 worldPosition)
    {
        ResolveAuthoritativeSurface();
        if (authoritativeSurface != null)
        {
            return authoritativeSurface.SampleSurfaceY(worldPosition);
        }

        if (sampleFromWaterOneDeformer && waterOne != null)
        {
            OceanWaveMeshDeformer deformer = waterOne.GetComponent<OceanWaveMeshDeformer>();
            if (deformer != null)
            {
                return waterOne.position.y + deformer.SampleWaveOffsetWorld(worldPosition);
            }
        }

        if (sampleFromRenderedWaterOneMesh)
        {
            float renderedY;
            if (TrySampleRenderedWaterOneSurface(worldPosition, out renderedY))
            {
                return renderedY;
            }
        }

        Vector3 oceanSpace = WorldToOceanSpace(worldPosition);
        return SampleHeightOceanSpace(oceanSpace.x, oceanSpace.z);
    }

    public float SampleWaterOffsetOnly(Vector3 worldPosition)
    {
        ResolveAuthoritativeSurface();
        if (authoritativeSurface != null)
        {
            return authoritativeSurface.SampleWaveOffset(worldPosition);
        }

        if (sampleFromWaterOneDeformer && waterOne != null)
        {
            OceanWaveMeshDeformer deformer = waterOne.GetComponent<OceanWaveMeshDeformer>();
            if (deformer != null)
            {
                return deformer.SampleWaveOffsetWorld(worldPosition);
            }
        }

        Vector3 oceanSpace = WorldToOceanSpace(worldPosition);
        return SampleHeightOceanSpace(oceanSpace.x, oceanSpace.z);
    }

    public bool TrySampleRenderedWaterOneSurface(Vector3 worldPosition, out float surfaceY)
    {
        surfaceY = 0f;
        if (waterOne == null)
        {
            return false;
        }

        MeshFilter[] filters = waterOne.GetComponentsInChildren<MeshFilter>();
        float bestDistanceSq = float.MaxValue;
        float bestY = 0f;
        float maxDistanceSq = Mathf.Max(0.01f, renderedSurfaceSearchRadius * renderedSurfaceSearchRadius);

        for (int f = 0; f < filters.Length; f++)
        {
            MeshFilter filter = filters[f];
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer != null && !renderer.enabled)
            {
                continue;
            }

            string n = filter.name;
            if (filter.transform != waterOne && !n.StartsWith("Water1_Tile_"))
            {
                continue;
            }

            Vector3[] vertices = filter.sharedMesh.vertices;
            Transform t = filter.transform;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 w = t.TransformPoint(vertices[i]);
                float dx = w.x - worldPosition.x;
                float dz = w.z - worldPosition.z;
                float d = dx * dx + dz * dz;

                if (d < bestDistanceSq)
                {
                    bestDistanceSq = d;
                    bestY = w.y;
                }
            }
        }

        if (bestDistanceSq <= maxDistanceSq)
        {
            surfaceY = bestY;
            return true;
        }

        return false;
    }

    public Vector3 SampleNormal(Vector3 worldPosition)
    {
        float sampleDistance = 0.75f;

        float hL = SampleHeight(worldPosition + Vector3.left * sampleDistance);
        float hR = SampleHeight(worldPosition + Vector3.right * sampleDistance);
        float hB = SampleHeight(worldPosition + Vector3.back * sampleDistance);
        float hF = SampleHeight(worldPosition + Vector3.forward * sampleDistance);

        Vector3 normal = new Vector3(hL - hR, sampleDistance * 2f, hB - hF);
        return normal.normalized;
    }

    public Vector3 WorldToOceanSpace(Vector3 worldPosition)
    {
        Vector3 p = worldPosition;

        if (oceanWorldRoot != null)
        {
            p = oceanWorldRoot.InverseTransformPoint(worldPosition);
        }

        p.x += oceanOffset.x;
        p.z += oceanOffset.y;

        return p;
    }

    public float SampleHeightOceanSpace(float x, float z)
    {
        if (_settings == null)
        {
            ApplyPreset();
        }

        ResolveAuthoritativeSurface();

        float t = Time.timeSinceLevelLoad;

        float primary = Mathf.Sin(
            (x + seed * 0.19f) * _settings.waveScale +
            t * _settings.waveSpeed * Mathf.PI * 2f
        );

        float secondary = Mathf.Sin(
            (z - seed * 0.11f) * (_settings.waveScale * 1.37f) +
            t * _settings.secondarySpeed * Mathf.PI * 2f +
            0.8f
        );

        float diagonal = Mathf.Sin(
            (x + z) * (_settings.waveScale * 0.52f) +
            t * _settings.waveSpeed * 1.3f * Mathf.PI * 2f +
            1.7f
        );

        float wave = primary;
        wave += secondary * _settings.secondaryBlend;
        wave += diagonal * (_settings.secondaryBlend * 0.5f);

        return wave * _settings.waveHeight;
    }

    private void ResolveAuthoritativeSurface()
    {
        if (authoritativeSurface != null)
        {
            return;
        }

        if (waterOne != null)
        {
            authoritativeSurface = waterOne.GetComponent<AuthoritativeOceanSurface>();
            if (authoritativeSurface == null)
            {
                authoritativeSurface = waterOne.gameObject.AddComponent<AuthoritativeOceanSurface>();
            }
        }
    }

    private static Vector2 HeadingToVector(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}

[DisallowMultipleComponent]
public class ShipStormMotionController : MonoBehaviour
{
    [Header("Legacy References")]
    public BoundaryShipGenerator boundaryShipGenerator;
    public Transform environmentMotionRoot;
    public Transform oceanVisualRoot;

    [Header("Legacy Options")]
    public bool autoCreateOceanVisuals = true;
    public bool applyPresetOnEnable = true;
    public StormMotionPreset preset = StormMotionPreset.HeavySwell;
    public int stormSeed = 17;
    public float oceanSurfaceChop = 0.09f;
    public float oceanHeight = 0f;

    private OceanWorldController oceanWorldController;

    private void Awake()
    {
        EnsureController();
    }

    private void OnEnable()
    {
        if (applyPresetOnEnable)
        {
            RefreshEnvironmentLayout();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshEnvironmentLayout();
    }

    public void RefreshEnvironmentLayout()
    {
        EnsureController();
        if (oceanWorldController == null)
        {
            return;
        }

        if (environmentMotionRoot == null)
        {
            environmentMotionRoot = EnsureChild(transform, "EnvironmentMotionRoot");
        }

        if (oceanVisualRoot == null && autoCreateOceanVisuals)
        {
            oceanVisualRoot = EnsureChild(environmentMotionRoot, "OceanVisualRoot");
        }

        oceanWorldController.oceanWorldRoot = environmentMotionRoot;
        oceanWorldController.visualShipRoot = ResolveVisualShipRoot();
        oceanWorldController.waterOne = FindOptionalChild(oceanVisualRoot, "Water1");
        oceanWorldController.waterTwo = FindOptionalChild(oceanVisualRoot, "Water2");
        oceanWorldController.preset = preset;
        oceanWorldController.seed = stormSeed;
        oceanWorldController.ApplyPreset();

        ConfigureWaterOneDeformer(oceanWorldController.waterOne);

        if (oceanVisualRoot != null)
        {
            Vector3 localPosition = oceanVisualRoot.localPosition;
            localPosition.y = oceanHeight;
            oceanVisualRoot.localPosition = localPosition;
        }

        OceanStormSettings settings = oceanWorldController.Settings;
        if (settings != null)
        {
            settings.secondaryBlend = Mathf.Max(settings.secondaryBlend, oceanSurfaceChop);
        }
    }

    private void EnsureController()
    {
        if (oceanWorldController == null)
        {
            oceanWorldController = GetComponent<OceanWorldController>();
        }

        if (oceanWorldController == null)
        {
            oceanWorldController = gameObject.AddComponent<OceanWorldController>();
        }
    }

    private Transform ResolveVisualShipRoot()
    {
        if (boundaryShipGenerator != null && boundaryShipGenerator.shipGeneratedRoot != null)
        {
            return boundaryShipGenerator.shipGeneratedRoot;
        }

        Transform generatedRoot = transform.Find("ShipGeneratedRoot");
        return generatedRoot != null ? generatedRoot : transform;
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static Transform FindOptionalChild(Transform parent, string childName)
    {
        return parent != null ? parent.Find(childName) : null;
    }

    private static void ConfigureWaterOneDeformer(Transform waterTransform)
    {
        if (waterTransform == null)
        {
            return;
        }

        OceanWaveMeshDeformer deformer = waterTransform.GetComponent<OceanWaveMeshDeformer>();

        if (deformer != null)
        {
            // Do not override user-assigned wave values. The deformer is the source of truth.
            deformer.useWorldSpaceWaveCoordinates = true;
        }

        WaterOneGrid3x3 grid = waterTransform.GetComponent<WaterOneGrid3x3>();
        if (grid == null)
        {
            grid = waterTransform.gameObject.AddComponent<WaterOneGrid3x3>();
        }

        grid.gridRadius = 2;
        grid.tileSize = Mathf.Max(grid.tileSize, 70f);
        grid.forwardBiasTiles = 0f;
        grid.earlyRecyclePaddingTiles = Mathf.Max(grid.earlyRecyclePaddingTiles, 0.2f);
        grid.buildOnStart = true;
        if (Application.isPlaying)
        {
            grid.BuildGrid();
        }
    }
}
