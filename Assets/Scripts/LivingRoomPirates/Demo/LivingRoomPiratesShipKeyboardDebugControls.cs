using UnityEngine;

/// <summary>
/// Legacy keyboard helper kept only so old scene references do not break.
/// It used to move/rotate the ship with WASD/Q/E, which is forbidden for
/// Living Room Pirates. Water movement is now handled by
/// LivingRoomPirates.Demo.LivingRoomPiratesSurfaceDebugSandbox.
/// </summary>
public class LivingRoomPiratesShipKeyboardDebugControls : MonoBehaviour
{
    public BoundaryShipGenerator boundaryShipGenerator;
    public Transform shipRoot;
    public bool showOverlay = false;

    private void OnEnable()
    {
        showOverlay = false;
        enabled = false;
    }
}
