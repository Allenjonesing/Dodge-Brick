using System.Collections;
using System.Collections.Generic;
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
        if (OVRManager.instance != null)
        {
            OVRManager.trackingOriginType = OVRManager.TrackingOrigin.Stage;
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
        // Guard: OVRManager must be present before accessing OVRManager.boundary.
        if (OVRManager.instance == null)
        {
            Debug.LogWarning("[BoundaryShipGenerator] OVRManager not found – using fallback dimensions.");
            ApplyFallback();
            return;
        }

        OVRBoundary boundary = OVRManager.boundary;

        if (boundary != null && boundary.GetConfigured())
        {
            Vector3 dims  = boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);
            DetectedWidth = dims.x;
            DetectedDepth = dims.z;

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
            // Placeholder: a single scaled primitive so the deck is visible
            // even when no prefab has been assigned yet.
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Plane);
            placeholder.name = "DeckPlaceholder";
            placeholder.transform.SetParent(shipGeneratedRoot, false);
            placeholder.transform.localPosition = Vector3.zero;
            // Unity's Plane is 10 m x 10 m at scale 1, so divide by 10.
            placeholder.transform.localScale = new Vector3(UsableWidth / 10f, 1f, UsableDepth / 10f);

            // Grey material to distinguish from the real floor.
            Renderer r = placeholder.GetComponent<Renderer>();
            if (r) r.material.color = new Color(0.45f, 0.35f, 0.2f); // wooden-brown

            // Remove collider so players don't clip.
            Collider c = placeholder.GetComponent<Collider>();
            if (c) c.enabled = false;

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
        if (railingStraightPrefab == null && railingCornerPrefab == null)
        {
            Debug.Log("[BoundaryShipGenerator] No railing prefabs assigned – skipping railings.");
            return;
        }

        float hw = UsableWidth  * 0.5f; // half-width
        float hd = UsableDepth * 0.5f; // half-depth

        // Segment length assumed to be 1 m.
        float segLen = 1f;

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
            Debug.Log($"[BoundaryShipGenerator]   Station '{label}': prefab not assigned – skipped.");
            return null;
        }

        GameObject obj = SpawnPrefab(prefab, localPos, Quaternion.Euler(0f, yaw, 0f), label);
        Debug.Log($"[BoundaryShipGenerator]   Station '{label}' spawned at {localPos}.");
        return obj;
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

    /// <summary>Destroys all children of shipGeneratedRoot so we start fresh.</summary>
    private void ClearGenerated()
    {
        Transform parent = shipGeneratedRoot != null ? shipGeneratedRoot : transform;
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in parent)
            children.Add(child.gameObject);
        foreach (GameObject child in children)
            DestroyImmediate(child);
    }
}
