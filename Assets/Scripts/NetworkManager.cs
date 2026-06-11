using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;

[System.Serializable]
public class DefaultRoom
{
    public string Name;
    public int sceneIndex;
    public int maxPLayer;
}

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public List<DefaultRoom> defaultRooms;
    public GameObject roomUI;

    // Scene index to load once we successfully join a room.
    private int pendingSceneIndex = -1;
    private bool isConnecting;
    private Button connectButton;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (roomUI != null)
            roomUI.SetActive(false);

        CacheConnectButton();
        PlayerGuidanceOverlay.SetStatus("Ready to connect.");
    }

    public void ConnectToServer()
    {
        if (PhotonNetwork.InRoom)
        {
            PlayerGuidanceOverlay.SetStatus("Already in a room. Loading gameplay.");
            return;
        }

        if (PhotonNetwork.IsConnected && PhotonNetwork.NetworkClientState == ClientState.JoinedLobby)
        {
            if (roomUI != null)
                roomUI.SetActive(true);

            SetConnectButtonVisible(false);
            PlayerGuidanceOverlay.SetStatus("Connected. Point at PLAY to enter MainGym.");
            return;
        }

        if (isConnecting)
        {
            PlayerGuidanceOverlay.SetStatus("Still connecting. Please wait.");
            return;
        }

        isConnecting = true;
        if (roomUI != null)
            roomUI.SetActive(false);

        SetConnectButtonVisible(false);
        PlayerGuidanceOverlay.SetStatus("Connecting to Photon...");
        PhotonNetwork.ConnectUsingSettings();
        Debug.Log("Try Connect To Server...");
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected To Server.");
        base.OnConnectedToMaster();
        PlayerGuidanceOverlay.SetStatus("Connected. Joining lobby...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();
        isConnecting = false;
        Debug.Log("WE JOINED THE LOBBY");
        if (roomUI != null)
            roomUI.SetActive(true);

        SetConnectButtonVisible(false);
        PlayerGuidanceOverlay.SetStatus("Connected. Point at PLAY to enter MainGym.");
    }

    public void InitiliazeRoom(int defaultRoomIndex)
    {
        if (defaultRoomIndex < 0 || defaultRoomIndex >= defaultRooms.Count)
        {
            PlayerGuidanceOverlay.SetStatus("Room setup is invalid. Check the room list.");
            return;
        }

        DefaultRoom roomSettings = defaultRooms[defaultRoomIndex];

        // Store the scene so we can load it after joining the room.
        // PhotonNetwork.LoadLevel requires being inside a room with
        // AutomaticallySyncScene enabled; it must not be called before
        // JoinOrCreateRoom completes.
        pendingSceneIndex = roomSettings.sceneIndex;

        //CREATE THE ROOM
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = (byte)roomSettings.maxPLayer;
        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true;

        PlayerGuidanceOverlay.SetStatus($"Joining room '{roomSettings.Name}'...");
        PhotonNetwork.JoinOrCreateRoom(roomSettings.Name, roomOptions, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined a Room");
        base.OnJoinedRoom();
        isConnecting = false;
        PlayerGuidanceOverlay.SetStatus("Joined room. Loading MainGym...");

        // Load the scene now that we are inside the room.
        if (pendingSceneIndex >= 0)
        {
            PhotonNetwork.LoadLevel(pendingSceneIndex);
            pendingSceneIndex = -1;
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log("A new player joined the room");
        base.OnPlayerEnteredRoom(newPlayer);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        base.OnDisconnected(cause);
        isConnecting = false;

        if (roomUI != null)
            roomUI.SetActive(false);

        SetConnectButtonVisible(true);
        PlayerGuidanceOverlay.SetStatus($"Disconnected: {cause}. Press CONNECT to retry.");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);
        RecoverFromRoomJoinFailure(message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        RecoverFromRoomJoinFailure(message);
    }

    private void RecoverFromRoomJoinFailure(string message)
    {
        isConnecting = false;
        pendingSceneIndex = -1;

        if (roomUI != null)
            roomUI.SetActive(false);

        SetConnectButtonVisible(true);
        PlayerGuidanceOverlay.SetStatus($"Could not join room: {message}. Press CONNECT to retry.");
    }

    private void CacheConnectButton()
    {
        if (connectButton != null)
            return;

        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in buttons)
        {
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                continue;

            if (label.text.Trim().ToUpperInvariant() != "CONNECT")
                continue;

            connectButton = button;
            break;
        }
    }

    private void SetConnectButtonVisible(bool visible)
    {
        CacheConnectButton();
        if (connectButton == null)
            return;

        connectButton.gameObject.SetActive(visible);
        connectButton.interactable = visible;
    }
}
