using UnityEngine;

namespace Assets.Scripts.LivingRoomPirates
{
    /// <summary>
    /// Legacy duplicate bootstrap disabled. The active installer is
    /// LivingRoomPirates.Demo.LivingRoomPiratesPrimitiveSceneInstaller.
    /// </summary>
    public static class LivingRoomPiratesDebugBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            // Intentionally no-op.
        }
    }
}
