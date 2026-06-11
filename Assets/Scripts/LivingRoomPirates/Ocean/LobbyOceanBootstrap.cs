using UnityEngine;
using UnityEngine.SceneManagement;

public static class LobbyOceanBootstrap
{
    private const string LobbySceneName = "Lobby";
    private const string RootName = "LivingRoomPiratesOceanSystem";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        BootstrapScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BootstrapScene(scene);
    }

    public static void BootstrapActiveScene()
    {
        BootstrapScene(SceneManager.GetActiveScene());
    }

    public static void BootstrapScene(Scene scene)
    {
        if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (scene.name != LobbySceneName)
        {
            return;
        }

        GameObject waterOne = GameObject.Find("Water1");
        GameObject waterTwo = GameObject.Find("Water2");

        if (waterOne == null && waterTwo == null)
        {
            Debug.LogWarning("[LobbyOceanBootstrap] No Water1 or Water2 found.");
            return;
        }

        GameObject root = GameObject.Find(RootName);

        if (root == null)
        {
            root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        Transform oceanWorldRoot = EnsureChild(root.transform, "OceanWorldRoot");
        Transform shipVisualRoot = EnsureChild(root.transform, "ShipVisualRoot");

        Reparent(waterOne, oceanWorldRoot);
        Reparent(waterTwo, oceanWorldRoot);

        OceanWorldController controller = root.GetComponent<OceanWorldController>();

        if (controller == null)
        {
            controller = root.AddComponent<OceanWorldController>();
        }

        controller.oceanWorldRoot = oceanWorldRoot;
        controller.waterOne = waterOne != null ? waterOne.transform : null;
        controller.waterTwo = waterTwo != null ? waterTwo.transform : null;
        controller.visualShipRoot = shipVisualRoot;
        controller.preset = StormMotionPreset.HeavySwell;
        controller.seed = 17;
        controller.ApplyPreset();

        ConfigureWaterOne(waterOne);
        ConfigureWaterTwo(waterTwo);

        Debug.Log("[LobbyOceanBootstrap] Living Room Pirates ocean system enabled.");
    }

    private static void ConfigureWaterOne(GameObject water)
    {
        if (water == null)
        {
            return;
        }

        DisableCollider(water);

        OceanWaveMeshDeformer deformer = water.GetComponent<OceanWaveMeshDeformer>();

        if (deformer == null)
        {
            deformer = water.AddComponent<OceanWaveMeshDeformer>();
        }

        deformer.useControllerSettings = true;
    }

    private static void ConfigureWaterTwo(GameObject water)
    {
        if (water == null)
        {
            return;
        }

        DisableCollider(water);

        OceanWaveMeshDeformer deformer = water.GetComponent<OceanWaveMeshDeformer>();

        if (deformer != null)
        {
            Object.Destroy(deformer);
        }
    }

    private static void DisableCollider(GameObject obj)
    {
        Collider collider = obj.GetComponent<Collider>();

        if (collider != null)
        {
            collider.enabled = false;
        }
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);

        if (child != null)
        {
            return child;
        }

        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private static void Reparent(GameObject obj, Transform parent)
    {
        if (obj == null)
        {
            return;
        }

        obj.SetActive(true);
        obj.transform.SetParent(parent, true);
    }
}
