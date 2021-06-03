using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AvatarToRagdoll : MonoBehaviour
{
    public GameObject ragDowllToASpawn;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // When a brick has a collision, we will check to see if it's a player
    void OnTriggerEnter(Collider collision)
    {
        if (gameObject != null && collision.tag == "Brick")
        {
            ragDowllToASpawn.SetActive(true);
        }
    }

}
