using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerGuidanceOverlay : MonoBehaviour
{
    private const string OverlayRootName = "PlayerGuidanceOverlay";

    private static PlayerGuidanceOverlay instance;

    private Camera targetCamera;
    private TextMesh titleText;
    private TextMesh bodyText;
    private TextMesh statusText;
    private string sceneName;
    private float destroyAt = -1f;
    private bool placed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // Disabled for Living Room Pirates prototype cleanup.
        // Player-facing help should be on physical ship signs, not a giant world overlay.
        PlayerGuidanceOverlay[] overlays = Resources.FindObjectsOfTypeAll<PlayerGuidanceOverlay>();
        foreach (PlayerGuidanceOverlay overlay in overlays)
        {
            if (overlay != null) Destroy(overlay.gameObject);
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureOverlayForScene(scene);
    }

    public static void SetStatus(string message)
    {
        // Overlay disabled.
    }

    public static void ClearStatus()
    {
        if (instance == null || instance.statusText == null)
            return;

        instance.statusText.text = string.Empty;
    }

    private static void EnsureOverlayForScene(Scene scene)
    {
        return;

        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (scene.name != "Lobby" && scene.name != "MainGym")
            return;

        PlayerGuidanceOverlay existing = FindExisting(scene);
        if (existing != null)
        {
            existing.RefreshPlacement();
            return;
        }

        GameObject root = new GameObject(OverlayRootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        PlayerGuidanceOverlay overlay = root.AddComponent<PlayerGuidanceOverlay>();
        overlay.sceneName = scene.name;
        overlay.Build(scene.name);
        overlay.RefreshPlacement();
    }

    private static PlayerGuidanceOverlay FindExisting(Scene scene)
    {
        PlayerGuidanceOverlay[] overlays = Resources.FindObjectsOfTypeAll<PlayerGuidanceOverlay>();
        foreach (PlayerGuidanceOverlay overlay in overlays)
        {
            if (overlay == null || overlay.gameObject.scene != scene)
                continue;

            return overlay;
        }

        return null;
    }

    private void Awake()
    {
        instance = this;
    }

    private void Update()
    {
        if (!placed)
            RefreshPlacement();

        if (destroyAt > 0f && Time.unscaledTime >= destroyAt)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build(string activeSceneName)
    {
        CreateBoardBase();

        if (activeSceneName == "Lobby")
        {
            SetContent(
                "Living Room Pirates",
                "1. Point at CONNECT and press trigger.\n2. When PLAY appears, press PLAY to enter MainGym.\n3. If connection fails, wait a moment and press CONNECT again.",
                "Status: Ready to connect.");
            return;
        }

        SetContent(
            "How To Play",
            "Grab bricks with your hands, stack them, and throw them at other players.\nUse voice chat to coordinate.\nStay aware of your real room boundary while you play.",
            "Left controller menu returns to the lobby.");
        destroyAt = Time.unscaledTime + 25f;
    }

    private void CreateBoardBase()
    {
        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
        background.name = "GuidanceBoard";
        background.transform.SetParent(transform, false);
        background.transform.localScale = new Vector3(1.6f, 0.95f, 0.04f);

        Collider boardCollider = background.GetComponent<Collider>();
        if (boardCollider != null)
            boardCollider.enabled = false;

        Renderer boardRenderer = background.GetComponent<Renderer>();
        if (boardRenderer != null)
            boardRenderer.material.color = new Color(0.07f, 0.1f, 0.15f, 0.94f);

        titleText = CreateTextObject("Title", new Vector3(0f, 0.3f, -0.03f), 0.055f, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
        bodyText = CreateTextObject("Body", new Vector3(0f, 0.02f, -0.03f), 0.034f, TextAnchor.UpperCenter, FontStyle.Normal, new Color(0.92f, 0.95f, 0.98f));
        statusText = CreateTextObject("Status", new Vector3(0f, -0.32f, -0.03f), 0.03f, TextAnchor.MiddleCenter, FontStyle.Italic, new Color(0.56f, 0.85f, 1f));

        bodyText.GetComponent<MeshRenderer>().sortingOrder = 2;
        titleText.GetComponent<MeshRenderer>().sortingOrder = 2;
        statusText.GetComponent<MeshRenderer>().sortingOrder = 2;
    }

    private TextMesh CreateTextObject(string objectName, Vector3 localPosition, float characterSize, TextAnchor anchor, FontStyle fontStyle, Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = localPosition;
        textObject.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        textMesh.characterSize = characterSize;
        textMesh.anchor = anchor;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontStyle = fontStyle;
        textMesh.fontSize = 72;
        textMesh.color = color;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        renderer.material = textMesh.font.material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return textMesh;
    }

    private void SetContent(string title, string body, string status)
    {
        titleText.text = title;
        bodyText.text = body;
        statusText.text = status;
    }

    private void SetStatusInternal(string message)
    {
        if (statusText == null)
            return;

        statusText.text = string.IsNullOrWhiteSpace(message) ? string.Empty : $"Status: {message}";
    }

    private void RefreshPlacement()
    {
        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = FindObjectOfType<Camera>();

        if (targetCamera == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 offset = sceneName == "Lobby"
            ? forward * 2.2f + right * 1.1f - Vector3.up * 0.2f
            : forward * 1.8f + right * 1.05f;

        transform.position = targetCamera.transform.position + offset;
        transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position, Vector3.up);
        placed = true;
    }
}