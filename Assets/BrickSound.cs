using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Add this Script Directly to The Death Zone
public class BrickSound : MonoBehaviour
{
    private AudioSource source;
    public List<AudioClip> sounds;    // Add your Audi Clip Here;
                             // This Will Configure the  AudioSource Component; 
                             // MAke Sure You added AudioSouce to death Zone;
    void Start()
    {
        source = GetComponent<AudioSource>();
        var randomIndex = Random.Range(0, sounds.Count - 1);
        source.clip = sounds[randomIndex];
    }

    void OnCollisionEnter()  //Plays Sound Whenever collision detected
    {
        Debug.Log("BRICK");
        var randomIndex = (int)Random.Range(0, sounds.Count - 1);
        source.clip = sounds[randomIndex];
        source.Play(0);
        Debug.Log("started");
    }
    // Make sure that deathzone has a collider, box, or mesh.. ect..,
    // Make sure to turn "off" collider trigger for your deathzone Area;
    // Make sure That anything that collides into deathzone, is rigidbody;
}