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
        base.OnJoinedRoom();

        if (debugText != null)
        {
            TextMesh debugLabel = debugText.GetComponent<TextMesh>();
            if (debugLabel != null)
                debugLabel.text = "Joined room. Grab bricks and throw them at rival pirates.";
        }

        PlayerGuidanceOverlay.SetStatus("You are in MainGym. Grab a brick to start playing.");

        // [spawnedPlayerPrefab] will have it's own bit of startup code
        if (spawnedPlayerPrefab == null)
            spawnedPlayerPrefab = PhotonNetwork.Instantiate("Network Player", transform.position, transform.rotation);
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        if (spawnedPlayerPrefab != null)
        {
            PhotonNetwork.Destroy(spawnedPlayerPrefab);
            spawnedPlayerPrefab = null;
        }
    }
}
