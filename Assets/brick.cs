using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BzKovSoft.RagdollTemplate.Scripts.Charachter;

public class brick : MonoBehaviour
{
    public GameObject bloodParticle;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    // When a brick has a collision, we will check to see if it's a player
    void OnCollisionEnter(Collision collision)
    {
        if (gameObject != null && collision.collider.tag == "Player")
        {
            ContactPoint contact = collision.contacts[0];
            var blood = Instantiate(bloodParticle, contact.point, Quaternion.identity);
            blood.transform.parent = collision.gameObject.transform;
            var ragdoll = collision.gameObject.GetComponent<BzRagdoll>();
            if (ragdoll != null)
            {
                ragdoll.BrickCollisionDetected();
            }
        }
    }
}
