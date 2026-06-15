using UnityEngine;

/// <summary>
/// Turns the scene Water1 object into a reusable, low-overhead visual ocean grid.
/// Water1 itself remains the authoritative vertical snap root. The generated
/// Water1_Tile_* children are placed under one shared TileGridRoot so yaw/heading,
/// wave coordinates, movement, and recycling all share one transform space.
/// </summary>
[DisallowMultipleComponent]
public class WaterOneGrid3x3 : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("2 = 5x5 grid.")]
    public int gridRadius = 2;

    [Tooltip("World-space width/depth of one visible Water1 tile. Leave scene Water1 scale at 1,1,1; code scales the visible tiles.")]
    public float tileSize = 70f;

    [Tooltip("When true, generated visual tiles are scaled to tileSize even if the scene Water1 plane is Scale 1 with Unity's default 10m plane mesh.")]
    public bool codeOwnsTileScale = true;

    public bool buildOnStart = true;
    public bool rebuildIfTileSizeChanges = false;

    [Header("Escalator Recycling")]
    public bool waterTravelEnabled = true;
    public bool recycleTilesAroundAnchor = true;
    public Transform recycleAnchor;

    [Tooltip("Moves the generated Water1 tiles under the locked ship. Water1 root only moves vertically for snap.")]
    public float waterTravelSpeed = 1.4f;

    [Tooltip("Virtual ship heading. Kept for compatibility; visualYawDegrees is the authority for grid movement/recycling.")]
    public Vector2 waterTravelDirection = new Vector2(0f, 1f);

    [Tooltip("Yaw used to rotate the entire tile grid parent. Individual tiles remain unrotated in local space.")]
    public float visualYawDegrees = 0f;

    [Tooltip("Optional forward placement bias in tile widths. Use 0 for a symmetric grid around the ship.")]
    public float forwardBiasTiles = 0f;

    [Tooltip("Recycle before the tile is actually at the edge. Higher values reduce visible edge exposure.")]
    [Range(0f, 0.45f)] public float earlyRecyclePaddingTiles = 0.2f;

    private const string GridRootName = "Water1_TileGridRoot_GROUP_ROTATES_AS_ONE";
    private float _lastTileSize;
    private Transform _gridRoot;
    private Vector2 _waveTreadmillOffset;
    [Header("Authoritative Ocean Coordinates")]
    [Tooltip("Virtual ship position in ocean-space. The physical ship stays at the origin; this advances instead.")]
    public Vector2 virtualShipOceanPosition = Vector2.zero;


    private void Start()
    {
        if (buildOnStart)
        {
            BuildGrid();
        }
    }

    private void LateUpdate()
    {
        if (rebuildIfTileSizeChanges)
        {
            float resolved = ResolveTileSize();
            if (Mathf.Abs(resolved - _lastTileSize) > 0.01f)
            {
                BuildGrid();
            }
        }

        EnsureGridRoot();
        AdvanceVirtualShip(Time.deltaTime);
        UpdateGroupTransform();
        PlaceTilesFromVirtualOceanPosition();
        RefreshWaveCoordinateRoots();
    }

    private void EnsureGridRoot()
    {
        if (_gridRoot != null) return;
        Transform existing = transform.Find(GridRootName);
        if (existing != null)
        {
            _gridRoot = existing;
            return;
        }

        GameObject root = new GameObject(GridRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        _gridRoot = root.transform;
    }

    private void RefreshWaveCoordinateRoots()
    {
        EnsureGridRoot();
        OceanWaveMeshDeformer rootDeformer = GetComponent<OceanWaveMeshDeformer>();
        if (rootDeformer != null)
        {
            rootDeformer.waveCoordinateRoot = _gridRoot;
            rootDeformer.useWaveCoordinateRoot = true;
            rootDeformer.useWorldSpaceWaveCoordinates = true;
            rootDeformer.waveCoordinateOffset = _waveTreadmillOffset;
            rootDeformer.useWaveCoordinateOffset = true;
        }

        if (_gridRoot == null) return;
        OceanWaveMeshDeformer[] deformers = _gridRoot.GetComponentsInChildren<OceanWaveMeshDeformer>(true);
        for (int i = 0; i < deformers.Length; i++)
        {
            if (deformers[i] == null) continue;
            deformers[i].waveCoordinateRoot = _gridRoot;
            deformers[i].useWaveCoordinateRoot = true;
            deformers[i].useWorldSpaceWaveCoordinates = true;
            deformers[i].waveCoordinateOffset = _waveTreadmillOffset;
            deformers[i].useWaveCoordinateOffset = true;
        }
    }

    private void UpdateGroupTransform()
    {
        if (_gridRoot == null) return;
        Vector3 anchor = recycleAnchor != null ? recycleAnchor.position : Vector3.zero;
        _gridRoot.position = new Vector3(anchor.x, transform.position.y, anchor.z);

        // Ship is locked physically facing +Z. A left/right heading change is shown
        // by rotating the ocean frame the opposite way around the player.
        _gridRoot.rotation = Quaternion.Euler(0f, -visualYawDegrees, 0f);
        _gridRoot.localScale = Vector3.one;
    }

    private void AdvanceVirtualShip(float deltaTime)
    {
        if (!waterTravelEnabled) return;

        float step = waterTravelSpeed * deltaTime;
        if (step <= 0f) return;

        Vector2 dir2 = waterTravelDirection.sqrMagnitude > 0.0001f ? waterTravelDirection.normalized : Vector2.up;

        // This is the whole treadmill model:
        // the physical ship never moves. Instead, the virtual ship position moves
        // through ocean coordinates. Everything in/on the ocean renders relative
        // to this value. If heading changes, this direction changes immediately.
        virtualShipOceanPosition += dir2 * step;
        _waveTreadmillOffset = virtualShipOceanPosition;
        PushWaveOffsetToTiles();
    }

    private void PlaceTilesFromVirtualOceanPosition()
    {
        if (_gridRoot == null) return;

        float resolved = _lastTileSize > 0.01f ? _lastTileSize : ResolveTileSize();
        if (resolved <= 0.01f) return;

        // Pick the nearest ocean-space tile center under the virtual ship, then
        // render a centered grid around it. Tile local position is oceanCoord - shipOceanPosition.
        // The grid root rotation then converts that ocean-relative vector into the player view.
        float baseX = Mathf.Round(virtualShipOceanPosition.x / resolved) * resolved;
        float baseZ = Mathf.Round(virtualShipOceanPosition.y / resolved) * resolved;
        Vector3 bias = Vector3.forward * (resolved * forwardBiasTiles);

        ForEachTile(tile =>
        {
            if (tile == null) return;
            if (!TryParseTileCoords(tile.name, out int ix, out int iz)) return;

            Vector2 tileOceanCenter = new Vector2(baseX + ix * resolved, baseZ + iz * resolved);
            Vector2 rel = tileOceanCenter - virtualShipOceanPosition;
            tile.localPosition = new Vector3(rel.x, 0f, rel.y) + bias;
            tile.localRotation = Quaternion.identity;
        });
    }

    private static bool TryParseTileCoords(string name, out int x, out int z)
    {
        x = 0; z = 0;
        if (string.IsNullOrEmpty(name) || !name.StartsWith("Water1_Tile_")) return false;
        string rest = name.Substring("Water1_Tile_".Length);
        string[] parts = rest.Split('_');
        if (parts.Length < 2) return false;
        return int.TryParse(parts[0], out x) && int.TryParse(parts[1], out z);
    }

    private void PushWaveOffsetToTiles()
    {
        OceanWaveMeshDeformer rootDeformer = GetComponent<OceanWaveMeshDeformer>();
        if (rootDeformer != null)
        {
            rootDeformer.waveCoordinateOffset = _waveTreadmillOffset;
            rootDeformer.useWaveCoordinateOffset = true;
        }

        if (_gridRoot == null) return;
        OceanWaveMeshDeformer[] deformers = _gridRoot.GetComponentsInChildren<OceanWaveMeshDeformer>(true);
        for (int i = 0; i < deformers.Length; i++)
        {
            if (deformers[i] == null) continue;
            deformers[i].waveCoordinateOffset = _waveTreadmillOffset;
            deformers[i].useWaveCoordinateOffset = true;
        }
    }

    public Transform TileGridRoot => _gridRoot;

    public Vector2 WaveTreadmillOffset => _waveTreadmillOffset;

    private void RecycleTiles()
    {
        float resolved = _lastTileSize > 0.01f ? _lastTileSize : ResolveTileSize();
        if (resolved <= 0.01f) return;

        float centerZ = resolved * forwardBiasTiles; // normally zero: symmetric 5x5 grid
        float halfGrid = resolved * (gridRadius + 0.5f - earlyRecyclePaddingTiles);
        float fullGrid = resolved * (gridRadius * 2 + 1);

        ForEachTile(tile => RecycleSingleTileLocal(tile, centerZ, halfGrid, fullGrid));
    }

    private void ForEachTile(System.Action<Transform> action)
    {
        if (action == null) return;
        EnsureGridRoot();
        if (_gridRoot == null) return;
        for (int i = 0; i < _gridRoot.childCount; i++)
        {
            Transform child = _gridRoot.GetChild(i);
            if (child != null && child.name.StartsWith("Water1_Tile_")) action(child);
        }
    }

    private static void RecycleSingleTileLocal(Transform tile, float centerZ, float halfGrid, float fullGrid)
    {
        if (tile == null) return;

        Vector3 p = tile.localPosition;
        float x = p.x;
        float z = p.z - centerZ;
        bool changed = false;

        while (x > halfGrid) { x -= fullGrid; changed = true; }
        while (x < -halfGrid) { x += fullGrid; changed = true; }
        while (z > halfGrid) { z -= fullGrid; changed = true; }
        while (z < -halfGrid) { z += fullGrid; changed = true; }

        if (changed)
        {
            p.x = x;
            p.z = z + centerZ;
            tile.localPosition = p;
        }
    }

    [ContextMenu("Build Water1 Visual Grid")]
    public void BuildGrid()
    {
        ClearGeneratedTiles();
        EnsureGridRoot();

        OceanWaveMeshDeformer sourceDeformer = GetComponent<OceanWaveMeshDeformer>();
        MeshFilter sourceMesh = GetComponent<MeshFilter>();
        Renderer sourceRenderer = GetComponent<Renderer>();

        if (sourceMesh == null || sourceMesh.sharedMesh == null || sourceRenderer == null)
        {
            Debug.LogWarning("[WaterOneGrid3x3] Water1 needs a MeshFilter and Renderer to clone visual tiles.", this);
            return;
        }

        _lastTileSize = ResolveTileSize();
        if (_lastTileSize <= 0.01f)
        {
            Debug.LogWarning("[WaterOneGrid3x3] Could not resolve tile size.", this);
            return;
        }

        UpdateGroupTransform();
        RefreshWaveCoordinateRoots();

        Vector3 biasedCenter = Vector3.forward * (_lastTileSize * forwardBiasTiles);

        for (int x = -gridRadius; x <= gridRadius; x++)
        {
            for (int z = -gridRadius; z <= gridRadius; z++)
            {
                CreateTile(x, z, biasedCenter + Vector3.right * (x * _lastTileSize) + Vector3.forward * (z * _lastTileSize), sourceMesh, sourceRenderer, sourceDeformer, _lastTileSize);
            }
        }

        sourceRenderer.enabled = false;
        Debug.Log($"[WaterOneGrid3x3] Built {(gridRadius * 2 + 1)}x{(gridRadius * 2 + 1)} Water1 grid under one rotating parent. Tile size: {_lastTileSize:F1}m, forward bias: {forwardBiasTiles:F2}.", this);
    }

    private void CreateTile(int x, int z, Vector3 localOffsetFromAnchor, MeshFilter sourceMesh, Renderer sourceRenderer, OceanWaveMeshDeformer sourceDeformer, float resolvedTileSize)
    {
        EnsureGridRoot();
        GameObject tile = new GameObject($"Water1_Tile_{x}_{z}");
        tile.transform.SetParent(_gridRoot, false);
        tile.transform.localPosition = localOffsetFromAnchor;
        tile.transform.localRotation = Quaternion.identity;
        tile.transform.localScale = ResolveTileLocalScale(sourceMesh, resolvedTileSize);

        MeshFilter meshFilter = tile.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = sourceMesh.sharedMesh;

        Renderer renderer = tile.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = sourceRenderer.sharedMaterials;
        renderer.enabled = true;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        OceanWaveMeshDeformer deformer = tile.AddComponent<OceanWaveMeshDeformer>();
        deformer.CopyWaveSettingsFrom(sourceDeformer);
        deformer.waveCoordinateRoot = _gridRoot;
        deformer.useWaveCoordinateRoot = true;
        deformer.useWorldSpaceWaveCoordinates = true;
        deformer.tileVariationSeed = (x + 31) * 73856093 ^ (z + 37) * 19349663;
        deformer.useTileInteriorVariation = true;
    }

    private Vector3 ResolveTileLocalScale(MeshFilter sourceMesh, float resolvedTileSize)
    {
        if (!codeOwnsTileScale || sourceMesh == null || sourceMesh.sharedMesh == null)
        {
            return Vector3.one;
        }

        Bounds localBounds = sourceMesh.sharedMesh.bounds;
        float localSize = Mathf.Max(Mathf.Abs(localBounds.size.x), Mathf.Abs(localBounds.size.z));
        if (localSize <= 0.0001f)
        {
            return Vector3.one;
        }

        float scale = resolvedTileSize / localSize;
        return new Vector3(scale, 1f, scale);
    }

    private float ResolveTileSize()
    {
        if (tileSize > 0.01f)
        {
            return tileSize;
        }

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            return Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z);
        }

        return 0f;
    }

    private void ClearGeneratedTiles()
    {
        Transform oldRoot = transform.Find(GridRootName);
        if (oldRoot != null)
        {
            if (Application.isPlaying) Destroy(oldRoot.gameObject);
            else DestroyImmediate(oldRoot.gameObject);
            if (_gridRoot == oldRoot) _gridRoot = null;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || !child.name.StartsWith("Water1_Tile_")) continue;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
