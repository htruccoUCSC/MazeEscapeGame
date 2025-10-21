using UnityEngine;
using UnityEngine.AI;

public class followAI : MonoBehaviour
{
    public NavMeshAgent ai;
    public Transform player;
    
    void Update()
    {
        ai.SetDestination(player.position);
    }
}
