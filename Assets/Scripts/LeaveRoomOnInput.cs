using UnityEngine.XR;
using Photon.Pun;
using UnityEngine;

public class LeaveRoomOnInput : MonoBehaviourPunCallbacks
{
    public InputHelpers.Button inputHelpers = InputHelpers.Button.MenuButton;
    public XRNode controller = XRNode.LeftHand;
    private bool wasPressed;
    private bool isLeaving;
    
    void Update()
    {
        InputHelpers.IsPressed(InputDevices.GetDeviceAtXRNode(controller), inputHelpers, out bool isPressed);

        if (!isLeaving && isPressed && !wasPressed)
        {
            isLeaving = true;
            PlayerGuidanceOverlay.SetStatus("Returning to lobby...");

            if (PhotonNetwork.IsConnected)
                PhotonNetwork.Disconnect();

            PhotonNetwork.LoadLevel(0);
        }

        wasPressed = isPressed;
    }
}
