using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;

// ---------------------------------------------------------------------------
// PirateNetworkPlayer.cs
// Living Room Pirates – pirate avatar head/hand tracking and network sync
//
// SETUP:
//   1. Attach to the "Network Player" prefab (replace or extend NetworkPlayer.cs).
//   2. Assign head, leftHand, rightHand Transforms (visual avatar bones).
//   3. The script reads XR tracking only when photonView.IsMine is true and
//      sends pose data via ShipStationNetwork.SendAvatarSync() each frame.
//   4. Remote clients receive the data via ShipStationNetwork.HandleAvatarSync()
//      and should update the pirate avatar skeleton accordingly.
//
// PHOTON:
//   • The PhotonView on this prefab uses OnPhotonSerializeView to stream the
//     head/hand transforms every network tick (continuous pose).
//   • ShipStationNetwork sends avatar sync via custom events as a backup /
//     supplementary channel.
// ---------------------------------------------------------------------------
public class PirateNetworkPlayer : MonoBehaviour, IPunObservable
{
    // -----------------------------------------------------------------------
    // Inspector fields
    // -----------------------------------------------------------------------

    [Header("Avatar Bone Targets (local visual avatar)")]
    // TODO: Assign the head Transform of the pirate avatar skeleton.
    public Transform head;

    // TODO: Assign the left-hand Transform of the pirate avatar skeleton.
    public Transform leftHand;

    // TODO: Assign the right-hand Transform of the pirate avatar skeleton.
    public Transform rightHand;

    [Header("Hand Animators")]
    // TODO: Assign the Animator on the left-hand model.
    public Animator leftHandAnimator;

    // TODO: Assign the Animator on the right-hand model.
    public Animator rightHandAnimator;

    // -----------------------------------------------------------------------
    // Private references
    // -----------------------------------------------------------------------

    private PhotonView _photonView;

    // XR rig tracking transforms (local player only)
    private Transform _headRig;
    private Transform _leftHandRig;
    private Transform _rightHandRig;

    // Received remote pose (smoothed each frame)
    private Vector3    _remoteHeadPos;
    private Quaternion _remoteHeadRot    = Quaternion.identity;
    private Vector3    _remoteLeftPos;
    private Quaternion _remoteLeftRot    = Quaternion.identity;
    private Vector3    _remoteRightPos;
    private Quaternion _remoteRightRot   = Quaternion.identity;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        _photonView = GetComponent<PhotonView>();

