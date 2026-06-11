using UnityEngine;
using UnityEngine.SceneManagement;
using LivingRoomPirates.Demo;

namespace Assets.Scripts.LivingRoomPirates
{
    public static class LivingRoomPiratesDebugBootstrap
    {
        private const string BootstrapRootName = "LivingRoomPiratesDebugRoot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            TryBootstrap(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryBootstrap(scene);
        }

        private static void TryBootstrap(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid() || !scene.isLoaded)
                return;

            BoundaryShipGenerator existingGenerator = Object.FindObjectOfType<BoundaryShipGenerator>();
            if (existingGenerator != null)
            {
                EnsureRuntimeSupport(existingGenerator.gameObject, existingGenerator);
                return;
            }

            GameObject root = new GameObject(BootstrapRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = DebugShipCenter();

            GameObject shipGeneratedRoot = new GameObject("ShipGeneratedRoot");
            shipGeneratedRoot.transform.SetParent(root.transform, false);

            GameObject environmentMotionRoot = new GameObject("EnvironmentMotionRoot");
            environmentMotionRoot.transform.SetParent(root.transform, false);

            GameObject oceanVisualRoot = new GameObject("OceanVisualRoot");
            oceanVisualRoot.transform.SetParent(environmentMotionRoot.transform, false);

            GameObject boundaryDebugRoot = new GameObject("BoundaryDebugRoot");
            boundaryDebugRoot.transform.SetParent(root.transform, false);

            BoundaryShipGenerator generator = root.AddComponent<BoundaryShipGenerator>();
            ShipStormMotionController stormController = root.AddComponent<ShipStormMotionController>();
            LivingRoomPiratesShipKeyboardDebugControls keyboardDebugControls = root.AddComponent<LivingRoomPiratesShipKeyboardDebugControls>();

            generator.shipGeneratedRoot = shipGeneratedRoot.transform;
            generator.boundaryDebugRoot = boundaryDebugRoot.transform;

            generator.safetyMargin = 0.2f;
            generator.editorFallbackWidth = 3.4f;
            generator.editorFallbackDepth = 3.4f;

            stormController.boundaryShipGenerator = generator;
            stormController.environmentMotionRoot = environmentMotionRoot.transform;
            stormController.oceanVisualRoot = oceanVisualRoot.transform;
            stormController.preset = StormMotionPreset.HeavySwell;
            stormController.stormSeed = 17;
            stormController.RefreshEnvironmentLayout();

            keyboardDebugControls.shipRoot = root.transform;
            keyboardDebugControls.boundaryShipGenerator = generator;

            EnsureRuntimeSupport(root, generator);

            Debug.Log($"[LivingRoomPiratesDebugBootstrap] Spawned debug ship bootstrap in scene '{scene.name}' at {root.transform.position}.");
        }

        private static void EnsureRuntimeSupport(GameObject root, BoundaryShipGenerator generator)
        {
            ShipStormMotionController stormController = root.GetComponent<ShipStormMotionController>();
            if (stormController == null)
            {
                stormController = root.AddComponent<ShipStormMotionController>();
            }

            stormController.boundaryShipGenerator = generator;
            if (stormController.environmentMotionRoot == null)
            {
                stormController.environmentMotionRoot = EnsureChild(root.transform, "EnvironmentMotionRoot");
            }

            if (stormController.oceanVisualRoot == null)
            {
                stormController.oceanVisualRoot = EnsureChild(stormController.environmentMotionRoot, "OceanVisualRoot");
            }

            stormController.RefreshEnvironmentLayout();

            DemoRuntimeBuilder runtimeBuilder = root.GetComponent<DemoRuntimeBuilder>();
            if (runtimeBuilder == null)
            {
                runtimeBuilder = root.AddComponent<DemoRuntimeBuilder>();
            }

            runtimeBuilder.SetShipRoot(generator.shipGeneratedRoot != null ? generator.shipGeneratedRoot : root.transform);
            runtimeBuilder.BuildRuntime();
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static Vector3 DebugShipCenter()
        {
            Camera activeCamera = Camera.main;
            if (activeCamera == null)
            {
                activeCamera = Object.FindObjectOfType<Camera>();
            }

            if (activeCamera == null)
                return Vector3.zero;

            Vector3 position = activeCamera.transform.position;
            position.y = 0f;
            return position;
        }
    }
}