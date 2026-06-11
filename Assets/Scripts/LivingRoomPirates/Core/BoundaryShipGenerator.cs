using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

// ---------------------------------------------------------------------------
// BoundaryShipGenerator.cs
// Living Room Pirates – ship layout generator
//
// SETUP (Inspector):
//   1. Attach this script to the "LivingRoomPiratesRoot" GameObject.
//   2. Assign prefabs for DeckTile, RailingStraight, RailingCorner, and each
//      station (see TODO comments below).
//   3. Assign the shipGeneratedRoot and boundaryDebugRoot transforms.
//   4. Optionally adjust safetyMargin and editorFallback dimensions.
//
// RUNTIME:
//   • Start() calls RegenerateShip() automatically.
//   • Call RegenerateShip() from the Unity Editor or from another script to
//     rebuild the ship at any time (e.g., after a play-area change).
// ---------------------------------------------------------------------------

/// <summary>
/// Ship sizes derived from the usable Guardian play-area rectangle.
/// The smaller of width/depth is used as the limiting dimension.
/// </summary>
public enum ShipTier
{
    Dinghy,   // < 1.5 m (or no valid boundary)
    Rowboat,  // 1.5 – 2.2 m
    Sloop,    // 2.2 – 3.0 m
    Brig,     // 3.0 – 4.0 m
    Galleon   // 4.0 m +
}

public class BoundaryShipGenerator : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector: scene references
    // -----------------------------------------------------------------------

    [Header("Scene Roots")]
    [Tooltip("Parent for all generated ship geometry. Clear this to rebuild.")]
    public Transform shipGeneratedRoot;

    [Tooltip("Parent for boundary debug visualisers (floor rectangle, etc.).")]
    public Transform boundaryDebugRoot;

    // -----------------------------------------------------------------------
    // Inspector: ship-part prefabs
    // -----------------------------------------------------------------------

    [Header("Structural Prefabs")]
    // TODO: Assign a flat deck-tile prefab (e.g. a thin box or quad).
    public GameObject deckTilePrefab;

    // TODO: Assign a straight railing segment prefab (~1 m long, ~1 m tall).
    public GameObject railingStraightPrefab;

    // TODO: Assign a 90-degree corner railing prefab.
    public GameObject railingCornerPrefab;

    [Header("Station Prefabs")]
    // TODO: Assign the SteeringWheel prefab.
    public GameObject steeringWheelPrefab;

    // TODO: Assign the forward CannonForward prefab (has CannonController).
    public GameObject cannonForwardPrefab;

    // TODO: Assign the side Cannon prefab (has CannonController).
    public GameObject cannonSidePrefab;

    // TODO: Assign the Anchor Lever prefab.
    public GameObject anchorLeverPrefab;

    // TODO: Assign the Sail Rope prefab.
    public GameObject sailRopePrefab;

    // TODO: Assign the Spyglass prefab.
    public GameObject spyglassPrefab;

    // TODO: Assign the Oar prefab (placed in pairs).
    public GameObject oarPrefab;

    // TODO: Assign the Repair Bucket prefab.
    public GameObject repairBucketPrefab;

    // TODO: Assign the Ammo Crate prefab.
    public GameObject ammoCratePrefab;

    // TODO: Assign the Treasure Chest prefab (Galleon only).
    public GameObject treasureChestPrefab;

    // TODO: Assign the small Mast prefab.
    public GameObject mastSmallPrefab;

    // -----------------------------------------------------------------------
    // Inspector: generation parameters
    // -----------------------------------------------------------------------

    [Header("Boundary Settings")]
    [Tooltip("Metres kept between the usable play area edge and real Guardian boundary. Default 0.35 m.")]
    public float safetyMargin = 0.35f;

    [Header("Editor / No-Headset Fallback")]
    [Tooltip("Width used when no valid Guardian boundary is detected (e.g., in the Unity Editor).")]
    public float editorFallbackWidth = 2.5f;

    [Tooltip("Depth used when no valid Guardian boundary is detected.")]
    public float editorFallbackDepth = 2.5f;

    // -----------------------------------------------------------------------
    // Public state (read-only from other scripts)
    // -----------------------------------------------------------------------

    /// <summary>Raw Guardian play-area width in metres (0 if unavailable).</summary>
    public float DetectedWidth  { get; private set; }

    /// <summary>Raw Guardian play-area depth in metres (0 if unavailable).</summary>
    public float DetectedDepth  { get; private set; }

    /// <summary>Usable width after subtracting both safety margins.</summary>
    public float UsableWidth    { get; private set; }

    /// <summary>Usable depth after subtracting both safety margins.</summary>
    public float UsableDepth    { get; private set; }

    /// <summary>Ship tier selected for this session.</summary>
    public ShipTier CurrentTier { get; private set; }

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        EnsureStormMotionController();
        RegenerateShip();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads the Meta Guardian play area, selects a ShipTier, and instantiates
    /// all ship geometry inside shipGeneratedRoot.  Safe to call repeatedly.
    /// </summary>
    [ContextMenu("Regenerate Ship")]
    public void RegenerateShip()
    {
        ClearGenerated();

        // 1. Set tracking origin so world-origin == floor-centre of play area.
        SetTrackingOrigin();

        // 2. Query Guardian boundary dimensions.
        QueryBoundary();

        // 3. Derive usable rectangle.
        UsableWidth = Mathf.Max(0.5f, DetectedWidth  - safetyMargin * 2f);
        UsableDepth = Mathf.Max(0.5f, DetectedDepth - safetyMargin * 2f);

        // 4. Select ship tier (limited by the smaller dimension).
        float minDim = Mathf.Min(UsableWidth, UsableDepth);
        CurrentTier  = SelectTier(minDim);

        // 5. Log results.
        Debug.Log($"[BoundaryShipGenerator] Play area detected: {DetectedWidth:F2} m x {DetectedDepth:F2} m");
        Debug.Log($"[BoundaryShipGenerator] Usable area:        {UsableWidth:F2} m x {UsableDepth:F2} m  (margin={safetyMargin} m)");
        Debug.Log($"[BoundaryShipGenerator] Ship tier selected: {CurrentTier}");

        // 6. Generate ship geometry.
        SpawnDeck();
        SpawnRailings();
        SpawnStations();

        ShipStormMotionController stormMotionController = GetComponent<ShipStormMotionController>();
        if (stormMotionController != null)
        {
            stormMotionController.RefreshEnvironmentLayout();
        }

        Debug.Log("[BoundaryShipGenerator] Ship generation complete.");
    }

    // -----------------------------------------------------------------------
    // Step 1 – Tracking origin
    // -----------------------------------------------------------------------

    private void SetTrackingOrigin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Stage tracking: world origin = floor-centre of play area.
        // This keeps the generated ship aligned with the real room even after
        // normal headset re-centring.
        if (TrySetOculusTrackingOrigin())
        {
            Debug.Log("[BoundaryShipGenerator] Tracking origin set to Stage.");
        }
        else
        {
            Debug.LogWarning("[BoundaryShipGenerator] OVRManager not found – tracking origin unchanged.");
        }
