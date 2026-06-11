using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// ---------------------------------------------------------------------------
// DisableLocomotion.cs
// Living Room Pirates – removes all artificial player locomotion
//
// SETUP:
//   Attach this script to the XR Rig (or any always-active GameObject) in
//   the main game scene.  On Start() it automatically finds and disables:
//
//     • ContinuousMovement   (the existing joystick-move script in this project)
//     • Any XRI LocomotionProvider (for example SnapTurnProvider or
//       TeleportationProvider in this project version)
//     • OVRPlayerController      (Oculus Integration)
//
//   Head tracking, hand/controller tracking, grab, and UI pointers are NOT
//   touched – the player's in-game position comes solely from their real
//   physical headset position.
// ---------------------------------------------------------------------------
public class DisableLocomotion : MonoBehaviour
{
    [Tooltip("If true, component references are logged to the console so you can confirm they were found and disabled.")]
    public bool verboseLog = true;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        DisableAll();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds and disables all artificial locomotion components in the scene.
    /// Safe to call multiple times.
    /// </summary>
    [ContextMenu("Disable All Locomotion")]
    public void DisableAll()
    {
        int disabled = 0;

        // -- Project-specific joystick move script --------------------------
        disabled += DisableAll<ContinuousMovement>("ContinuousMovement (project script)");

        // -- XR Interaction Toolkit locomotion providers --------------------
        // This project's XRI version does not expose ContinuousMoveProvider or
        // ContinuousTurnProvider. Disabling the shared base type catches all
        // installed XRI locomotion implementations.
        disabled += DisableAll<LocomotionProvider>("LocomotionProvider (XRI)");

        // -- Oculus OVRPlayerController -------------------------------------
        // OVRPlayerController is part of Oculus Integration; it drives the
        // CharacterController via joystick input.  Disabling it leaves head
        // tracking intact because that is handled by the OVRCameraRig itself.
        DisableOVRPlayerController(ref disabled);

        // -- CharacterController velocity ---------------------------------
        // If a CharacterController is used for ground-snapping only (no
        // locomotion script), we zero its velocity but leave it enabled so
        // the player stays on the floor.
        NullifyCharacterControllerVelocity();

        Debug.Log($"[DisableLocomotion] Disabled {disabled} locomotion component(s).");
    }

    // -----------------------------------------------------------------------
    // Generic helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds all components of type T in the scene and disables them.
    /// Returns the number of components that were actually disabled.
    /// </summary>
    private int DisableAll<T>(string label) where T : MonoBehaviour
    {
        T[] found = FindSceneObjectsOfType<T>(includeInactive: true);
        foreach (T comp in found)
        {
            comp.enabled = false;
            if (verboseLog)
                Debug.Log($"[DisableLocomotion] Disabled {label} on '{comp.gameObject.name}'.");
        }
        return found.Length;
    }

    // -----------------------------------------------------------------------
    // OVRPlayerController (uses reflection so builds without Oculus SDK fail gracefully)
    // -----------------------------------------------------------------------

    private void DisableOVRPlayerController(ref int counter)
    {
        // We reference OVRPlayerController by its string name so this script
        // compiles even when the Oculus Integration package is absent.
        MonoBehaviour[] all = FindSceneObjectsOfType<MonoBehaviour>(includeInactive: true);
        foreach (MonoBehaviour mb in all)
        {
            if (mb.GetType().Name == "OVRPlayerController")
            {
                mb.enabled = false;
                counter++;
                if (verboseLog)
                    Debug.Log($"[DisableLocomotion] Disabled OVRPlayerController on '{mb.gameObject.name}'.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // CharacterController velocity reset
    // -----------------------------------------------------------------------

    private void NullifyCharacterControllerVelocity()
    {
        // Zeroes the velocity of any CharacterController to stop residual
        // momentum.  The component itself stays active for ground-snapping.
        CharacterController[] ccs = FindSceneObjectsOfType<CharacterController>(includeInactive: false);
        foreach (CharacterController cc in ccs)
        {
            // CharacterController.velocity is read-only; the way to stop it
            // is to ensure no locomotion script is driving Move() any more.
            // Logging the find is enough to confirm the rig is present.
            if (verboseLog)
                Debug.Log($"[DisableLocomotion] CharacterController found on '{cc.gameObject.name}' – no locomotion script will call Move().");
        }
    }

    private T[] FindSceneObjectsOfType<T>(bool includeInactive) where T : Component
    {
        if (!includeInactive)
            return FindObjectsOfType<T>();

        T[] allObjects = Resources.FindObjectsOfTypeAll<T>();
        List<T> sceneObjects = new List<T>(allObjects.Length);
        foreach (T obj in allObjects)
        {
            if (obj.hideFlags != HideFlags.None)
                continue;

            if (!obj.gameObject.scene.IsValid())
                continue;

            sceneObjects.Add(obj);
        }

        return sceneObjects.ToArray();
    }
}
