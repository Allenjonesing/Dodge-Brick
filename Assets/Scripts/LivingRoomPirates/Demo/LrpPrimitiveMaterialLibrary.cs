using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime-safe material resolver for generated primitive Living Room Pirates objects.
/// It prefers materials from the user's texture pack, but falls back to cheap Standard colors
/// if the materials are not included in the build or are named differently.
///
/// Target material names now come from Assets/Materials first:
///   Wood 2, Wood 3, Metal 1 / Metal 1 - Dark, Aluminum Metal
/// </summary>
public static class LrpPrimitiveMaterialLibrary
{
    private static Material _wood2;
    private static Material _wood3;
    private static Material _metal1Dark;
    private static Material _metal1Ball;
    private static Material _metal1Untinted;
    private static Material _aluminumWaterBlue;
    private static Material _fallbackWood;
    private static Material _fallbackDarkWood;
    private static Material _fallbackMetal;
    private static Material _fallbackBall;
    private static Material _fallbackWater;
    private static Material _rope;

    public static void Apply(GameObject go, Color fallbackColor)
    {
        if (go == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = MaterialForObject(go.name, fallbackColor);
    }

    public static void ApplyWood(GameObject go, bool alternate = false)
    {
        if (go == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = alternate ? Wood3() : Wood2();
    }

    public static void ApplyDarkMetal(GameObject go)
    {
        if (go == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = Metal1Dark();
    }

    public static void ApplyDarkMetalRecursive(GameObject go)
    {
        if (go == null) return;
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        Material mat = Metal1Dark();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].sharedMaterial = mat;
        }
    }

    public static void ApplyCannonballMetal(GameObject go)
    {
        if (go == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = Metal1Ball();
    }

    public static void ApplyUntintedMetal(GameObject go)
    {
        if (go == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = Metal1Untinted();
    }

    public static void ApplyWater(GameObject go)
    {
        if (go == null) return;
        Renderer r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.sharedMaterial = AluminumWaterBlue();
    }

    public static Material MaterialForObject(string objectName, Color fallbackColor)
    {
        string n = objectName == null ? string.Empty : objectName.ToLowerInvariant();

        if (n.Contains("water")) return AluminumWaterBlue();
        if (n.Contains("rope") || n.Contains("sheet") || n.Contains("fuse")) return Rope();
        if (n.Contains("cannonball") || n.Contains("cannon_ball") || n.Contains("ball")) return Metal1Ball();
        if (n.Contains("cannon") || n.Contains("barrel") || n.Contains("chain") || n.Contains("metal")) return Metal1Dark();
        if (n.Contains("sign") || n.Contains("trim") || n.Contains("handle") || n.Contains("hammer")) return Wood3();
        if (n.Contains("deck") || n.Contains("plank") || n.Contains("rail") || n.Contains("mast") || n.Contains("yard") || n.Contains("spar") || n.Contains("wheel") || n.Contains("capstan") || n.Contains("hull") || n.Contains("ship")) return Wood2();

        return Fallback("LRP_Color_" + ColorUtility.ToHtmlStringRGBA(fallbackColor), fallbackColor);
    }


    public static Material Rope()
    {
        if (_rope == null) _rope = Fallback("LRP_Fallback_Rope", new Color(0.78f, 0.68f, 0.45f, 1f));
        return _rope;
    }

    public static Material Wood2()
    {
        if (_wood2 == null) _wood2 = CloneTinted(FindNamedMaterial("Wood 2"), new Color(0.78f, 0.66f, 0.50f, 1f), "LRP_Wood2_Runtime");
        if (_wood2 == null) _wood2 = FallbackWood();
        return _wood2;
    }

    public static Material Wood3()
    {
        if (_wood3 == null) _wood3 = CloneTinted(FindNamedMaterial("Wood 3"), new Color(0.70f, 0.55f, 0.38f, 1f), "LRP_Wood3_Runtime");
        if (_wood3 == null) _wood3 = FallbackDarkWood();
        return _wood3;
    }

    public static Material Metal1Dark()
    {
        if (_metal1Dark == null)
        {
            Material source = FindNamedMaterial("Metal 1 - Dark");
            if (source == null) source = FindNamedMaterial("Metal 1 Dark");
            if (source == null) source = FindNamedMaterial("Metal 1");
            _metal1Dark = CloneTinted(source, new Color(0.16f, 0.16f, 0.17f, 1f), "Metal 1 - Dark");
        }
        if (_metal1Dark == null) _metal1Dark = FallbackMetal();
        return _metal1Dark;
    }

    public static Material Metal1Ball()
    {
        if (_metal1Ball == null) _metal1Ball = CloneTinted(FindNamedMaterial("Metal 1"), new Color(0.34f, 0.34f, 0.36f, 1f), "LRP_Metal1_Cannonball_Runtime");
        if (_metal1Ball == null) _metal1Ball = FallbackBall();
        return _metal1Ball;
    }

    public static Material Metal1Untinted()
    {
        if (_metal1Untinted == null)
        {
            Material source = FindNamedMaterial("Metal 1");
            _metal1Untinted = source != null ? new Material(source) : null;
            if (_metal1Untinted != null) _metal1Untinted.name = "LRP_Metal1_Untinted_Runtime";
        }
        if (_metal1Untinted == null) _metal1Untinted = Fallback("LRP_Fallback_UntintedMetal", new Color(0.72f, 0.72f, 0.74f, 1f));
        return _metal1Untinted;
    }

    public static Material AluminumWaterBlue()
    {
        if (_aluminumWaterBlue == null) _aluminumWaterBlue = CloneTinted(FindNamedMaterial("Aluminum Metal"), new Color(0.35f, 0.58f, 0.95f, 1f), "LRP_AluminumMetal_WaterBlue_Runtime");
        if (_aluminumWaterBlue == null) _aluminumWaterBlue = FallbackWater();
        return _aluminumWaterBlue;
    }

    private static Material CloneTinted(Material source, Color tint, string name)
    {
        if (source == null) return null;
        Material m = new Material(source);
        m.name = name;
        SetMaterialColor(m, tint);
        return m;
    }

    private static void SetMaterialColor(Material m, Color color)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
    }

    private static Material FindNamedMaterial(string wantedName)
    {
        if (string.IsNullOrEmpty(wantedName)) return null;

        // Runtime-safe first: if the user puts copies under Assets/Resources/Materials,
        // these paths work on Quest builds. Plain Assets/Materials is editor-only unless
        // the material is otherwise referenced by a scene/prefab, so we also search loaded
        // materials below.
        Material m = Resources.Load<Material>("Materials/" + wantedName);
        if (m != null) return m;
        m = Resources.Load<Material>(wantedName);
        if (m != null) return m;

        // Build/runtime path: use already-loaded material assets. This catches materials
        // referenced by scene objects or prefabs without touching their shader setup.
        Material[] all = Resources.FindObjectsOfTypeAll<Material>();
        for (int i = 0; i < all.Length; i++)
        {
            if (MaterialNameMatches(all[i], wantedName)) return all[i];
        }

#if UNITY_EDITOR
        // Editor path: prefer the user's working folder exactly, not the old Seamless
        // Textures folder. This prevents grabbing mobile-incompatible texture-pack mats.
        string[] exactPaths = new[]
        {
            "Assets/Materials/" + wantedName + ".mat",
            "Assets/Materials/" + wantedName.Replace(" - ", " ") + ".mat"
        };
        for (int i = 0; i < exactPaths.Length; i++)
        {
            Material editorMat = AssetDatabase.LoadAssetAtPath<Material>(exactPaths[i]);
            if (MaterialNameMatches(editorMat, wantedName)) return editorMat;
        }

        string[] guids = AssetDatabase.FindAssets('"' + wantedName + '"' + " t:Material", new[] { "Assets/Materials" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material editorMat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (MaterialNameMatches(editorMat, wantedName)) return editorMat;
        }
#endif
        return null;
    }

    private static bool MaterialNameMatches(Material material, string wantedName)
    {
        if (material == null) return false;
        string n = material.name.Replace(" (Instance)", string.Empty).Trim();
        return string.Equals(n, wantedName, System.StringComparison.OrdinalIgnoreCase)
            || n.ToLowerInvariant().Contains(wantedName.ToLowerInvariant());
    }

    private static Material FallbackWood()
    {
        if (_fallbackWood == null) _fallbackWood = Fallback("LRP_Fallback_Wood2", new Color(0.58f, 0.39f, 0.20f, 1f));
        return _fallbackWood;
    }

    private static Material FallbackDarkWood()
    {
        if (_fallbackDarkWood == null) _fallbackDarkWood = Fallback("LRP_Fallback_Wood3", new Color(0.42f, 0.26f, 0.12f, 1f));
        return _fallbackDarkWood;
    }

    private static Material FallbackMetal()
    {
        if (_fallbackMetal == null) _fallbackMetal = Fallback("LRP_Fallback_DarkMetal", new Color(0.16f, 0.16f, 0.17f, 1f));
        return _fallbackMetal;
    }

    private static Material FallbackBall()
    {
        if (_fallbackBall == null) _fallbackBall = Fallback("LRP_Fallback_CannonballMetal", new Color(0.28f, 0.28f, 0.30f, 1f));
        return _fallbackBall;
    }

    private static Material FallbackWater()
    {
        if (_fallbackWater == null) _fallbackWater = Fallback("LRP_Fallback_AluminumWaterBlue", new Color(0.35f, 0.58f, 0.95f, 1f));
        return _fallbackWater;
    }

    private static Material Fallback(string name, Color color)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material m = new Material(shader);
        m.name = name;
        SetMaterialColor(m, color);
        return m;
    }
}
