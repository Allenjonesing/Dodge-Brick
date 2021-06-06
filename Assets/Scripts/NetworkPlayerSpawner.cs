using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using System;

public class NetworkPlayerSpawner : MonoBehaviourPunCallbacks
{
    private GameObject spawnedPlayerPrefab;
    public GameObject debugText;

    public override void OnJoinedRoom()
    {
        // When the network player joins, we create their [spawnedPlayerPrefab] at the transform.position.
        debugText.GetComponent<TextMesh>().text = "A Player Joined The Room!";
        base.OnJoinedRoom();
        // [spawnedPlayerPrefab] will have it's own bit of startup code
        spawnedPlayerPrefab = PhotonNetwork.Instantiate("Network Player", transform.position, transform.rotation);
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        PhotonNetwork.Destroy(spawnedPlayerPrefab);
    }
}
