using UnityEngine;

/// <summary>
/// Legacy bootstrap disabled. The current no-prefab workflow is installed by
/// LivingRoomPirates.Demo.LivingRoomPiratesPrimitiveSceneInstaller instead.
/// Keeping this type prevents missing-script references while avoiding duplicate
/// debug menus, old WASD ship movement, and old enemy/Water2 setup.
/// </summary>
public static class LivingRoomPiratesDebugBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // Intentionally no-op.
    }
}
