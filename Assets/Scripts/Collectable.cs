using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Collectable : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The amount to add to the magic circle's size when collected.")]
    public float sizeIncreaseAmount = 0.5f;

    [Header("Feedback")]
    [Tooltip("The sound to play on pickup. (Optional)")]
    public AudioClip pickupSound;

    [Tooltip("A particle effect to play on pickup. (Optional)")]
    public GameObject pickupEffect;

    // Finds and stores the AI script here
    private LeadAI dogAI;

    // Prevents the player from collecting it twice
    private bool isCollected = false;

    void Start()
    {
        // Finds the dog AI in the scene when the game starts.
        dogAI = FindFirstObjectByType<LeadAI>();

        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        // If it's already collected, or if the object that touched it is not the player, does nothing.
        if (isCollected || !other.CompareTag("Player"))
        {
            return;
        }

        isCollected = true;

        // If the dog AI was found, increase its magic circle size
        if (dogAI != null)
        {
            dogAI.IncreaseMagicCircle(sizeIncreaseAmount);
        }

        // If audio clip is assigned, play it at the collectable's position
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        // If a pickup effect is assigned, play it at the collectable's position
        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, Quaternion.identity);
        }

        // Destroys the collectable object
        Destroy(gameObject);
    }
}
