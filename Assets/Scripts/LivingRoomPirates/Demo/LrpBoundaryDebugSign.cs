using UnityEngine;

/// <summary>
/// Keeps an in-world wooden sign synced with BoundaryShipGenerator values so
/// Quest testing shows exactly which boundary path and ship tier were used.
/// </summary>
[RequireComponent(typeof(LrpWoodenSign))]
public sealed class LrpBoundaryDebugSign : MonoBehaviour
{
    public BoundaryShipGenerator generator;
    public float refreshInterval = 0.20f;
    private bool _announced;
    private LrpWoodenSign _sign;
    private float _nextRefresh;

    private void Awake()
    {
        _sign = GetComponent<LrpWoodenSign>();
    }

    private void OnEnable()
    {
        if (!_announced)
        {
            _announced = true;
            LrpShipLog.Add("DEBUG LOG PANEL ACTIVE");
        }
        Refresh(true);
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh(false);
    }

    private void Refresh(bool force)
    {
        if (_sign == null) _sign = GetComponent<LrpWoodenSign>();
        if (_sign == null) return;

        if (generator == null)
            generator = FindObjectOfType<BoundaryShipGenerator>();

        if (generator == null)
        {
            _sign.title = "BOUNDARY DEBUG";
            _sign.boardWidth = 3.25f;
            _sign.boardHeight = 1.18f;
            _sign.textCharacterSize = 0.018f;
            _sign.fontSize = 32;
            _sign.BuildIfNeeded();
            _sign.SetBody("DEBUG LOG PANEL ACTIVE\nGenerator: MISSING\nNo BoundaryShipGenerator found\n\nLOGS\n" + LrpShipLog.RecentString(10));
            return;
        }

        string runtime = Application.isEditor ? "Unity Editor" : (Application.platform == RuntimePlatform.Android ? "Quest/Android" : Application.platform.ToString());
        float detectedArea = generator.DetectedWidth * generator.DetectedDepth;
        float usableArea = generator.UsableWidth * generator.UsableDepth;

        _sign.title = "BOUNDARY DEBUG | GAME LOG";
        _sign.boardWidth = 3.25f;
        _sign.boardHeight = 1.18f;
        _sign.textCharacterSize = 0.018f;
        _sign.fontSize = 32;
        _sign.textVisibleThroughWalls = false;
        _sign.doubleSidedText = false;
        _sign.BuildIfNeeded();
        string boundaryColumn =
            "DEBUG LOG PANEL ACTIVE\n" +
            "BOUNDARY STATUS\n" +
            "Runtime: " + runtime + "\n" +
            "Source: " + generator.LastBoundarySource + "\n" +
            "Points: " + generator.LastBoundaryPointCount + "\n" +
            "Detected: " + generator.DetectedWidth.ToString("F2") + " x " + generator.DetectedDepth.ToString("F2") + "m\n" +
            "Det Area: " + detectedArea.ToString("F2") + "m2\n" +
            "Usable: " + generator.UsableWidth.ToString("F2") + " x " + generator.UsableDepth.ToString("F2") + "m\n" +
            "Use Area: " + usableArea.ToString("F2") + "m2\n" +
            "Ship: " + generator.CurrentTier;

        string logColumn = "GAME LOG\n" + LrpShipLog.RecentString(10);

        _sign.SetBody(boundaryColumn + "\n\n" + logColumn);
    }
}
