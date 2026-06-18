using UnityEngine;

namespace LivingRoomPirates.Demo
{
    /// <summary>
    /// Single authoritative wind source for the primitive Living Room Pirates rig.
    /// Drives ship speed modifier, wind wisps, and Water1 wave size/speed.
    /// The ship stays locked; only the ocean treadmill speed changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LrpWindController : MonoBehaviour
    {
        [Header("References")]
        public LivingRoomPiratesSurfaceDebugSandbox sandbox;
        public OceanWorldController ocean;
        public Transform waterOne;

        [Header("Wind")]
        [Tooltip("Direction the wind is blowing toward in world space.")]
        public Vector2 windDirection = new Vector2(0.35f, 0.94f);
        public float windDirectionChangeDegreesPerSecond = 2.2f;
        [Range(0f, 1f)] public float normalizedWindSpeed = 0.38f;
        public float windSpeedChangePerSecond = 0.035f;
        public Vector2 windSpeedRangeMetersPerSecond = new Vector2(1.0f, 9.0f);

        [Header("Sailing Effect")]
        public float minimumAgainstWindMultiplier = 0.08f;
        public float maximumDownwindMultiplier = 1.35f;
        public float trimImportance = 0.55f;

        [Header("Wave Effect")]
        public Vector2 waveAmplitudeRange = new Vector2(0.22f, 2.15f);
        public Vector2 wavePrimaryFrequencyRange = new Vector2(0.035f, 0.22f);
        public Vector2 waveSpatialFrequencyRange = new Vector2(0.33f, 0.95f);
        public Vector2 waveSecondaryBlendRange = new Vector2(0.05f, 0.32f);

        private LrpWoodenSign _windSign;
        private float _phase;
        private float _directionAngle;

        public float CurrentWindMetersPerSecond => Mathf.Lerp(windSpeedRangeMetersPerSecond.x, windSpeedRangeMetersPerSecond.y, normalizedWindSpeed);
        public Vector3 WindDirectionWorld
        {
            get
            {
                Vector2 d = windDirection.sqrMagnitude > 0.001f ? windDirection.normalized : Vector2.up;
                return new Vector3(d.x, 0f, d.y).normalized;
            }
        }

        private void Awake()
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
            if (ocean == null) ocean = FindObjectOfType<OceanWorldController>();
            if (waterOne == null)
            {
                GameObject w = GameObject.Find("Water1");
                if (w != null) waterOne = w.transform;
            }
            _directionAngle = Mathf.Atan2(windDirection.x, windDirection.y) * Mathf.Rad2Deg;
        }

        private void Update()
        {
            if (sandbox == null) sandbox = FindObjectOfType<LivingRoomPiratesSurfaceDebugSandbox>();
            if (ocean == null) ocean = FindObjectOfType<OceanWorldController>();
            if (waterOne == null)
            {
                GameObject w = GameObject.Find("Water1");
                if (w != null) waterOne = w.transform;
            }

            EvolveWind();
            ApplyToOceanAndVisuals();
            EnsureWindDebugSign();
            UpdateWindDebugSign();
        }

        private void EvolveWind()
        {
            _phase += Time.deltaTime;
            float speedTarget = Mathf.Clamp01(0.52f + Mathf.Sin(_phase * 0.087f) * 0.32f + Mathf.Sin(_phase * 0.031f + 1.7f) * 0.16f);
            normalizedWindSpeed = Mathf.MoveTowards(normalizedWindSpeed, speedTarget, windSpeedChangePerSecond * Time.deltaTime);

            _directionAngle += Mathf.Sin(_phase * 0.043f + 0.8f) * windDirectionChangeDegreesPerSecond * Time.deltaTime;
            Quaternion q = Quaternion.Euler(0f, _directionAngle, 0f);
            Vector3 d = q * Vector3.forward;
            windDirection = new Vector2(d.x, d.z).normalized;
        }

