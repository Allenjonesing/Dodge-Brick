using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;

public class NetworkPlayer : MonoBehaviour
{
    public List<GameObject> avatars;

    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    public Animator leftHandAnimator;
    public Animator rightHandAnimator;

    private PhotonView photonView;

    private Transform headRig;
    private Transform leftHandRig;
    private Transform rightHandRig;
    private GameObject spawnedAvatar;

    // Start is called before the first frame update
    void Start()
    {
        // We've just been created at the NetworkManager's position.
        photonView = GetComponent<PhotonView>();

        // Find the XR Rig in the scene, as well as the specific VR devices.
        // Only the local (owning) player has a physical XR rig to track.
        if (photonView.IsMine)
        {
            XRRig rig = FindObjectOfType<XRRig>();
            headRig = rig.transform.Find("Camera Offset/Main Camera");
            leftHandRig = rig.transform.Find("Camera Offset/LeftHand Controller");
            rightHandRig = rig.transform.Find("Camera Offset/RightHand Controller");

            // Load the avatar for this player on all clients.
            // NOTE: XRRig is NOT passed as a parameter because Photon cannot
            // serialize MonoBehaviour objects across the network; it is
            // looked up locally instead.
            photonView.RPC("LoadAvatar", RpcTarget.AllBuffered, PlayerPrefs.GetInt("AvatarID"));
        }
    }

    // Function that is responsible to load an avatar among the avatar list.
    // Runs on all clients so each client can parent the avatar's bones to the
    // NetworkPlayer's tracked head/hand transforms.
    [PunRPC]
    public void LoadAvatar(int index)
    {
        // Restart fresh (If needed, in order to change the selected avatar)
        if (spawnedAvatar)
            Destroy(spawnedAvatar);

        // Only the owner calls PhotonNetwork.Instantiate; Photon's replication
        // then creates the avatar prefab on all remote clients automatically.
        if (photonView.IsMine)
        {
            XRRig rig = FindObjectOfType<XRRig>();
            spawnedAvatar = PhotonNetwork.Instantiate("Blue Avatar", rig.transform.position, rig.transform.rotation);
            AvatarInfo avatarInfo = spawnedAvatar.GetComponent<AvatarInfo>();

            // Set correct parents for position tracking
            avatarInfo.head.SetParent(head, false);
            avatarInfo.leftHand.SetParent(leftHand, false);
            avatarInfo.rightHand.SetParent(rightHand, false);

            // Apply hand animators
            leftHandAnimator = avatarInfo.leftHandAnimator;
            rightHandAnimator = avatarInfo.rightHandAnimator;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(photonView.IsMine)
        {          
            MapPosition(head, headRig);
            MapPosition(leftHand, leftHandRig);
            MapPosition(rightHand, rightHandRig);

            UpdateHandAnimation(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), leftHandAnimator);
            UpdateHandAnimation(InputDevices.GetDeviceAtXRNode(XRNode.RightHand), rightHandAnimator);
        }
      
    }

    void UpdateHandAnimation(InputDevice targetDevice, Animator handAnimator)
    {
        if (!handAnimator)
            return;

        if (targetDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            handAnimator.SetFloat("Trigger", triggerValue);
        }
        else
        {
            handAnimator.SetFloat("Trigger", 0);
        }

        if (targetDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue))
        {
            handAnimator.SetFloat("Grip", gripValue);
        }
        else
        {
            handAnimator.SetFloat("Grip", 0);
        }
    }


    void MapPosition(Transform target,Transform rigTransform)
    {    
        target.position = rigTransform.position;
        target.rotation = rigTransform.rotation;
    }
}
