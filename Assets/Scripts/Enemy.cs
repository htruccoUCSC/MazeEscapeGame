using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    public Transform player;                    // Direct reference to the player (assign in Inspector)
    public Transform[] waypoints;               // Assign empty GameObjects in Inspector
    public AudioClip footstepClip;              // Placeholder footstep clip
    public AudioClip alertClip;                 // Placeholder alert clip

    [Header("Vision")]
    public float viewDistance = 12f;
    [Range(1, 180)] public float viewAngle = 90f;
    public LayerMask obstructionMask;           // Layers that block sight (assign in Inspector)

    [Header("Behavior")]
    public float loseSightDelay = 5f;           // seconds until giving up chase
    public float attackDistance = 1.5f;         // distance at which player is "reached" and destroyed

    [Header("Cinematic")]
    public Camera playerCamera;                 // Optional fallback if player Transform isn't available
    public float focusDuration = 0.5f;          // how long the player rotates to face the enemy (first sight)
    public float particleDelay = 1f;            // delay after sight to enable particle object
    public GameObject alertParticleObject;      // assign the Magic circle GameObject (child of enemy) in Inspector

    NavMeshAgent _agent;
    AudioSource _footstepSource;
    AudioSource _oneShotSource;

    enum State { Patrolling, Chasing }
    State _state = State.Patrolling;

    int _currentWaypointIndex = -1;
    float _lastSeenTime = -Mathf.Infinity;

    // cinematic state
    bool _cinematicTriggered = false; // ensures rotation cinematic runs only the first time
    Coroutine _cinematicCoroutine;

    // track active disable coroutine so repeated calls reset timing
    Coroutine _particleDisableCoroutine;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent == null)
            _agent = gameObject.AddComponent<NavMeshAgent>();

        // Create/manage audio sources (footsteps loops while moving; one-shot for alert)
        _footstepSource = gameObject.AddComponent<AudioSource>();
        _footstepSource.clip = footstepClip;
        _footstepSource.loop = true;
        _footstepSource.playOnAwake = false;

        _oneShotSource = gameObject.AddComponent<AudioSource>();
        _oneShotSource.playOnAwake = false;
        _oneShotSource.loop = false;

        // Safety checks
        if (player == null)
            Debug.LogWarning("Enemy: player reference not set in Inspector.");

        if (waypoints == null || waypoints.Length == 0)
            Debug.LogWarning("Enemy: no waypoints assigned. Assign some empty GameObjects to waypoints.");

        // Try to auto-find the player's camera if not assigned
        if (playerCamera == null && player != null)
            playerCamera = player.GetComponentInChildren<Camera>();

        // Ensure the particle object (if assigned) is disabled on start so it only appears when chasing
        if (alertParticleObject != null)
            alertParticleObject.SetActive(false);

        // Start patrolling immediately
        PickNextWaypoint();
    }

    void Update()
    {
        if (player != null)
            UpdateVision();

        switch (_state)
        {
            case State.Patrolling:
                PatrolUpdate();
                break;
            case State.Chasing:
                ChaseUpdate();
                break;
        }

        UpdateFootsteps();
    }

    void UpdateVision()
    {
        Vector3 dirToPlayer = (player.position - transform.position);
        float dist = dirToPlayer.magnitude;

        bool inDistance = dist <= viewDistance;
        bool inAngle = Vector3.Angle(transform.forward, dirToPlayer.normalized) <= (viewAngle * 0.5f);
        bool obstructed = false;

        if (inDistance && inAngle)
        {
            // Raycast to check line of sight
            Ray ray = new Ray(transform.position + Vector3.up * 0.5f, dirToPlayer.normalized);
            if (Physics.Raycast(ray, out RaycastHit hit, viewDistance, ~0))
            {
                // If something hit and it's not the player, check layer mask for obstruction
                if (hit.transform != player)
                {
                    if (((1 << hit.collider.gameObject.layer) & obstructionMask) != 0)
                        obstructed = true;
                }
            }

            if (!obstructed)
            {
                // Player seen
                _lastSeenTime = Time.time;

                // If transitioning into chasing, play alert and start particle sequence (delayed)
                if (_state != State.Chasing)
                {
                    _state = State.Chasing;
                    _oneShotSource.PlayOneShot(alertClip);

                    // First sight ever: rotate the entire player then spawn particle after particleDelay.
                    // Later sightings: do not rotate player, but still spawn the particle after particleDelay.
                    if (!_cinematicTriggered)
                    {
                        _cinematicTriggered = true;
                        if (_cinematicCoroutine != null)
                            StopCoroutine(_cinematicCoroutine);
                        _cinematicCoroutine = StartCoroutine(RotatePlayerThenEnableParticle());
                    }
                    else
                    {
                        // Subsequent sightings — enable particle after particleDelay (no rotation)
                        StartCoroutine(EnableParticleDelayed());
                    }
                }
            }
        }
    }

    void PatrolUpdate()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        if (_agent.pathPending)
            return;

        if (_agent.remainingDistance <= _agent.stoppingDistance + 0.2f)
        {
            PickNextWaypoint();
        }
    }

    void ChaseUpdate()
    {
        if (player == null)
        {
            // No player to chase; return to patrol
            _state = State.Patrolling;
            PickNextWaypoint();
            return;
        }

        _agent.SetDestination(player.position);

        // If close enough to player, destroy them
        if (Vector3.Distance(transform.position, player.position) <= attackDistance)
        {
            Destroy(player.gameObject);
            _agent.ResetPath();
            _state = State.Patrolling;
            PickNextWaypoint();
            return;
        }

        // If we haven't seen player for loseSightDelay seconds, give up and patrol
        if (Time.time - _lastSeenTime > loseSightDelay)
        {
            _state = State.Patrolling;
            PickNextWaypoint();
        }
    }

    void PickNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        // Pick a random waypoint (could be same as current)
        _currentWaypointIndex = UnityEngine.Random.Range(0, waypoints.Length);
        var wp = waypoints[_currentWaypointIndex];
        if (wp != null)
            _agent.SetDestination(wp.position);
    }

    void UpdateFootsteps()
    {
        // Consider moving if NavMeshAgent has velocity and a path
        bool isMoving = _agent.velocity.sqrMagnitude > 0.01f && _agent.hasPath;

        if (isMoving)
        {
            if (!_footstepSource.isPlaying && footstepClip != null)
                _footstepSource.Play();
        }
        else
        {
            if (_footstepSource.isPlaying)
                _footstepSource.Stop();
        }
    }

    IEnumerator RotatePlayerThenEnableParticle()
    {
        // Rotate the whole player to face the enemy (first sight cinematic).
        if (player != null)
        {
            Vector3 lookTarget = transform.position;
            Vector3 dir = (lookTarget - player.position);
            if (dir.sqrMagnitude > 0.0001f)
            {
                // Calculate the target rotation only on the Y-axis
                Quaternion startRot = player.rotation;
                Quaternion targetRotation = Quaternion.Euler(0, Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y, 0);

                float elapsed = 0f;
                while (elapsed < focusDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / focusDuration);
                    player.rotation = Quaternion.Slerp(startRot, targetRotation, t);
                    yield return null;
                }

                player.rotation = targetRotation;
            }
        }
        else if (playerCamera != null)
        {
            // fallback: rotate camera if player transform is not available
            Vector3 lookTarget = transform.position + Vector3.up * 1f;
            Quaternion startRot = playerCamera.transform.rotation;
            Quaternion targetRotation = Quaternion.LookRotation((lookTarget - playerCamera.transform.position).normalized);

            float elapsed = 0f;
            while (elapsed < focusDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / focusDuration);
                playerCamera.transform.rotation = Quaternion.Slerp(startRot, targetRotation, t);
                yield return null;
            }

            playerCamera.transform.rotation = targetRotation;
        }

        // Wait the configured delay then enable the particle object so it follows the enemy
        yield return new WaitForSeconds(particleDelay);

        EnableParticleObject();

        _cinematicCoroutine = null;
    }

    IEnumerator EnableParticleDelayed()
    {
        yield return new WaitForSeconds(particleDelay);
        EnableParticleObject();
    }

    void EnableParticleObject()
    {
        if (alertParticleObject == null)
            return;

        // If an existing disable coroutine is running, stop it so we can restart the lifetime
        if (_particleDisableCoroutine != null)
        {
            StopCoroutine(_particleDisableCoroutine);
            _particleDisableCoroutine = null;
        }

        // Activate the particle object (it should be a child of the enemy so it follows automatically)
        alertParticleObject.SetActive(true);

        // Play all ParticleSystems under the object (restart if already playing)
        var systems = alertParticleObject.GetComponentsInChildren<ParticleSystem>(true);
        float maxDuration = 0f;
        foreach (var ps in systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
            // approximate lifetime
            var main = ps.main;
            float dur = main.duration + main.startLifetime.constantMax;
            if (dur > maxDuration) maxDuration = dur;
        }

        // Schedule disabling after the longest particle finishes
        if (maxDuration <= 0f)
            maxDuration = 2f; // fallback
        _particleDisableCoroutine = StartCoroutine(DisableParticleObjectAfter(maxDuration + 0.1f));
    }

    IEnumerator DisableParticleObjectAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (alertParticleObject != null)
            alertParticleObject.SetActive(false);
        _particleDisableCoroutine = null;
    }

    void OnDrawGizmosSelected()
    {
        // Visualize view cone
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        Vector3 forward = transform.forward;
        Quaternion leftRot = Quaternion.Euler(0, -viewAngle * 0.5f, 0);
        Quaternion rightRot = Quaternion.Euler(0, viewAngle * 0.5f, 0);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + leftRot * forward * viewDistance);
        Gizmos.DrawLine(transform.position, transform.position + rightRot * forward * viewDistance);
    }
}