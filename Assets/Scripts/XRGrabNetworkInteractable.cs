using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;

// XRGrabNetworkInteractable extends XRGrabInteractable with Photon network sync.
// Attach this (instead of XRGrabInteractable) to any throwable/grabbable prefab that
// also has a PhotonView. Make sure to add this component to the PhotonView's
// "Observed Components" list in the Unity Inspector so that OnPhotonSerializeView
// is called by Photon every network tick.
public class XRGrabNetworkInteractable : XRGrabInteractable, IPunObservable
{
    private PhotonView photonView;
    private Rigidbody rb;

    // Start is called before the first frame update
    void Start()
    {
        photonView = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody>();
    }

    // When a player grabs this object, transfer ownership to them so their
    // client becomes the authority for position updates.
    protected override void OnSelectEnter(XRBaseInteractor interactor)
    {
        photonView.RequestOwnership();
        base.OnSelectEnter(interactor);
    }

    // Called by Photon every network update tick.
    // The owner streams position/rotation/physics outward; all other clients
    // receive and apply the values so they see the object move in real time.
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(rb != null ? rb.velocity : Vector3.zero);
            stream.SendNext(rb != null ? rb.angularVelocity : Vector3.zero);
        }
        else
        {
            transform.position = (Vector3)stream.ReceiveNext();
            transform.rotation = (Quaternion)stream.ReceiveNext();
            Vector3 vel = (Vector3)stream.ReceiveNext();
            Vector3 angVel = (Vector3)stream.ReceiveNext();
            if (rb != null)
            {
                rb.velocity = vel;
                rb.angularVelocity = angVel;
            }
        }
    }
}
