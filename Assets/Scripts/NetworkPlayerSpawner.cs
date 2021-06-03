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
        debugText.GetComponent<TextMesh>().text = "Joined Room!";
        base.OnJoinedRoom();
        debugText.GetComponent<TextMesh>().text = "OnJoinedRoom!";
        spawnedPlayerPrefab = PhotonNetwork.Instantiate("Network Player", transform.position, transform.rotation);
        debugText.GetComponent<TextMesh>().text = "spawnedPlayerPrefab created!";
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();
        PhotonNetwork.Destroy(spawnedPlayerPrefab);
    }
}
