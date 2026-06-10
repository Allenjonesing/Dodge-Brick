using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;

// ---------------------------------------------------------------------------
// CannonController.cs
// Living Room Pirates – single cannon that fires locally and broadcasts via
// ShipStationNetwork so all remote clients can react.
//
// SETUP (Inspector):
//   1. Attach this script to a cannon prefab.
//   2. Assign firePoint (the empty Transform at the barrel mouth).
//   3. Assign cannonballPrefab – a Rigidbody prefab to launch.
//   4. Assign the AudioSource for the fire sound (optional).
//   5. Set shipSide ("Forward", "Port", or "Starboard") and stationIndex.
//   6. Optionally assign fireVFX (particle system at the barrel).
//
// INTERACTION:
//   The cannon can be fired by:
//     a) A grab-and-trigger interaction: add an XRSimpleInteractable to the
//        cannon and hook its "Activated" event to Fire().
//     b) Calling Fire() directly from another script.
// ---------------------------------------------------------------------------
public class CannonController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector fields
    // -----------------------------------------------------------------------

    [Header("Station Identity")]
    [Tooltip("Which side of the ship this cannon is on: Forward, Port, Starboard.")]
    public string shipSide = "Forward";

    [Tooltip("0-based index among cannons on the same side (used in network events).")]
    public int stationIndex = 0;

    [Header("Cannon Setup")]
    // TODO: Assign the Transform at the cannon barrel mouth in the Inspector.
    [Tooltip("Transform at the barrel mouth – cannonballs spawn here and travel forward.")]
    public Transform firePoint;

    // TODO: Assign a Rigidbody-based cannonball prefab in the Inspector.
    [Tooltip("Prefab of the cannonball (must have a Rigidbody).")]
    public GameObject cannonballPrefab;

    [Tooltip("Force applied to the cannonball on fire (Newtons).")]
    public float fireForce = 15f;

    [Tooltip("Normalised fire power sent over the network (0–1). Adjust per cannon.")]
    [Range(0f, 1f)]
    public float firePower = 0.8f;

    [Header("Optional")]
    // TODO: Assign a particle system for the muzzle flash VFX in the Inspector.
    [Tooltip("Muzzle flash / smoke particle system (optional).")]
    public ParticleSystem fireVFX;

    // TODO: Assign an AudioSource with the cannon-fire clip in the Inspector.
    [Tooltip("AudioSource for the fire sound effect (optional).")]
    public AudioSource fireAudio;

    [Tooltip("Seconds between allowed shots (cooldown).")]
    public float fireCooldown = 2f;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private float _lastFireTime = -999f;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Fire the cannon locally and broadcast the event to all remote clients.
    /// Call this from an XR interaction "Activated" event or directly.
    /// </summary>
    public void Fire()
    {
        if (Time.time - _lastFireTime < fireCooldown)
        {
            Debug.Log($"[CannonController] {name} is on cooldown.");
            return;
        }
        _lastFireTime = Time.time;

        // --- Local fire --------------------------------------------------

        if (firePoint == null)
        {
            Debug.LogWarning($"[CannonController] {name}: firePoint not assigned – cannot spawn cannonball.");
        }
        else if (cannonballPrefab == null)
        {
            Debug.LogWarning($"[CannonController] {name}: cannonballPrefab not assigned – cannot spawn cannonball.");
        }
        else
        {
            // Spawn locally (not via PhotonNetwork.Instantiate – cannonball
            // physics are purely local; only the FIRE EVENT is networked).
            GameObject ball = Instantiate(cannonballPrefab, firePoint.position, firePoint.rotation);
            Rigidbody  rb   = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(firePoint.forward * fireForce, ForceMode.Impulse);
            }

            // Auto-destroy cannonball after 5 seconds to avoid clutter.
            Destroy(ball, 5f);
        }

        // VFX / SFX
        if (fireVFX  != null) fireVFX.Play();
        if (fireAudio != null) fireAudio.Play();

        // --- Network event -----------------------------------------------

        float aimYaw = firePoint != null
            ? firePoint.eulerAngles.y
            : transform.eulerAngles.y;

        if (ShipStationNetwork.Instance != null)
        {
            ShipStationNetwork.Instance.SendCannonFired(shipSide, stationIndex, aimYaw, firePower);
        }
        else
        {
            Debug.LogWarning("[CannonController] ShipStationNetwork.Instance is null – fire event not sent.");
        }

        Debug.Log($"[CannonController] {name} fired. side:{shipSide} idx:{stationIndex} yaw:{aimYaw:F1}");
    }

    // -----------------------------------------------------------------------
    // XR interaction hook
    // -----------------------------------------------------------------------

    /// <summary>
    /// Convenience method matching the XRSimpleInteractable.Activated signature
    /// so it can be wired directly in the Inspector.
    /// </summary>
    public void OnActivated(ActivateEventArgs args)
    {
        Fire();
    }
}
