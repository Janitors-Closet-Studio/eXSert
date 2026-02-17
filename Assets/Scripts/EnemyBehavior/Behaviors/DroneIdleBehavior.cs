// DroneIdleBehavior.cs
// Purpose: Idle behavior for drones within a swarm, optionally triggers relocation.
// Works with: DroneSwarmManager, DroneRelocateBehavior.

using Behaviors;
using UnityEngine;

public class DroneIdleBehavior<TState, TTrigger> : IdleBehavior<TState, TTrigger>
    where TState : struct, System.Enum
    where TTrigger : struct, System.Enum
{
    // Tick interval for idle behavior - don't run every frame
    // Increased to reduce SetDestination calls which cause memory leaks
    private const float TickInterval = 0.5f;

    /// <summary>
    /// Returns true if this drone is the leader of its cluster (first drone in the list).
    /// Only the leader should run cluster-wide logic to prevent duplicate operations.
    /// </summary>
    private bool IsClusterLeader(DroneEnemy drone)
    {
        if (drone?.Cluster == null || drone.Cluster.drones == null || drone.Cluster.drones.Count == 0)
            return false;
        return ReferenceEquals(drone.Cluster.drones[0], drone);
    }

    /// <summary>
    /// Returns true if there are multiple zones available for relocation.
    /// If only one zone exists, drones should stay in Idle instead of relocating.
    /// </summary>
    private bool HasMultipleZones()
    {
        var zones = ZoneManager.Instance != null 
            ? ZoneManager.Instance.GetAllZones() 
            : null;
        return zones != null && zones.Length > 1;
    }

    public override void Tick(BaseEnemy<TState, TTrigger> enemy)
    {
        var drone = enemy as DroneEnemy;
        if (drone == null || drone.Cluster == null) return;

        // Only the cluster leader should run cluster-wide logic
        if (!IsClusterLeader(drone)) return;

        // PLAYER DETECTION - Always check for player, regardless of zone
        var player = drone.GetPlayerTransform();
        if (player != null && player.gameObject.activeInHierarchy)
        {
            float distToPlayer = Vector3.Distance(drone.transform.position, player.position);
            if (distToPlayer <= drone.DetectionRange)
            {
                drone.Cluster.AlertClusterSeePlayer();
                return; // Exit early - we're transitioning to chase
            }
        }
        
        // MOVEMENT - Only if we have a valid zone
        if (drone.currentZone == null)
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.LogWarning("DroneIdleBehavior", $"[DroneIdleBehavior] Drone {drone.name} has no currentZone assigned! Cannot move.");
#endif
            return;
        }

        // Move cluster to a new random point every interval (leader only)
        if (Time.time - drone.lastZoneMoveTime > drone.zoneMoveInterval)
        {
            Vector3 newTarget = drone.currentZone.GetRandomPointInZone();
            drone.Cluster.target.position = newTarget;
            drone.lastZoneMoveTime = Time.time;
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log("DroneIdleBehavior", $"[DroneIdleBehavior] Cluster moving to new target: {newTarget}");
#endif
        }
        drone.Cluster.UpdateClusterMovement();
    }

    public override void OnEnter(BaseEnemy<TState, TTrigger> enemy)
    {
        var drone = enemy as DroneEnemy;
        if (drone != null)
        {
            // Only start tick coroutine for the cluster leader
            // Non-leaders will just idle without running redundant logic
            if (IsClusterLeader(drone))
            {
                // Randomize formation to spread drones out
                drone.Cluster?.RandomizeFormationOffset();
                drone.StartTickCoroutine(() => Tick(enemy), TickInterval);
            }
            
            
            // Only start the idle timer (which triggers Relocate) if there are multiple zones
            // With only one zone, there's nowhere to relocate to - just stay in Idle
            if (HasMultipleZones())
            {
                drone.StartIdleTimer();
            }
        }
    }

    public override void OnExit(BaseEnemy<TState, TTrigger> enemy)
    {
        var drone = enemy as DroneEnemy;
        if (drone != null)
        {
            drone.StopIdleTimer();
            if (IsClusterLeader(drone))
            {
                drone.StopTickCoroutine();
            }
        }
    }
}