        if (_photonView.IsMine)
        {
            // Locate the XR Rig in the scene for tracking.
            // XRRig is the XRI component; it lives on the local player's camera rig.
            XRRig rig = FindObjectOfType<XRRig>();
            if (rig != null)
            {
                _headRig      = rig.transform.Find("Camera Offset/Main Camera");
                _leftHandRig  = rig.transform.Find("Camera Offset/LeftHand Controller");
                _rightHandRig = rig.transform.Find("Camera Offset/RightHand Controller");
            }
            else
            {
                Debug.LogWarning("[PirateNetworkPlayer] XRRig not found – head/hand tracking unavailable.");
            }

            // Tell all clients to display the correct avatar.
            // TODO: Replace 0 with PlayerPrefs.GetInt("AvatarID") if you have multiple pirate skins.
            _photonView.RPC("RPC_LoadPirateAvatar", RpcTarget.AllBuffered, 0);
        }
    }

    private void Update()
    {
        if (_photonView.IsMine)
        {
            UpdateLocalTracking();
        }
        else
        {
            ApplyRemotePose();
        }
    }

    // -----------------------------------------------------------------------
    // Local tracking
    // -----------------------------------------------------------------------

    private void UpdateLocalTracking()
    {
        if (_headRig      != null && head      != null) MapTransform(head,      _headRig);
        if (_leftHandRig  != null && leftHand  != null) MapTransform(leftHand,  _leftHandRig);
        if (_rightHandRig != null && rightHand != null) MapTransform(rightHand, _rightHandRig);

        // Animate hand models from controller input.
        UpdateHandAnimation(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand),  leftHandAnimator);
        UpdateHandAnimation(InputDevices.GetDeviceAtXRNode(XRNode.RightHand), rightHandAnimator);

        // Send to remote clients via ShipStationNetwork (unreliable – high frequency).
        if (ShipStationNetwork.Instance != null && head != null)
        {
            ShipStationNetwork.Instance.SendAvatarSync(
                head.position,      head.rotation,
                leftHand  != null ? leftHand.position  : Vector3.zero,
                leftHand  != null ? leftHand.rotation  : Quaternion.identity,
                rightHand != null ? rightHand.position : Vector3.zero,
                rightHand != null ? rightHand.rotation : Quaternion.identity
            );
        }
    }

    private static void MapTransform(Transform target, Transform source)
    {
        target.position = source.position;
        target.rotation = source.rotation;
    }

    private static void UpdateHandAnimation(InputDevice device, Animator anim)
    {
        if (anim == null) return;

        float trigger = 0f;
        float grip    = 0f;
        device.TryGetFeatureValue(CommonUsages.trigger, out trigger);
        device.TryGetFeatureValue(CommonUsages.grip,    out grip);

        anim.SetFloat("Trigger", trigger);
        anim.SetFloat("Grip",    grip);
    }

    // -----------------------------------------------------------------------
    // Remote pose application (smooth interpolation)
    // -----------------------------------------------------------------------

    private void ApplyRemotePose()
    {
        float t = Time.deltaTime * 15f; // interpolation speed

        if (head != null)
        {
            head.position = Vector3.Lerp(head.position, _remoteHeadPos, t);
            head.rotation = Quaternion.Slerp(head.rotation, _remoteHeadRot, t);
        }
        if (leftHand != null)
        {
            leftHand.position = Vector3.Lerp(leftHand.position, _remoteLeftPos, t);
            leftHand.rotation = Quaternion.Slerp(leftHand.rotation, _remoteLeftRot, t);
        }
        if (rightHand != null)
        {
            rightHand.position = Vector3.Lerp(rightHand.position, _remoteRightPos, t);
            rightHand.rotation = Quaternion.Slerp(rightHand.rotation, _remoteRightRot, t);
        }
    }

    // -----------------------------------------------------------------------
    // Photon stream (IPunObservable)
    // Called by Photon every network tick when this script is in the PhotonView's
    // Observed Components list.
    // -----------------------------------------------------------------------

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send local avatar pose.
            stream.SendNext(head      != null ? head.position      : Vector3.zero);
            stream.SendNext(head      != null ? head.rotation      : Quaternion.identity);
            stream.SendNext(leftHand  != null ? leftHand.position  : Vector3.zero);
            stream.SendNext(leftHand  != null ? leftHand.rotation  : Quaternion.identity);
            stream.SendNext(rightHand != null ? rightHand.position : Vector3.zero);
            stream.SendNext(rightHand != null ? rightHand.rotation : Quaternion.identity);
        }
        else
        {
            // Receive remote player's avatar pose.
            _remoteHeadPos  = (Vector3)stream.ReceiveNext();
            _remoteHeadRot  = (Quaternion)stream.ReceiveNext();
            _remoteLeftPos  = (Vector3)stream.ReceiveNext();
            _remoteLeftRot  = (Quaternion)stream.ReceiveNext();
            _remoteRightPos = (Vector3)stream.ReceiveNext();
            _remoteRightRot = (Quaternion)stream.ReceiveNext();
        }
    }

    // -----------------------------------------------------------------------
    // RPCs
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads and configures the pirate avatar skin on all clients.
    /// </summary>
    /// <param name="avatarIndex">Index into the pirate avatar list.</param>
    [PunRPC]
    public void RPC_LoadPirateAvatar(int avatarIndex)
    {
        // TODO: Swap the visual avatar mesh/material to the chosen pirate skin.
        // Example:
        //   Destroy(currentAvatarModel);
        //   currentAvatarModel = Instantiate(pirateAvatars[avatarIndex], transform);
        Debug.Log($"[PirateNetworkPlayer] RPC_LoadPirateAvatar called with index {avatarIndex} on {(_photonView.IsMine ? "local" : "remote")} player.");
    }
}
