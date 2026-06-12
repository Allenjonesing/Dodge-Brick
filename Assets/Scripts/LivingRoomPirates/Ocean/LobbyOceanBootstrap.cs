using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Legacy ocean bootstrap disabled.
/// The active workflow is LivingRoomPirates.Demo.LivingRoomPiratesPrimitiveSceneInstaller.
///
/// Important: Water1 and Water2 are now scene-authored objects. This class must not
/// create roots, move water, apply presets, or add a second OceanWorldController.
/// It remains only so old references compile.
/// </summary>
public static class LobbyOceanBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // Intentionally no-op. The primitive scene installer owns all setup.
    }

    public static void BootstrapActiveScene()
    {
        // Intentionally no-op.
    }

    public static void BootstrapScene(Scene scene)
    {
        // Intentionally no-op.
    }
}
