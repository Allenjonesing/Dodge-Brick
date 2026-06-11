using UnityEngine;

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
    public float waterOneHeightOffset = -1.0f;
    public float waterOneFollowStrength = 0f;
    public bool useShipFootprintSamplesForWaterOne = true;

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

    private OceanStormSettings _settings;
    private Renderer[] _waterOneFollowRenderers;
    private Transform _waterOneFollowRendererRoot;

    public OceanStormSettings Settings => _settings;

    private void Awake()
    {
        Instance = this;
        ApplyPreset();
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

        UpdateFakeTravel();
        UpdateWaterRoots();
    }

    public void ApplyPreset()
    {
        _settings = OceanStormSettings.FromPreset(preset);
    }

    private void UpdateFakeTravel()
    {
        float forwardInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        float reverseInput = Input.GetKey(KeyCode.S) ? -0.5f : 0f;
        float turnInput = 0f;

        if (Input.GetKey(KeyCode.A)) turnInput -= 1f;
        if (Input.GetKey(KeyCode.D)) turnInput += 1f;

        shipHeadingDegrees += turnInput * turnSpeed * Time.deltaTime;

        Vector2 forward = HeadingToVector(shipHeadingDegrees);
        float throttle = forwardInput + reverseInput;

        simulatedVelocity += forward * throttle * acceleration * Time.deltaTime;
        simulatedVelocity = Vector2.ClampMagnitude(simulatedVelocity, maxSpeed);
        simulatedVelocity = Vector2.Lerp(simulatedVelocity, Vector2.zero, Time.deltaTime * 0.35f);

        oceanOffset += simulatedVelocity * Time.deltaTime;
    }

    private void UpdateShipVisualMotion()
    {
        if (visualShipRoot == null)
        {
            return;
        }

        float t = Time.timeSinceLevelLoad;

        float pitch = Mathf.Sin(t * _settings.waveSpeed * 7.1f + seed) * _settings.shipPitchDegrees;
        float roll = Mathf.Sin(t * _settings.waveSpeed * 8.3f + seed * 0.7f) * _settings.shipRollDegrees;
        float bob = Mathf.Sin(t * _settings.waveSpeed * 6.2f + seed * 0.31f) * _settings.shipBobHeight;

        visualShipRoot.localPosition = new Vector3(0f, bob, 0f);
        visualShipRoot.localRotation = Quaternion.Euler(pitch, 0f, roll);
    }

    private void UpdateWaterRoots()
    {
        Vector3 oppositeMovement = new Vector3(-oceanOffset.x, 0f, -oceanOffset.y);

        if (oceanWorldRoot != null)
        {
            oceanWorldRoot.localPosition = oppositeMovement;
            oceanWorldRoot.localRotation = Quaternion.Euler(0f, -shipHeadingDegrees, 0f);
        }

        if (waterTwo != null && visualShipRoot != null)
        {
            Vector3 shipEuler = visualShipRoot.localEulerAngles;
            float pitch = NormalizeAngle(shipEuler.x);
            float roll = NormalizeAngle(shipEuler.z);

            waterTwo.localRotation = Quaternion.Euler(
                -pitch * _settings.horizonPitchCancel,
                0f,
                -roll * _settings.horizonRollCancel
            );
        }

        UpdateWaterOneHeight();
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
        float surfaceHeight = SampleHeight(point) * GetWaterOneVerticalScale();
        return point.y + waterOneHeightOffset - surfaceHeight;
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

        return foundRenderer ? bounds : (Bounds?)null;
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
        Vector3 oceanSpace = WorldToOceanSpace(worldPosition);
        return SampleHeightOceanSpace(oceanSpace.x, oceanSpace.z);
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
            deformer.useControllerSettings = true;
        }
    }
}
