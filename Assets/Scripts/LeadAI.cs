using UnityEngine;
using UnityEngine.AI;

// This checks to makesure our GameObject has the components it needs
[RequireComponent(typeof(NavMeshAgent), typeof(AudioSource))]
public class LeadAI : MonoBehaviour
{
    // Core Components
    private NavMeshAgent ai;
    private AudioSource audioSource;
    public Transform player;

    [Header("Path Settings")]
    [Tooltip("An array of Transforms (empty GameObjects) for the AI to follow in order.")]
    public Transform[] waypoints;
    private int currentWaypointIndex = 0;

    [Header("Behavior Settings")]
    [Tooltip("The max distance the player can be before the AI stops and calls them back.")]
    public float maxPlayerDistance = 10.0f;
    [Tooltip("The distance the player must be within for the AI to resume leading.")]
    public float resumePathDistance = 5.0f;

    [Header("Audio Settings")]
    [Tooltip("The sound to play when the player is too far.")]
    public AudioClip attentionSound;

    [Header("Particle Settings")]
    [Tooltip("The particle system that can grow when gathering collectables.")]
    public ParticleSystem magicCircle;

    // Internal state to track what the AI is doing
    private bool isWaitingForPlayer = false;

    void Awake()
    {
        // This gets the components attached to this GameObject
        ai = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();

        // AudioSource setup
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1.0f;
    }

    void Start()
    {
        // Finds the player
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        // Sets up the initial destination
        if (waypoints.Length > 0)
        {
            // Tells the AI to go to the first point
            ai.SetDestination(waypoints[currentWaypointIndex].position);
        }

        // Ensures the "resume" distance is smaller than the "max" distance
        if (resumePathDistance >= maxPlayerDistance)
        {
            resumePathDistance = maxPlayerDistance - 1.0f;
        }
    }

    void Update()
    {
        // Doesn't do anything if it doesn't have a player or a path to lead/follow
        if (player == null || waypoints.Length == 0) return;

        // Checks the distance to the player
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Checks if the player went too far
        if (distanceToPlayer > maxPlayerDistance && !isWaitingForPlayer)
        {
            isWaitingForPlayer = true;
            PlayAttentionSound();
        }
        // Checks if the player is back in range
        else if (distanceToPlayer < resumePathDistance && isWaitingForPlayer)
        {
            isWaitingForPlayer = false;
            StopAttentionSound();
        }

        if (isWaitingForPlayer)
        {
            // Stops following the path and goes back to the player
            ai.SetDestination(player.position);
        }
        else
        {
            // Checks if we've reached the current waypoint
            if (!ai.pathPending && ai.remainingDistance <= ai.stoppingDistance)
            {
                GoToNextWaypoint();
            }

            // Always updates the destination in case the waypoint moves or we switched from waiting
            ai.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    private void GoToNextWaypoint()
    {
        // We cycle to the next waypoint, looping back to the start at the end of the maze
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    private void PlayAttentionSound()
    {
        // Loops the sound until the player comes back or the dog catches up to the player
        if (attentionSound != null && !audioSource.isPlaying)
        {
            audioSource.clip = attentionSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    private void StopAttentionSound()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }
    }

    // Increases the particle system's size.
    public void IncreaseMagicCircle(float amountToIncrease)
    {
        Transform circleTransform = magicCircle.transform;

        Vector3 scaleIncrease = new Vector3(amountToIncrease, amountToIncrease, amountToIncrease);

        circleTransform.localScale += scaleIncrease;
    }
}