#else
        Debug.Log("[BoundaryShipGenerator] Editor mode: tracking origin not changed.");
#endif
    }

    // -----------------------------------------------------------------------
    // Step 2 – Boundary query
    // -----------------------------------------------------------------------

    private void QueryBoundary()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (TryGetOculusBoundaryDimensions(out float detectedWidth, out float detectedDepth))
        {
            DetectedWidth = detectedWidth;
            DetectedDepth = detectedDepth;

            if (DetectedWidth < 0.5f || DetectedDepth < 0.5f)
            {
                Debug.LogWarning("[BoundaryShipGenerator] Guardian boundary too small – using Dinghy fallback dimensions.");
                ApplyFallback();
            }
        }
        else
        {
            Debug.LogWarning("[BoundaryShipGenerator] No valid Guardian boundary – using Dinghy fallback dimensions.");
            ApplyFallback();
        }
#else
        // Editor / no-headset path: use inspector fallback values.
        ApplyFallback();
        Debug.Log($"[BoundaryShipGenerator] Editor fallback dimensions: {DetectedWidth:F2} m x {DetectedDepth:F2} m");
#endif
    }

    private void ApplyFallback()
    {
        DetectedWidth = editorFallbackWidth;
        DetectedDepth = editorFallbackDepth;
    }

    private bool TrySetOculusTrackingOrigin()
    {
        Type ovrManagerType = FindType("OVRManager");
        if (ovrManagerType == null)
            return false;

        object instance = ovrManagerType.GetProperty("instance")?.GetValue(null, null);
        if (instance == null)
            return false;

        Type trackingOriginType = ovrManagerType.GetNestedType("TrackingOrigin");
        if (trackingOriginType == null)
            return false;

        object stage = Enum.Parse(trackingOriginType, "Stage");
        ovrManagerType.GetProperty("trackingOriginType")?.SetValue(null, stage, null);
        return true;
    }

    private bool TryGetOculusBoundaryDimensions(out float width, out float depth)
    {
        width = 0f;
        depth = 0f;

        Type ovrManagerType = FindType("OVRManager");
        Type ovrBoundaryType = FindType("OVRBoundary");
        if (ovrManagerType == null || ovrBoundaryType == null)
            return false;

        object instance = ovrManagerType.GetProperty("instance")?.GetValue(null, null);
        if (instance == null)
            return false;

        object boundary = ovrManagerType.GetProperty("boundary")?.GetValue(null, null);
        if (boundary == null)
            return false;

        object configured = ovrBoundaryType.GetMethod("GetConfigured")?.Invoke(boundary, null);
        if (!(configured is bool isConfigured) || !isConfigured)
            return false;

        Type boundaryType = ovrBoundaryType.GetNestedType("BoundaryType");
        if (boundaryType == null)
            return false;

        object playArea = Enum.Parse(boundaryType, "PlayArea");
        object dimensions = ovrBoundaryType.GetMethod("GetDimensions")?.Invoke(boundary, new[] { playArea });
        if (!(dimensions is Vector3 dims))
            return false;

        width = dims.x;
        depth = dims.z;
        return true;
    }

    private static Type FindType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null)
                return type;
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // Step 3 – Tier selection
    // -----------------------------------------------------------------------

    private ShipTier SelectTier(float minDim)
    {
        if (minDim < 1.5f) return ShipTier.Dinghy;
        if (minDim < 2.2f) return ShipTier.Rowboat;
        if (minDim < 3.0f) return ShipTier.Sloop;
        if (minDim < 4.0f) return ShipTier.Brig;
        return ShipTier.Galleon;
    }

    // -----------------------------------------------------------------------
    // Step 4 – Deck tiles
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spawns a grid of deck tiles to cover the usable rectangle.
    /// If deckTilePrefab is null, a grey placeholder plane is used.
    /// </summary>
    private void SpawnDeck()
    {
        if (deckTilePrefab == null)
        {
            SpawnPlaceholderDeck();

            Debug.Log("[BoundaryShipGenerator] No deckTilePrefab assigned – spawned placeholder deck.");
            return;
        }

        // Tile the deck with the assigned prefab.
        // Assumes each tile is 1 m x 1 m in local space.
        float tileSize = 1f;
        int  tilesX    = Mathf.CeilToInt(UsableWidth  / tileSize);
        int  tilesZ    = Mathf.CeilToInt(UsableDepth / tileSize);

        float startX = -(tilesX - 1) * tileSize * 0.5f;
        float startZ = -(tilesZ - 1) * tileSize * 0.5f;

        for (int x = 0; x < tilesX; x++)
        {
            for (int z = 0; z < tilesZ; z++)
            {
                Vector3 pos = new Vector3(startX + x * tileSize, 0f, startZ + z * tileSize);
                SpawnPrefab(deckTilePrefab, pos, Quaternion.identity, $"DeckTile_{x}_{z}");
            }
        }

        Debug.Log($"[BoundaryShipGenerator] Spawned {tilesX * tilesZ} deck tiles ({tilesX} x {tilesZ}).");
    }

    // -----------------------------------------------------------------------
    // Step 5 – Railings
    // -----------------------------------------------------------------------

    /// <summary>
    /// Generates railings around the four sides of the usable rectangle.
    /// Each rail is placed just inside the physical Guardian boundary.
    /// Colliders on railings are intentionally left disabled to avoid
    /// players punching geometry at the same time they reach their real wall.
    /// </summary>
    private void SpawnRailings()
    {
        float hw = UsableWidth  * 0.5f; // half-width
        float hd = UsableDepth * 0.5f; // half-depth

        // Segment length assumed to be 1 m.
        float segLen = 1f;

        if (railingStraightPrefab == null && railingCornerPrefab == null)
        {
            SpawnPlaceholderRailings(hw, hd, segLen);
            Debug.Log("[BoundaryShipGenerator] No railing prefabs assigned – spawned placeholder railings.");
            return;
        }

        // --- Front rail (bow, +Z) ---
        SpawnRailLine(new Vector3(-hw, 0f,  hd), new Vector3( hw, 0f,  hd), Vector3.right, segLen, "Front");

        // --- Back rail (stern, -Z) ---
        SpawnRailLine(new Vector3( hw, 0f, -hd), new Vector3(-hw, 0f, -hd), Vector3.left,  segLen, "Back");

        // --- Left rail (port, -X) ---
        SpawnRailLine(new Vector3(-hw, 0f,  hd), new Vector3(-hw, 0f, -hd), Vector3.back,  segLen, "Left");

        // --- Right rail (starboard, +X) ---
        SpawnRailLine(new Vector3( hw, 0f, -hd), new Vector3( hw, 0f,  hd), Vector3.forward, segLen, "Right");

        // --- Corners ---
        SpawnCorner(new Vector3(-hw, 0f,  hd), 0f,   "CornerFrontLeft");
        SpawnCorner(new Vector3( hw, 0f,  hd), 90f,  "CornerFrontRight");
        SpawnCorner(new Vector3( hw, 0f, -hd), 180f, "CornerBackRight");
        SpawnCorner(new Vector3(-hw, 0f, -hd), 270f, "CornerBackLeft");

        Debug.Log("[BoundaryShipGenerator] Railings generated.");
    }

    private void SpawnRailLine(Vector3 from, Vector3 to, Vector3 dir, float segLen, string side)
    {
        if (railingStraightPrefab == null) return;

        float length = Vector3.Distance(from, to);
        int   count  = Mathf.Max(1, Mathf.FloorToInt(length / segLen));

        for (int i = 0; i < count; i++)
        {
            float t   = (i + 0.5f) / count;
            Vector3 p = Vector3.Lerp(from, to, t);
            Quaternion rot = Quaternion.LookRotation(dir);
            GameObject seg = SpawnPrefab(railingStraightPrefab, p, rot, $"Railing_{side}_{i}");

            // Disable railing colliders – players must not push against them.
            if (seg != null)
            {
                foreach (Collider c in seg.GetComponentsInChildren<Collider>())
                    c.enabled = false;
            }
        }
    }

    private void SpawnCorner(Vector3 pos, float yaw, string name)
    {
        if (railingCornerPrefab == null) return;
        GameObject corner = SpawnPrefab(railingCornerPrefab, pos, Quaternion.Euler(0, yaw, 0), name);
        if (corner != null)
        {
            foreach (Collider c in corner.GetComponentsInChildren<Collider>())
                c.enabled = false;
        }
    }

    private void SpawnPlaceholderRailings(float halfWidth, float halfDepth, float segLen)
    {
        SpawnPlaceholderRailLine(new Vector3(-halfWidth, 0f,  halfDepth), new Vector3( halfWidth, 0f,  halfDepth), segLen, "Front");
        SpawnPlaceholderRailLine(new Vector3( halfWidth, 0f, -halfDepth), new Vector3(-halfWidth, 0f, -halfDepth), segLen, "Back");
        SpawnPlaceholderRailLine(new Vector3(-halfWidth, 0f,  halfDepth), new Vector3(-halfWidth, 0f, -halfDepth), segLen, "Left");
        SpawnPlaceholderRailLine(new Vector3( halfWidth, 0f, -halfDepth), new Vector3( halfWidth, 0f,  halfDepth), segLen, "Right");

        SpawnPlaceholderCorner(new Vector3(-halfWidth, 0f,  halfDepth), "CornerFrontLeft");
        SpawnPlaceholderCorner(new Vector3( halfWidth, 0f,  halfDepth), "CornerFrontRight");
        SpawnPlaceholderCorner(new Vector3( halfWidth, 0f, -halfDepth), "CornerBackRight");
        SpawnPlaceholderCorner(new Vector3(-halfWidth, 0f, -halfDepth), "CornerBackLeft");
    }

    private void SpawnPlaceholderRailLine(Vector3 from, Vector3 to, float segLen, string side)
    {
        float length = Vector3.Distance(from, to);
        int count = Mathf.Max(1, Mathf.FloorToInt(length / segLen));
        Vector3 inward = Vector3.Cross(Vector3.up, (to - from).normalized) * 0.05f;
        Vector3? previousTop = null;
        Vector3? previousMid = null;

        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) / count;
            Vector3 position = Vector3.Lerp(from, to, t) + inward;
            Vector3 forward = (to - from).normalized;
            CreatePlaceholderPart(
                $"Railing_{side}_{i}",
                PrimitiveType.Cube,
                position + Vector3.up * 0.5f,
                Quaternion.LookRotation(forward),
                new Vector3(0.08f, 1f, 0.12f),
                new Color(0.35f, 0.24f, 0.12f));

            Vector3 top = position + Vector3.up * 0.82f;
            Vector3 mid = position + Vector3.up * 0.48f;
            if (previousTop.HasValue)
            {
                CreatePlaceholderBeam($"Railing_{side}_{i}_TopRail", previousTop.Value, top, 0.045f, new Color(0.62f, 0.5f, 0.28f));
                CreatePlaceholderBeam($"Railing_{side}_{i}_MidRail", previousMid.Value, mid, 0.03f, new Color(0.48f, 0.37f, 0.18f));
            }

            previousTop = top;
            previousMid = mid;
        }
    }

    private void SpawnPlaceholderCorner(Vector3 position, string partName)
    {
        CreatePlaceholderPart(
            partName,
            PrimitiveType.Cube,
            position + Vector3.up * 0.5f,
            Quaternion.identity,
            new Vector3(0.16f, 1f, 0.16f),
            new Color(0.38f, 0.26f, 0.14f));

        CreatePlaceholderPart(
            $"{partName}_Cap",
            PrimitiveType.Sphere,
            position + Vector3.up * 1.02f,
            Quaternion.identity,
            new Vector3(0.12f, 0.12f, 0.12f),
            new Color(0.73f, 0.62f, 0.36f));
    }

    // -----------------------------------------------------------------------
    // Step 6 – Station placement by tier
    // -----------------------------------------------------------------------

    /// <summary>
    /// Places interactive stations on the flat deck according to the selected
    /// ShipTier.  All positions are relative to world-origin (floor-centre).
    /// </summary>
    private void SpawnStations()
    {
        switch (CurrentTier)
        {
            case ShipTier.Dinghy:   SpawnDinghy();   break;
            case ShipTier.Rowboat:  SpawnRowboat();  break;
            case ShipTier.Sloop:    SpawnSloop();    break;
            case ShipTier.Brig:     SpawnBrig();     break;
            case ShipTier.Galleon:  SpawnGalleon();  break;
        }
    }

    // -- Dinghy -------------------------------------------------------
    // Everything within arm's reach.  Designed for a tiny or missing boundary.
    private void SpawnDinghy()
    {
        Debug.Log("[BoundaryShipGenerator] Generating Dinghy stations.");

        SpawnStation(cannonForwardPrefab, new Vector3(0f, 0f,  0.4f), 0f,   "CannonForward");
        SpawnStation(spyglassPrefab,      new Vector3(0.3f, 0f, 0.2f), 0f,  "Spyglass");
        SpawnStation(oarPrefab,           new Vector3(-0.3f, 0f, 0f), 90f,  "OarLeft");
        SpawnStation(oarPrefab,           new Vector3( 0.3f, 0f, 0f), -90f, "OarRight");
        SpawnStation(ammoCratePrefab,     new Vector3(0f, 0f, -0.3f), 0f,   "AmmoCrate");
    }

    // -- Rowboat -------------------------------------------------------
    private void SpawnRowboat()
    {
        Debug.Log("[BoundaryShipGenerator] Generating Rowboat stations.");

        float hw = UsableWidth  * 0.5f;
        float hd = UsableDepth * 0.5f;

        SpawnStation(cannonForwardPrefab, new Vector3(0f, 0f,  hd * 0.5f), 0f,   "CannonForward");
        SpawnStation(oarPrefab,           new Vector3(-hw * 0.4f, 0f, 0f), 90f,  "OarLeft");
        SpawnStation(oarPrefab,           new Vector3( hw * 0.4f, 0f, 0f), -90f, "OarRight");
        SpawnStation(sailRopePrefab,      new Vector3(0f, 0f, 0f),          0f,  "SailRope");
        SpawnStation(repairBucketPrefab,  new Vector3(0f, 0f, -hd * 0.4f), 0f,  "RepairBucket");
    }

    // -- Sloop ---------------------------------------------------------
    private void SpawnSloop()
    {
        Debug.Log("[BoundaryShipGenerator] Generating Sloop stations.");

        float hw = UsableWidth  * 0.5f;
        float hd = UsableDepth * 0.5f;

        SpawnStation(steeringWheelPrefab, new Vector3(0f, 0f, -hd * 0.5f),  0f,   "SteeringWheel");
        SpawnStation(cannonForwardPrefab, new Vector3(0f, 0f,  hd * 0.5f),  0f,   "CannonForward");
        SpawnStation(cannonSidePrefab,    new Vector3(-hw * 0.5f, 0f, 0f),   90f,  "CannonPortMid");
        SpawnStation(cannonSidePrefab,    new Vector3( hw * 0.5f, 0f, 0f),  -90f,  "CannonStarMid");
        SpawnStation(anchorLeverPrefab,   new Vector3(0f, 0f, hd * 0.35f),  0f,   "AnchorLever");
        SpawnStation(sailRopePrefab,      new Vector3(0f, 0f, 0f),           0f,   "SailRope");
        SpawnStation(mastSmallPrefab,     new Vector3(0f, 0f, 0f),           0f,   "MastSmall");
    }

    // -- Brig ----------------------------------------------------------
    private void SpawnBrig()
    {
        Debug.Log("[BoundaryShipGenerator] Generating Brig stations.");

        float hw = UsableWidth  * 0.5f;
        float hd = UsableDepth * 0.5f;

        SpawnStation(steeringWheelPrefab, new Vector3(0f, 0f, -hd * 0.55f),  0f,   "SteeringWheel");
        SpawnStation(cannonForwardPrefab, new Vector3(0f, 0f,  hd * 0.6f),   0f,   "CannonForward");

        // Two side cannons per side
        SpawnStation(cannonSidePrefab,    new Vector3(-hw * 0.5f, 0f,  hd * 0.25f),  90f, "CannonPortFwd");
        SpawnStation(cannonSidePrefab,    new Vector3(-hw * 0.5f, 0f, -hd * 0.25f),  90f, "CannonPortAft");
        SpawnStation(cannonSidePrefab,    new Vector3( hw * 0.5f, 0f,  hd * 0.25f), -90f, "CannonStarFwd");
        SpawnStation(cannonSidePrefab,    new Vector3( hw * 0.5f, 0f, -hd * 0.25f), -90f, "CannonStarAft");

        SpawnStation(anchorLeverPrefab,   new Vector3(0f, 0f,  hd * 0.4f),   0f,   "AnchorLever");
        SpawnStation(sailRopePrefab,      new Vector3(0f, 0f,  0f),           0f,   "SailRope");
        SpawnStation(mastSmallPrefab,     new Vector3(0f, 0f,  0f),           0f,   "MastSmall");
        SpawnStation(ammoCratePrefab,     new Vector3(-hw * 0.3f, 0f, 0f),    0f,   "AmmoCratePort");
        SpawnStation(ammoCratePrefab,     new Vector3( hw * 0.3f, 0f, 0f),    0f,   "AmmoCrateStar");
        SpawnStation(repairBucketPrefab,  new Vector3(-hw * 0.3f, 0f, -hd * 0.3f), 0f, "RepairBucketPort");
        SpawnStation(repairBucketPrefab,  new Vector3( hw * 0.3f, 0f, -hd * 0.3f), 0f, "RepairBucketStar");
    }

    // -- Galleon -------------------------------------------------------
    private void SpawnGalleon()
    {
        Debug.Log("[BoundaryShipGenerator] Generating Galleon stations.");

        float hw = UsableWidth  * 0.5f;
        float hd = UsableDepth * 0.5f;

        SpawnStation(steeringWheelPrefab, new Vector3(0f, 0f, -hd * 0.6f),  0f,   "SteeringWheel");
        SpawnStation(cannonForwardPrefab, new Vector3(0f, 0f,  hd * 0.65f), 0f,   "CannonForward");

        // Three side cannons per side
        SpawnStation(cannonSidePrefab,    new Vector3(-hw * 0.55f, 0f,  hd * 0.35f),  90f, "CannonPortFwd");
        SpawnStation(cannonSidePrefab,    new Vector3(-hw * 0.55f, 0f,  0f),           90f, "CannonPortMid");
        SpawnStation(cannonSidePrefab,    new Vector3(-hw * 0.55f, 0f, -hd * 0.35f),  90f, "CannonPortAft");
        SpawnStation(cannonSidePrefab,    new Vector3( hw * 0.55f, 0f,  hd * 0.35f), -90f, "CannonStarFwd");
        SpawnStation(cannonSidePrefab,    new Vector3( hw * 0.55f, 0f,  0f),          -90f, "CannonStarMid");
        SpawnStation(cannonSidePrefab,    new Vector3( hw * 0.55f, 0f, -hd * 0.35f), -90f, "CannonStarAft");

        SpawnStation(anchorLeverPrefab,   new Vector3(0f, 0f,  hd * 0.5f),   0f,  "AnchorLever");
        SpawnStation(sailRopePrefab,      new Vector3(-hw * 0.2f, 0f, 0f),   0f,  "SailRopePort");
        SpawnStation(sailRopePrefab,      new Vector3( hw * 0.2f, 0f, 0f),   0f,  "SailRopeStar");
        SpawnStation(mastSmallPrefab,     new Vector3(0f, 0f, 0f),            0f,  "MastSmall");
        SpawnStation(spyglassPrefab,      new Vector3(-hw * 0.3f, 0f, hd * 0.5f), 0f, "Spyglass");
        SpawnStation(ammoCratePrefab,     new Vector3(-hw * 0.35f, 0f, 0f),  0f,  "AmmoCratePort");
        SpawnStation(ammoCratePrefab,     new Vector3( hw * 0.35f, 0f, 0f),  0f,  "AmmoCrateStar");
        SpawnStation(repairBucketPrefab,  new Vector3(-hw * 0.35f, 0f, -hd * 0.35f), 0f, "RepairPort");
        SpawnStation(repairBucketPrefab,  new Vector3( hw * 0.35f, 0f, -hd * 0.35f), 0f, "RepairStar");
        SpawnStation(treasureChestPrefab, new Vector3(0f, 0f, -hd * 0.55f),  0f,  "TreasureChest");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spawns a station prefab and logs it.  Gracefully skips if the prefab
    /// is null (i.e., not yet assigned in the Inspector).
    /// </summary>
    private GameObject SpawnStation(GameObject prefab, Vector3 localPos, float yaw, string label)
    {
        if (prefab == null)
        {
            GameObject marker = SpawnPlaceholderStation(localPos, yaw, label);
            Debug.Log($"[BoundaryShipGenerator]   Station '{label}': prefab not assigned – spawned placeholder.");
            return marker;
        }

        GameObject obj = SpawnPrefab(prefab, localPos, Quaternion.Euler(0f, yaw, 0f), label);
        Debug.Log($"[BoundaryShipGenerator]   Station '{label}' spawned at {localPos}.");
        return obj;
    }

    private GameObject SpawnPlaceholderStation(Vector3 localPos, float yaw, string label)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        if (LabelContains(label, "cannon")) return SpawnPlaceholderCannon(localPos, rotation, label);
        if (LabelContains(label, "steering")) return SpawnPlaceholderWheel(localPos, rotation, label);
        if (LabelContains(label, "anchor")) return SpawnPlaceholderLever(localPos, rotation, label, new Color(0.18f, 0.28f, 0.45f));
        if (LabelContains(label, "sail")) return SpawnPlaceholderRopeRig(localPos, rotation, label);
        if (LabelContains(label, "mast")) return SpawnPlaceholderMast(localPos, rotation, label);
        if (LabelContains(label, "spyglass")) return SpawnPlaceholderSpyglass(localPos, rotation, label);
        if (LabelContains(label, "oar")) return SpawnPlaceholderOar(localPos, rotation, label);
        if (LabelContains(label, "repair")) return SpawnPlaceholderBucket(localPos, rotation, label);
        if (LabelContains(label, "ammo")) return SpawnPlaceholderCrate(localPos, rotation, label, new Color(0.48f, 0.3f, 0.14f));
        if (LabelContains(label, "treasure")) return SpawnPlaceholderChest(localPos, rotation, label);

        GameObject marker = CreatePlaceholderPart(
            label,
            PrimitiveType.Cylinder,
            localPos + Vector3.up * 0.15f,
            rotation,
            new Vector3(0.25f, 0.15f, 0.25f),
            StationColor(label));

        CreatePlaceholderPart(
            $"{label}_Indicator",
            PrimitiveType.Cube,
            localPos + rotation * new Vector3(0f, 0.55f, 0.28f),
            Quaternion.Euler(0f, yaw, 30f),
            new Vector3(0.08f, 0.08f, 0.3f),
            Color.white);

        return marker;
    }

    /// <summary>Instantiates a prefab under shipGeneratedRoot with a given name.</summary>
    private GameObject SpawnPrefab(GameObject prefab, Vector3 localPos, Quaternion localRot, string objName)
    {
        if (prefab == null) return null;

        Transform parent = shipGeneratedRoot != null ? shipGeneratedRoot : transform;
        GameObject obj = Instantiate(prefab, parent);
        obj.name = objName;
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = localRot;
        return obj;
    }

    private GameObject CreatePlaceholderPart(string objName, PrimitiveType primitiveType, Vector3 localPos, Quaternion localRot, Vector3 localScale, Color color)
    {
        Transform parent = shipGeneratedRoot != null ? shipGeneratedRoot : transform;
        GameObject obj = GameObject.CreatePrimitive(primitiveType);
        obj.name = objName;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPos;
        obj.transform.localRotation = localRot;
        obj.transform.localScale = localScale;

        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        return obj;
    }

    private void SpawnPlaceholderDeck()
    {
        float hullWidth = Mathf.Max(1f, UsableWidth + 0.32f);
        float hullDepth = Mathf.Max(1f, UsableDepth + 0.7f);

        CreatePlaceholderPart(
            "HullShadow",
            PrimitiveType.Cube,
            new Vector3(0f, -0.08f, 0f),
            Quaternion.identity,
            new Vector3(hullWidth * 0.92f, 0.04f, hullDepth * 0.92f),
            new Color(0.12f, 0.09f, 0.06f));

        CreatePlaceholderPart(
            "HullBase",
            PrimitiveType.Cube,
            new Vector3(0f, -0.03f, 0f),
            Quaternion.identity,
            new Vector3(hullWidth * 0.8f, 0.22f, hullDepth * 0.86f),
            new Color(0.24f, 0.16f, 0.1f));

        CreatePlaceholderPart(
            "DeckSurface",
            PrimitiveType.Cube,
            new Vector3(0f, 0.03f, 0f),
            Quaternion.identity,
            new Vector3(UsableWidth, 0.06f, UsableDepth),
            new Color(0.56f, 0.43f, 0.24f));

        float plankWidth = Mathf.Clamp(UsableWidth / 5f, 0.28f, 0.5f);
        float plankStart = -UsableWidth * 0.5f + plankWidth * 0.5f;
        int plankCount = Mathf.Max(3, Mathf.RoundToInt(UsableWidth / plankWidth));
        for (int i = 0; i < plankCount; i++)
        {
            float x = plankStart + i * plankWidth;
            CreatePlaceholderPart(
                $"DeckPlank_{i}",
                PrimitiveType.Cube,
                new Vector3(x, 0.065f, 0f),
                Quaternion.identity,
                new Vector3(plankWidth - 0.02f, 0.01f, UsableDepth * 0.98f),
                i % 2 == 0 ? new Color(0.62f, 0.49f, 0.28f) : new Color(0.52f, 0.38f, 0.2f));
        }

        float bowZ = UsableDepth * 0.5f + 0.22f;
        float sternZ = -UsableDepth * 0.5f - 0.18f;
        CreatePlaceholderPart(
            "BowCap",
            PrimitiveType.Cylinder,
            new Vector3(0f, 0.07f, bowZ),
            Quaternion.Euler(90f, 0f, 0f),
            new Vector3(hullWidth * 0.28f, 0.14f, 0.16f),
            new Color(0.34f, 0.22f, 0.13f));

        CreatePlaceholderPart(
            "SternCap",
            PrimitiveType.Cube,
            new Vector3(0f, 0.1f, sternZ),
            Quaternion.identity,
            new Vector3(hullWidth * 0.52f, 0.18f, 0.18f),
            new Color(0.31f, 0.21f, 0.12f));

        CreatePlaceholderPart(
            "HelmStep",
            PrimitiveType.Cube,
            new Vector3(0f, 0.13f, sternZ + 0.1f),
            Quaternion.identity,
            new Vector3(hullWidth * 0.34f, 0.08f, 0.26f),
            new Color(0.4f, 0.29f, 0.16f));
    }

    private GameObject SpawnPlaceholderCannon(Vector3 localPos, Quaternion rotation, string label)
    {
        GameObject carriage = CreatePlaceholderPart(
            label,
            PrimitiveType.Cube,
            localPos + Vector3.up * 0.12f,
            rotation,
            new Vector3(0.32f, 0.14f, 0.32f),
            new Color(0.38f, 0.24f, 0.12f));

        CreatePlaceholderPart(
            $"{label}_Barrel",
            PrimitiveType.Cylinder,
            localPos + rotation * new Vector3(0f, 0.28f, 0.18f),
            rotation * Quaternion.Euler(90f, 0f, 0f),
            new Vector3(0.12f, 0.36f, 0.12f),
            new Color(0.15f, 0.15f, 0.18f));

        return carriage;
    }

    private GameObject SpawnPlaceholderWheel(Vector3 localPos, Quaternion rotation, string label)
    {
        GameObject stand = CreatePlaceholderPart(
            label,
            PrimitiveType.Cube,
            localPos + Vector3.up * 0.2f,
            rotation,
            new Vector3(0.16f, 0.4f, 0.16f),
            new Color(0.45f, 0.3f, 0.16f));

        CreatePlaceholderPart(
            $"{label}_Wheel",
            PrimitiveType.Cylinder,
            localPos + rotation * new Vector3(0f, 0.6f, 0.05f),
            rotation * Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.22f, 0.04f, 0.22f),
            new Color(0.68f, 0.48f, 0.2f));

        return stand;
    }

    private GameObject SpawnPlaceholderLever(Vector3 localPos, Quaternion rotation, string label, Color accent)
    {
        GameObject baseBlock = CreatePlaceholderPart(
            label,
            PrimitiveType.Cube,
            localPos + Vector3.up * 0.08f,
            rotation,
            new Vector3(0.22f, 0.12f, 0.22f),
            new Color(0.28f, 0.18f, 0.1f));

        CreatePlaceholderPart(
            $"{label}_Handle",
            PrimitiveType.Cube,
            localPos + rotation * new Vector3(0f, 0.45f, 0.02f),
            rotation * Quaternion.Euler(-25f, 0f, 0f),
            new Vector3(0.08f, 0.6f, 0.08f),
            accent);

        return baseBlock;
    }

    private GameObject SpawnPlaceholderRopeRig(Vector3 localPos, Quaternion rotation, string label)
    {
        GameObject cleat = CreatePlaceholderPart(
            label,
            PrimitiveType.Cube,
            localPos + Vector3.up * 0.08f,
            rotation,
            new Vector3(0.28f, 0.08f, 0.18f),
            new Color(0.48f, 0.3f, 0.16f));

        CreatePlaceholderPart(
            $"{label}_Rope",
            PrimitiveType.Cylinder,
            localPos + rotation * new Vector3(0f, 0.28f, 0f),
            rotation * Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.14f, 0.03f, 0.14f),
            new Color(0.84f, 0.82f, 0.68f));

        return cleat;
    }

    private GameObject SpawnPlaceholderMast(Vector3 localPos, Quaternion rotation, string label)
    {
        GameObject mast = CreatePlaceholderPart(
            label,
            PrimitiveType.Cylinder,
            localPos + Vector3.up * 0.85f,
            rotation,
            new Vector3(0.12f, 0.85f, 0.12f),
            new Color(0.56f, 0.41f, 0.23f));

        CreatePlaceholderPart(
            $"{label}_Yard",
            PrimitiveType.Cube,
            localPos + rotation * new Vector3(0f, 1.05f, 0f),
            rotation,
            new Vector3(0.9f, 0.05f, 0.08f),
            new Color(0.78f, 0.78f, 0.68f));

        return mast;
    }

    private GameObject SpawnPlaceholderSpyglass(Vector3 localPos, Quaternion rotation, string label)
    {
        GameObject stand = CreatePlaceholderPart(
            label,
            PrimitiveType.Cylinder,
            localPos + Vector3.up * 0.16f,
            rotation,
            new Vector3(0.12f, 0.16f, 0.12f),
            new Color(0.4f, 0.28f, 0.14f));

        CreatePlaceholderPart(
            $"{label}_Scope",
            PrimitiveType.Cylinder,
            localPos + rotation * new Vector3(0f, 0.44f, 0.08f),
            rotation * Quaternion.Euler(70f, 0f, 0f),
            new Vector3(0.06f, 0.24f, 0.06f),
            new Color(0.72f, 0.62f, 0.16f));

        return stand;
    }

    private GameObject SpawnPlaceholderOar(Vector3 localPos, Quaternion rotation, string label)
    {
        GameObject shaft = CreatePlaceholderPart(
            label,
            PrimitiveType.Cube,
            localPos + rotation * new Vector3(0f, 0.15f, 0f),
            rotation,
            new Vector3(0.06f, 0.06f, 0.8f),
            new Color(0.56f, 0.39f, 0.2f));

        CreatePlaceholderPart(
            $"{label}_Blade",
            PrimitiveType.Cube,
            localPos + rotation * new Vector3(0f, 0.15f, 0.38f),
            rotation,
            new Vector3(0.16f, 0.02f, 0.22f),
            new Color(0.62f, 0.46f, 0.24f));

        return shaft;
    }

    private GameObject SpawnPlaceholderBucket(Vector3 localPos, Quaternion rotation, string label)
    {
        GameObject bucket = CreatePlaceholderPart(
            label,
            PrimitiveType.Cylinder,
            localPos + Vector3.up * 0.14f,
            rotation,
            new Vector3(0.18f, 0.14f, 0.18f),
            new Color(0.72f, 0.12f, 0.12f));

        CreatePlaceholderPart(
            $"{label}_Handle",
            PrimitiveType.Cylinder,
            localPos + rotation * new Vector3(0f, 0.34f, 0f),
            rotation * Quaternion.Euler(0f, 0f, 90f),
            new Vector3(0.14f, 0.02f, 0.14f),
            new Color(0.82f, 0.82f, 0.82f));

        return bucket;
    }

    private GameObject SpawnPlaceholderCrate(Vector3 localPos, Quaternion rotation, string label, Color color)
    {
        GameObject crate = CreatePlaceholderPart(
            label,
            PrimitiveType.Cube,
            localPos + Vector3.up * 0.16f,
            rotation,
            new Vector3(0.28f, 0.24f, 0.28f),
            color);

        CreatePlaceholderPart(
            $"{label}_Lid",
            PrimitiveType.Cube,
            localPos + rotation * new Vector3(0f, 0.3f, 0f),
            rotation,
            new Vector3(0.3f, 0.03f, 0.3f),
            new Color(0.65f, 0.49f, 0.28f));

        return crate;
    }

    private GameObject SpawnPlaceholderChest(Vector3 localPos, Quaternion rotation, string label)
    {
        GameObject chest = SpawnPlaceholderCrate(localPos, rotation, label, new Color(0.44f, 0.25f, 0.1f));
        CreatePlaceholderPart(
            $"{label}_Treasure",
            PrimitiveType.Sphere,
            localPos + rotation * new Vector3(0f, 0.34f, 0f),
            rotation,
            new Vector3(0.12f, 0.12f, 0.12f),
            new Color(0.9f, 0.76f, 0.16f));
        return chest;
    }

    private void CreatePlaceholderBeam(string objName, Vector3 from, Vector3 to, float thickness, Color color)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length <= 0.001f) return;

        CreatePlaceholderPart(
            objName,
            PrimitiveType.Cube,
            from + delta * 0.5f,
            Quaternion.LookRotation(delta.normalized),
            new Vector3(thickness, thickness, length),
            color);
    }

    private bool LabelContains(string label, string value)
    {
        return label.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Color StationColor(string label)
    {
        string lowered = label.ToLowerInvariant();

        if (lowered.Contains("cannon")) return new Color(0.15f, 0.15f, 0.18f);
        if (lowered.Contains("steering")) return new Color(0.65f, 0.42f, 0.18f);
        if (lowered.Contains("anchor")) return new Color(0.18f, 0.28f, 0.45f);
        if (lowered.Contains("sail") || lowered.Contains("mast")) return new Color(0.78f, 0.78f, 0.68f);
        if (lowered.Contains("ammo")) return new Color(0.48f, 0.3f, 0.14f);
        if (lowered.Contains("repair")) return new Color(0.72f, 0.12f, 0.12f);
        if (lowered.Contains("spyglass")) return new Color(0.72f, 0.62f, 0.16f);
        if (lowered.Contains("treasure")) return new Color(0.85f, 0.68f, 0.12f);
        if (lowered.Contains("oar")) return new Color(0.5f, 0.32f, 0.18f);

        return new Color(0.24f, 0.52f, 0.62f);
    }

    private void EnsureStormMotionController()
    {
        ShipStormMotionController stormMotionController = GetComponent<ShipStormMotionController>();
        if (stormMotionController == null)
        {
            stormMotionController = gameObject.AddComponent<ShipStormMotionController>();
        }

        stormMotionController.boundaryShipGenerator = this;
    }

    /// <summary>Destroys all children of shipGeneratedRoot so we start fresh.</summary>
    private void ClearGenerated()
    {
        Transform parent = shipGeneratedRoot != null ? shipGeneratedRoot : transform;
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in parent)
            children.Add(child.gameObject);
        foreach (GameObject child in children)
        {
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
