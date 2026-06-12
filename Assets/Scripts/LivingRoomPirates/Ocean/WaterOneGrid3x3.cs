using UnityEngine;

/// <summary>
/// Turns a single Water1 mesh into a 3x3 seamless visual grid.
/// Attach this to Water1. Water1 remains the root that OceanWorldController moves vertically.
/// The original Water1 renderer is kept as the center tile; the eight clones are visual children.
/// </summary>
[DisallowMultipleComponent]
public class WaterOneGrid3x3 : MonoBehaviour
{
    [Header("Grid")]
    public int gridRadius = 1;
    [Tooltip("World-space width/depth of one Water1 tile. If <= 0, this is estimated from the renderer bounds.")]
    public float tileSize = 0f;
    public bool buildOnStart = true;
    public bool rebuildIfTileSizeChanges = false;

    [Header("Escalator Recycling")]
    public bool waterTravelEnabled = true;
    public bool recycleTilesAroundAnchor = true;
    public Transform recycleAnchor;
    [Tooltip("Moves the generated Water1 tiles under the locked ship. The root only moves vertically for snap.")]
    public float waterTravelSpeed = 1.4f;
    public Vector2 waterTravelDirection = new Vector2(0f, 1f);

    private float _lastTileSize;

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

        if (waterTravelEnabled)
        {
            MoveGeneratedTiles(Time.deltaTime);
        }

        if (waterTravelEnabled && recycleTilesAroundAnchor)
        {
            RecycleTiles();
        }
    }


    private void MoveGeneratedTiles(float deltaTime)
    {
        Vector2 dir = waterTravelDirection.sqrMagnitude > 0.0001f
            ? waterTravelDirection.normalized
            : Vector2.up;

        // Negative direction makes the water move underneath the stationary ship,
        // like the ship is travelling forward even though the player never moves.
        Vector3 localStep = new Vector3(-dir.x, 0f, -dir.y) * waterTravelSpeed * deltaTime;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("Water1_Tile_"))
            {
                child.localPosition += localStep;
            }
        }
    }

    private void RecycleTiles()
    {
        float resolved = _lastTileSize > 0.01f ? _lastTileSize : ResolveTileSize();
        if (resolved <= 0.01f) return;

        Vector3 anchor = recycleAnchor != null ? recycleAnchor.position : Vector3.zero;
        float halfGrid = resolved * (gridRadius + 0.5f);
        float fullGrid = resolved * (gridRadius * 2 + 1);

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("Water1_Tile_"))
            {
                RecycleSingleTile(child, anchor, halfGrid, fullGrid);
            }
        }
    }

    private static void RecycleSingleTile(Transform tile, Vector3 anchor, float halfGrid, float fullGrid)
    {
        if (tile == null || tile.parent == null) return;

        Vector3 world = tile.position;
        bool changed = false;

        while (world.x - anchor.x > halfGrid) { world.x -= fullGrid; changed = true; }
        while (world.x - anchor.x < -halfGrid) { world.x += fullGrid; changed = true; }
        while (world.z - anchor.z > halfGrid) { world.z -= fullGrid; changed = true; }
        while (world.z - anchor.z < -halfGrid) { world.z += fullGrid; changed = true; }

        if (changed)
        {
            tile.position = new Vector3(world.x, tile.position.y, world.z);
        }
    }

    [ContextMenu("Build 3x3 Water1 Grid")]
    public void BuildGrid()
    {
        ClearGeneratedTiles();

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

        for (int x = -gridRadius; x <= gridRadius; x++)
        {
            for (int z = -gridRadius; z <= gridRadius; z++)
            {
                CreateTile(x, z, sourceMesh, sourceRenderer, sourceDeformer, _lastTileSize);
            }
        }

        sourceRenderer.enabled = false;
        Debug.Log($"[WaterOneGrid3x3] Built {(gridRadius * 2 + 1)}x{(gridRadius * 2 + 1)} Water1 visual grid. Root renderer hidden; generated tiles recycle. Tile size: {_lastTileSize:F2} world units.", this);
    }

    private void CreateTile(int x, int z, MeshFilter sourceMesh, Renderer sourceRenderer, OceanWaveMeshDeformer sourceDeformer, float resolvedTileSize)
    {
        GameObject tile = new GameObject($"Water1_Tile_{x}_{z}");
        tile.transform.SetParent(transform, false);

        // The placed scene Water1 is allowed to stay at Scale 1,1,1, but if the
        // user or old scene accidentally scaled it, child local offsets must be
        // divided by the parent scale. Otherwise a 40m tile on a parent scaled 4
        // gets spaced 160m apart and creates huge gaps.
        float sx = Mathf.Abs(transform.lossyScale.x) > 0.0001f ? Mathf.Abs(transform.lossyScale.x) : 1f;
        float sz = Mathf.Abs(transform.lossyScale.z) > 0.0001f ? Mathf.Abs(transform.lossyScale.z) : 1f;
        tile.transform.localPosition = new Vector3((x * resolvedTileSize) / sx, 0f, (z * resolvedTileSize) / sz);
        tile.transform.localRotation = Quaternion.identity;
        tile.transform.localScale = Vector3.one;

        MeshFilter meshFilter = tile.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = sourceMesh.sharedMesh;

        Renderer renderer;
        if (sourceRenderer is MeshRenderer)
        {
            renderer = tile.AddComponent<MeshRenderer>();
        }
        else
        {
            renderer = tile.AddComponent<MeshRenderer>();
        }

        renderer.sharedMaterials = sourceRenderer.sharedMaterials;
        renderer.enabled = true;
        renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        renderer.receiveShadows = sourceRenderer.receiveShadows;

        OceanWaveMeshDeformer deformer = tile.AddComponent<OceanWaveMeshDeformer>();
        deformer.CopyWaveSettingsFrom(sourceDeformer);
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
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith("Water1_Tile_"))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }
}
