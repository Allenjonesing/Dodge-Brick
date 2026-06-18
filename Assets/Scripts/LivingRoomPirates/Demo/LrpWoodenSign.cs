using UnityEngine;
using LivingRoomPirates.Demo;

/// <summary>
/// Runtime primitive wooden sign builder for headset-readable debug/status signs.
/// Uses only primitives + TextMesh so it works in bare scenes with no prefab setup.
/// Text is front-facing, normal-depth TextMesh so it does not render through ship geometry.
/// </summary>
public sealed class LrpWoodenSign : MonoBehaviour
{
    public TextMesh frontText;
    public TextMesh backText;
    public Renderer boardRenderer;

    public string title = "SIGN";
    [TextArea(3, 12)] public string body = "";

    public float boardWidth = 1.35f;
    public float boardHeight = 0.86f;
    public float boardThickness = 0.055f;
    public float textCharacterSize = 0.030f;
    public int fontSize = 42;
    public bool textVisibleThroughWalls = false;
    public bool doubleSidedText = false;

    private string _lastTitle;
    private string _lastBody;

    public static LrpWoodenSign Create(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, string title, string body)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = localRotation;

        LrpWoodenSign sign = root.AddComponent<LrpWoodenSign>();
        sign.title = title;
        sign.body = body;
        sign.BuildIfNeeded();
        sign.RefreshText(true);
        return sign;
    }

    private void Awake()
    {
        BuildIfNeeded();
        RefreshText(true);
    }

    private void LateUpdate()
    {
        RefreshText(false);
    }

    public void SetBody(string newBody)
    {
        body = newBody ?? string.Empty;
        RefreshText(false);
    }

    public void BuildIfNeeded()
    {
        if (boardRenderer == null)
        {
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "WoodenBoard";
            board.transform.SetParent(transform, false);
            board.transform.localPosition = Vector3.zero;
            board.transform.localRotation = Quaternion.identity;
            board.transform.localScale = new Vector3(boardWidth, boardHeight, boardThickness);

            Collider col = board.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            boardRenderer = board.GetComponent<Renderer>();
            if (boardRenderer != null)
            {
                LrpPrimitiveMaterialLibrary.ApplyWood(board, true);
            }
        }

        if (boardRenderer != null)
        {
            boardRenderer.transform.localScale = new Vector3(boardWidth, boardHeight, boardThickness);
        }

        if (frontText == null)
            frontText = CreateTextMesh("FrontText", new Vector3(-boardWidth * 0.47f, boardHeight * 0.37f, -boardThickness * 0.5f - 0.018f), Quaternion.identity);
        else
            ConfigureTextTransform(frontText, new Vector3(-boardWidth * 0.47f, boardHeight * 0.37f, -boardThickness * 0.5f - 0.018f), Quaternion.identity);

        if (doubleSidedText)
        {
            if (backText == null)
                backText = CreateTextMesh("BackText", new Vector3(boardWidth * 0.47f, boardHeight * 0.37f, boardThickness * 0.5f + 0.018f), Quaternion.Euler(0f, 180f, 0f));
            else
                ConfigureTextTransform(backText, new Vector3(boardWidth * 0.47f, boardHeight * 0.37f, boardThickness * 0.5f + 0.018f), Quaternion.Euler(0f, 180f, 0f));
        }
        else if (backText != null)
        {
            backText.gameObject.SetActive(false);
        }
    }

    private void ConfigureTextTransform(TextMesh text, Vector3 localPosition, Quaternion localRotation)
    {
        if (text == null) return;
        text.transform.localPosition = localPosition;
        text.transform.localRotation = localRotation;
        text.characterSize = textCharacterSize;
        text.fontSize = fontSize;
    }

    private TextMesh CreateTextMesh(string name, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(transform, false);
        textObj.transform.localPosition = localPosition;
        textObj.transform.localRotation = localRotation;

        TextMesh text = textObj.AddComponent<TextMesh>();
        text.anchor = TextAnchor.UpperLeft;
        text.alignment = TextAlignment.Left;
        text.characterSize = textCharacterSize;
        text.fontSize = fontSize;
        text.lineSpacing = 0.82f;
        text.color = new Color(0.02f, 0.018f, 0.012f, 1f);
        text.richText = false;

        Renderer renderer = textObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateTextMaterial(text);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 0;
        }

        LrpSignTextDepthGuard guard = textObj.AddComponent<LrpSignTextDepthGuard>();
        guard.boardRoot = boardRenderer != null ? boardRenderer.transform : transform;

        return text;
    }

    private Material CreateTextMaterial(TextMesh text)
    {
        // Do NOT use GUI/Text Shader here. On Quest/Unity 2019 it behaves like an
        // overlay and can render giant black font-atlas rectangles through geometry.
        // Use the font atlas as a real cutout/transparent world material instead.
        Shader shader = Shader.Find("Unlit/Transparent Cutout");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        Texture fontTexture = null;
        if (text != null && text.font != null && text.font.material != null)
            fontTexture = text.font.material.mainTexture;
        if (fontTexture != null && mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", fontTexture);

        Color ink = new Color(0.035f, 0.025f, 0.015f, 1f);
        mat.color = ink;
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", ink);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", ink);
        if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.35f);

        mat.renderQueue = textVisibleThroughWalls ? 5000 : 2450;
        if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", textVisibleThroughWalls ? (int)UnityEngine.Rendering.CompareFunction.Always : (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", textVisibleThroughWalls ? 0 : 1);

        return mat;
    }

    private void RefreshText(bool force)
    {
        if (!force && _lastTitle == title && _lastBody == body) return;
        _lastTitle = title;
        _lastBody = body;

        string text = string.IsNullOrEmpty(title) ? body : title + "\n" + body;
        if (frontText != null) frontText.text = text;
        if (backText != null) backText.text = text;
    }
}
