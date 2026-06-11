using UnityEngine;
using UnityEngine.SceneManagement;

public static class LobbyWaterBootstrap
{
    private const string LobbySceneName = "Lobby";
    private const string BootstrapRootName = "LobbyWaterTestRoot";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        LobbyOceanBootstrap.BootstrapActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LobbyOceanBootstrap.BootstrapScene(scene);
    }
}