        public float ComputeSailingMultiplier(Vector3 shipForwardWorld, float sailSheetAngleDegrees, out float windAlignment, out float trimEfficiency)
        {
            Vector3 shipForward = shipForwardWorld.sqrMagnitude > 0.001f ? shipForwardWorld.normalized : Vector3.forward;
            Vector3 wind = WindDirectionWorld;

            windAlignment = Vector3.Dot(shipForward, wind); // +1 wind behind us, -1 directly against us.
            float downwind01 = Mathf.InverseLerp(-1f, 1f, windAlignment);
            float rawWindPower = Mathf.Lerp(minimumAgainstWindMultiplier, maximumDownwindMultiplier, downwind01);

            float signedWindAcrossBow = Vector3.SignedAngle(shipForward, wind, Vector3.up);
            float desiredSheet = Mathf.Clamp(signedWindAcrossBow, -70f, 70f);
            float sheetError = Mathf.Abs(Mathf.DeltaAngle(sailSheetAngleDegrees, desiredSheet));
            trimEfficiency = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(1f - sheetError / 95f));

            float windSpeedPower = Mathf.Lerp(0.35f, 1.25f, normalizedWindSpeed);
            return rawWindPower * Mathf.Lerp(1f, trimEfficiency, trimImportance) * windSpeedPower;
        }

        private void ApplyToOceanAndVisuals()
        {
            LrpOceanMotionVisuals visuals = FindObjectOfType<LrpOceanMotionVisuals>();
            if (visuals != null)
            {
                visuals.windSpeed = Mathf.Lerp(2.2f, 12.5f, normalizedWindSpeed);
            }

            WaterPlaneWaveDeformer[] deformers = FindObjectsOfType<WaterPlaneWaveDeformer>();
            float t = normalizedWindSpeed;
            for (int i = 0; i < deformers.Length; i++)
            {
                WaterPlaneWaveDeformer d = deformers[i];
                if (d == null) continue;
                d.heightAmplitude = Mathf.Lerp(waveAmplitudeRange.x, waveAmplitudeRange.y, t);
                d.primaryFrequency = Mathf.Lerp(wavePrimaryFrequencyRange.x, wavePrimaryFrequencyRange.y, t);
                d.spatialFrequency = Mathf.Lerp(waveSpatialFrequencyRange.x, waveSpatialFrequencyRange.y, t);
                d.secondaryBlend = Mathf.Lerp(waveSecondaryBlendRange.x, waveSecondaryBlendRange.y, t);
            }
        }

        private void EnsureWindDebugSign()
        {
            if (_windSign != null) return;
            Transform parent = transform;
            _windSign = LrpWoodenSign.Create(parent, "WindAndSheetsDebugSign", new Vector3(0f, 1.35f, -1.45f), Quaternion.Euler(0f, 180f, 0f), "WIND / SAIL", "starting...");
            _windSign.boardWidth = 1.15f;
            _windSign.boardHeight = 0.82f;
            _windSign.textCharacterSize = 0.026f;
            _windSign.fontSize = 38;
            _windSign.textVisibleThroughWalls = false;
            _windSign.doubleSidedText = false;
            _windSign.BuildIfNeeded();
        }

        private void UpdateWindDebugSign()
        {
            if (_windSign == null || sandbox == null) return;
            _windSign.SetBody(
                "Wind " + CurrentWindMetersPerSecond.ToString("F1") + " m/s\n" +
                "Dir " + Mathf.Atan2(windDirection.x, windDirection.y).ToString("F1") + " deg\n" +
                "Sheet " + sandbox.sailSheetAngleDegrees.ToString("F0") + " deg\n" +
                "Align " + sandbox.lastWindAlignment.ToString("F2") + "\n" +
                "Trim " + sandbox.lastSailTrimEfficiency.ToString("F2") + "\n" +
                "Speed x" + sandbox.lastWindSpeedMultiplier.ToString("F2") + "\n" +
                "Grab sheets to angle sail");
        }
    }
}
