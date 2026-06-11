using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

// ---------------------------------------------------------------------------
// ShipStationNetwork.cs
// Living Room Pirates – Photon PUN network events for all ship stations
//
// SETUP:
//   1. Attach this script to the "NetworkedPropsRoot" GameObject (or any
//      always-active scene object).
//   2. Make sure that object also has a PhotonView.
//   3. Register this object's PhotonView in the Photon lobby/room setup so
//      it is present on every client.
//
// DESIGN:
//   Each player's ship is a different physical size, so we do NOT sync exact
//   world-space positions.  Instead we send *intent events* (what action was
//   taken, on which station, with what parameters).  Each client maps the
//   incoming event onto its own local ship layout.
//
//   Event codes are defined as byte constants below.  Use custom Photon
//   event codes in the range 1–199 (Photon reserves 200+).
// ---------------------------------------------------------------------------
public class ShipStationNetwork : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // -----------------------------------------------------------------------
    // Photon custom event codes (1–199)
    // -----------------------------------------------------------------------
    public const byte EVENT_CANNON_FIRED    = 10;
    public const byte EVENT_CANNONBALL_HIT  = 11;
    public const byte EVENT_REPAIR_ACTION   = 12;
    public const byte EVENT_ANCHOR_ACTION   = 13;
    public const byte EVENT_SAIL_ACTION     = 14;
    public const byte EVENT_AVATAR_SYNC     = 15;

    // -----------------------------------------------------------------------
    // Singleton-style reference so CannonController etc. can call us easily
    // -----------------------------------------------------------------------
    public static ShipStationNetwork Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // -----------------------------------------------------------------------
    // IOnEventCallback registration
    // -----------------------------------------------------------------------

    // MonoBehaviourPunCallbacks does not declare OnEnable/OnDisable as virtual,
    // so we use 'new' (hiding) rather than 'override' to avoid a compile error.
    public new void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public new void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // -----------------------------------------------------------------------
    // Send helpers (called by local station scripts)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Broadcast that the local player fired a cannon.
    /// </summary>
    /// <param name="shipSide">"Port", "Starboard", or "Forward".</param>
    /// <param name="stationIndex">0-based index of the cannon on that side.</param>
    /// <param name="aimYaw">World-space horizontal aim angle in degrees.</param>
    /// <param name="firePower">Normalised fire power 0–1.</param>
    public void SendCannonFired(string shipSide, int stationIndex, float aimYaw, float firePower)
    {
        object[] data = new object[]
        {
            PhotonNetwork.LocalPlayer.ActorNumber,
            shipSide,
            stationIndex,
            aimYaw,
            firePower
        };

        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EVENT_CANNON_FIRED, data, opts, SendOptions.SendReliable);

        Debug.Log($"[ShipStationNetwork] CannonFired sent – side:{shipSide} idx:{stationIndex} yaw:{aimYaw:F1} power:{firePower:F2}");
    }

    /// <summary>
    /// Broadcast that a cannonball hit something.
    /// </summary>
    /// <param name="hitType">"Water", "HullPort", "HullStarboard", "Mast", etc.</param>
    /// <param name="hitPosition">Approximate world-space impact position (local ship coords).</param>
    public void SendCannonballHit(string hitType, Vector3 hitPosition)
    {
        object[] data = new object[]
        {
            PhotonNetwork.LocalPlayer.ActorNumber,
            hitType,
            hitPosition.x,
            hitPosition.y,
            hitPosition.z
        };

        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EVENT_CANNONBALL_HIT, data, opts, SendOptions.SendReliable);

        Debug.Log($"[ShipStationNetwork] CannonballHit sent – type:{hitType} pos:{hitPosition}");
    }

    /// <summary>
    /// Broadcast a repair action (player fixed hull damage).
    /// </summary>
    /// <param name="repairStation">Name or index of the repair station used.</param>
    /// <param name="repairAmount">HP or percentage repaired.</param>
    public void SendRepairAction(string repairStation, float repairAmount)
    {
        object[] data = new object[]
        {
            PhotonNetwork.LocalPlayer.ActorNumber,
            repairStation,
            repairAmount
        };

        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EVENT_REPAIR_ACTION, data, opts, SendOptions.SendReliable);

        Debug.Log($"[ShipStationNetwork] RepairAction sent – station:{repairStation} amount:{repairAmount:F1}");
    }

    /// <summary>
    /// Broadcast that the player operated the anchor lever.
    /// </summary>
    /// <param name="isAnchored">True = dropped, false = raised.</param>
    public void SendAnchorAction(bool isAnchored)
    {
        object[] data = new object[]
        {
            PhotonNetwork.LocalPlayer.ActorNumber,
            isAnchored
        };

        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EVENT_ANCHOR_ACTION, data, opts, SendOptions.SendReliable);

        Debug.Log($"[ShipStationNetwork] AnchorAction sent – anchored:{isAnchored}");
    }

    /// <summary>
    /// Broadcast a sail adjustment (rope pull / canvas change).
    /// </summary>
    /// <param name="sailIndex">Which sail (0 = main, 1 = fore, etc.).</param>
    /// <param name="openAmount">Normalised sail openness 0–1.</param>
    public void SendSailAction(int sailIndex, float openAmount)
    {
        object[] data = new object[]
        {
            PhotonNetwork.LocalPlayer.ActorNumber,
            sailIndex,
            openAmount
        };

        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(EVENT_SAIL_ACTION, data, opts, SendOptions.SendReliable);

        Debug.Log($"[ShipStationNetwork] SailAction sent – sail:{sailIndex} open:{openAmount:F2}");
    }

    /// <summary>
    /// Broadcast this player's head and hand transforms so remote clients can
    /// update their avatar representation.  Call every frame or on a timer.
    /// </summary>
    /// <param name="headPos">Head world position.</param>
    /// <param name="headRot">Head world rotation.</param>
    /// <param name="leftHandPos">Left hand world position.</param>
    /// <param name="leftHandRot">Left hand world rotation.</param>
    /// <param name="rightHandPos">Right hand world position.</param>
    /// <param name="rightHandRot">Right hand world rotation.</param>
    public void SendAvatarSync(
        Vector3 headPos,    Quaternion headRot,
        Vector3 leftHandPos,  Quaternion leftHandRot,
        Vector3 rightHandPos, Quaternion rightHandRot)
    {
        object[] data = new object[]
        {
            PhotonNetwork.LocalPlayer.ActorNumber,
            headPos.x,    headPos.y,    headPos.z,
            headRot.x,    headRot.y,    headRot.z,    headRot.w,
            leftHandPos.x,  leftHandPos.y,  leftHandPos.z,
            leftHandRot.x,  leftHandRot.y,  leftHandRot.z,  leftHandRot.w,
            rightHandPos.x, rightHandPos.y, rightHandPos.z,
            rightHandRot.x, rightHandRot.y, rightHandRot.z, rightHandRot.w
        };

        RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        // Unreliable is fine for continuous pose data – occasional drops are invisible.
        PhotonNetwork.RaiseEvent(EVENT_AVATAR_SYNC, data, opts, SendOptions.SendUnreliable);
    }

    // -----------------------------------------------------------------------
    // Receive handler (IOnEventCallback)
    // -----------------------------------------------------------------------

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case EVENT_CANNON_FIRED:
                HandleCannonFired(photonEvent.CustomData as object[]);
                break;

            case EVENT_CANNONBALL_HIT:
                HandleCannonballHit(photonEvent.CustomData as object[]);
                break;

            case EVENT_REPAIR_ACTION:
                HandleRepairAction(photonEvent.CustomData as object[]);
                break;

            case EVENT_ANCHOR_ACTION:
                HandleAnchorAction(photonEvent.CustomData as object[]);
                break;

            case EVENT_SAIL_ACTION:
                HandleSailAction(photonEvent.CustomData as object[]);
                break;

            case EVENT_AVATAR_SYNC:
                HandleAvatarSync(photonEvent.CustomData as object[]);
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Receive handlers
    // -----------------------------------------------------------------------

    private void HandleCannonFired(object[] data)
    {
        if (data == null || data.Length < 5) return;

        int    actorNum     = (int)data[0];
        string shipSide     = (string)data[1];
        int    stationIndex = (int)data[2];
        float  aimYaw       = (float)data[3];
        float  firePower    = (float)data[4];

        Debug.Log($"[ShipStationNetwork] Remote CannonFired from actor {actorNum} – side:{shipSide} idx:{stationIndex} yaw:{aimYaw:F1} power:{firePower:F2}");

        // TODO: Trigger local VFX/SFX for the cannon fire on the remote ship.
        // Example: CannonVFXManager.Instance.PlayRemoteFire(actorNum, shipSide, stationIndex, aimYaw, firePower);
    }

    private void HandleCannonballHit(object[] data)
    {
        if (data == null || data.Length < 5) return;

        int    actorNum    = (int)data[0];
        string hitType     = (string)data[1];
        float  hx          = (float)data[2];
        float  hy          = (float)data[3];
        float  hz          = (float)data[4];
        Vector3 hitPos     = new Vector3(hx, hy, hz);

        Debug.Log($"[ShipStationNetwork] Remote CannonballHit from actor {actorNum} – type:{hitType} pos:{hitPos}");

        // TODO: Apply hull damage, spawn hit VFX.
        // Example: HullDamageManager.Instance.TakeHit(actorNum, hitType, hitPos);
    }

    private void HandleRepairAction(object[] data)
    {
        if (data == null || data.Length < 3) return;

        int    actorNum      = (int)data[0];
        string repairStation = (string)data[1];
        float  amount        = (float)data[2];

        Debug.Log($"[ShipStationNetwork] Remote RepairAction from actor {actorNum} – station:{repairStation} amount:{amount:F1}");

        // TODO: Increase local hull HP to reflect the repair.
        // Example: HullDamageManager.Instance.ApplyRepair(actorNum, repairStation, amount);
    }

    private void HandleAnchorAction(object[] data)
    {
        if (data == null || data.Length < 2) return;

        int  actorNum   = (int)data[0];
        bool isAnchored = (bool)data[1];

        Debug.Log($"[ShipStationNetwork] Remote AnchorAction from actor {actorNum} – anchored:{isAnchored}");

        // TODO: Show/hide anchor VFX on the remote player's ship representation.
        // Example: AnchorVisual.Instance.SetAnchored(actorNum, isAnchored);
    }

    private void HandleSailAction(object[] data)
    {
        if (data == null || data.Length < 3) return;

        int   actorNum   = (int)data[0];
        int   sailIndex  = (int)data[1];
        float openAmount = (float)data[2];

        Debug.Log($"[ShipStationNetwork] Remote SailAction from actor {actorNum} – sail:{sailIndex} open:{openAmount:F2}");

        // TODO: Animate the sail on the remote representation.
        // Example: SailManager.Instance.SetRemoteSail(actorNum, sailIndex, openAmount);
    }

    private void HandleAvatarSync(object[] data)
    {
        if (data == null || data.Length < 22) return;

        int actorNum = (int)data[0];

        Vector3    headPos    = new Vector3((float)data[1],  (float)data[2],  (float)data[3]);
        Quaternion headRot    = new Quaternion((float)data[4],  (float)data[5],  (float)data[6],  (float)data[7]);
        Vector3    leftPos    = new Vector3((float)data[8],  (float)data[9],  (float)data[10]);
        Quaternion leftRot    = new Quaternion((float)data[11], (float)data[12], (float)data[13], (float)data[14]);
        Vector3    rightPos   = new Vector3((float)data[15], (float)data[16], (float)data[17]);
        Quaternion rightRot   = new Quaternion((float)data[18], (float)data[19], (float)data[20], (float)data[21]);

        // TODO: Update the remote player's pirate avatar transforms.
        // Example: PirateAvatarManager.Instance.UpdateRemoteAvatar(actorNum, headPos, headRot, leftPos, leftRot, rightPos, rightRot);
    }
}
