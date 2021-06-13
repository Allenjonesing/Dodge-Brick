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

        // Find the XR Rig in the scene, as well as the specific VR devices
        XRRig rig = FindObjectOfType<XRRig>();
        headRig = rig.transform.Find("Camera Offset/Main Camera");
        leftHandRig = rig.transform.Find("Camera Offset/LeftHand Controller");
        rightHandRig = rig.transform.Find("Camera Offset/RightHand Controller");

        // For our own avatar only, we'll load it in
        if (photonView.IsMine)
        {
            photonView.RPC("LoadAvatar", RpcTarget.AllBuffered, PlayerPrefs.GetInt("AvatarID"), rig);
        }
    }

    //Function that is responsible to load an avatar among the avatar list
    [PunRPC]
    public void LoadAvatar(int index, XRRig rig)
    {
        // Restart fresh (If needed, in order to change the selected avatar)
        if (spawnedAvatar)
            Destroy(spawnedAvatar);

        // Select the correct avatar and init it where we want
        // rig.gameObject.transform.position = 
        spawnedAvatar = PhotonNetwork.Instantiate("Blue Avatar", rig.transform.position, rig.transform.rotation);
        AvatarInfo avatarInfo = spawnedAvatar.GetComponent<AvatarInfo>();

        // Set correct parents for position tracking
        avatarInfo.head.SetParent(head, false);
        avatarInfo.leftHand.SetParent(leftHand, false);
        avatarInfo.rightHand.SetParent(rightHand, false);

        // Apply hand animators
        leftHandAnimator = avatarInfo.leftHandAnimator;
        rightHandAnimator = avatarInfo.rightHandAnimator;

        // Remove our own avatar cause it sucks
        // spawnedAvatar.transform.Find("Character Avatar").Find("CharacterBodyAvatar").GetComponent<SkinnedMeshRenderer>().enabled = false;
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
