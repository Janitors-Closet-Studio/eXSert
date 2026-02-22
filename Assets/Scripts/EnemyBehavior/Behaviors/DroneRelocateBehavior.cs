// DroneRelocateBehavior.cs
// Purpose: Handles drone relocation logic within a swarm, integrates with pathing.
// Works with: PathRequestManager, DroneSwarmManager.

using Behaviors;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DroneRelocateBehavior<TState, TTrigger> : RelocateBehavior<TState, TTrigger>
    where TState : struct, System.Enum
    where TTrigger : struct, System.Enum
{
    private Coroutine relocateCoroutine;

    /// <summary>
    /// Returns true if this drone is the leader of its cluster.
    /// Only the leader should run cluster-wide relocation logic.
    /// </summary>
    private bool IsClusterLeader(DroneEnemy drone)
    {
        if (drone?.Cluster == null || drone.Cluster.drones == null || drone.Cluster.drones.Count == 0)
            return false;
        return ReferenceEquals(drone.Cluster.drones[0], drone);
    }

    public override void OnEnter(BaseEnemy<TState, TTrigger> enemy)
    {
        var drone = enemy as DroneEnemy;
        if (drone == null || drone.Cluster == null)
            return;

        // Only the cluster leader should run relocation logic
        if (!IsClusterLeader(drone))
            return;

        // Pick a new zone different from currentZone
        Zone newZone = null;
        // Use ZoneManager if available
        var zones = ZoneManager.Instance != null 
            ? ZoneManager.Instance.GetAllZones() 
            : UnityEngine.Object.FindObjectsByType<Zone>(FindObjectsSortMode.None);
        
        // If no zones exist, wait briefly then return to Idle to prevent rapid state cycling
        if (zones == null || zones.Length == 0)
        {
            relocateCoroutine = drone.StartCoroutine(DelayedReturnToIdle(drone));
            return;
        }

        if (zones.Length > 1)
        {
            var candidates = new List<Zone>(zones);
            candidates.Remove(drone.currentZone);
            if (candidates.Count > 0)
                newZone = candidates[Random.Range(0, candidates.Count)];
            else
                newZone = zones[0];
        }
        else if (zones.Length == 1)
        {
            newZone = zones[0];
        }

        if (newZone != null)
        {
            // Set zone for all drones in cluster
            foreach (var member in drone.Cluster.drones)
            {
                member.currentZone = newZone;
            }
        }

        // Set cluster target to a random point in the new zone
        Vector3 relocateTarget = newZone != null 
            ? newZone.GetRandomPointInZone() 
            : drone.transform.position;
        drone.Cluster.target.position = relocateTarget;

        // Start coroutine to monitor arrival (leader only)
        relocateCoroutine = drone.StartCoroutine(RelocateAndReturnToIdle(drone, relocateTarget));
        drone.Cluster.BeginRelocate(relocateTarget);
    }

    public override void OnExit(BaseEnemy<TState, TTrigger> enemy)
    {
        var drone = enemy as DroneEnemy;
        if (drone == null) return;

        // Only the leader has a relocate coroutine to stop
        if (IsClusterLeader(drone) && relocateCoroutine != null)
        {
            drone.StopCoroutine(relocateCoroutine);
            relocateCoroutine = null;
            drone.Cluster?.EndRelocate();
        }
    }

    /// <summary>
    /// When no zones exist, wait briefly before returning to Idle to prevent rapid state cycling.
    /// </summary>
    private IEnumerator DelayedReturnToIdle(DroneEnemy drone)
    {
        // Wait a reasonable time before transitioning back to prevent rapid cycling
        yield return WaitForSecondsCache.Get(5f);
        
        if (drone != null && drone.Cluster != null)
        {
            foreach (var member in drone.Cluster.drones)
            {
                member.TryFireTriggerByName("RelocateComplete");
            }
        }
    }

    // Coroutine: Wait until all cluster members are inside the target zone, then fire trigger to return to Idle
    private IEnumerator RelocateAndReturnToIdle(DroneEnemy drone, Vector3 target)
    {
        Zone targetZone = drone.currentZone;
        // Increased interval to reduce SetDestination calls which cause memory leaks
        const float checkInterval = 0.5f;
        float stuckCheckInterval = 6f;
        float stuckThreshold = 0.5f;
        float maxRelocateTime = 15f; // Maximum time before forcing completion
        float startTime = Time.time;
        Dictionary<DroneEnemy, Vector3> stuckCheckStartPositions = new Dictionary<DroneEnemy, Vector3>();
        float stuckCheckStartTime = Time.time;

        foreach (var member in drone.Cluster.drones)
            stuckCheckStartPositions[member] = member.transform.position;

        drone.Cluster.BeginRelocate(target);

        while (true)
        {
            int inZoneCount = 0;
            int total = drone.Cluster.drones.Count;

            // If no target zone, count based on distance to target instead
            for (int i = 0; i < drone.Cluster.drones.Count; i++)
            {
                var member = drone.Cluster.drones[i];
                if (targetZone != null && targetZone.Contains(member.transform.position))
                {
                    inZoneCount++;
                }
                else if (targetZone == null)
                {
                    // Fallback: if no zone, check if close to target
                    if (Vector3.Distance(member.transform.position, target) < 5f)
                        inZoneCount++;
                }
            }

            // Normal transition if enough are in the zone (or near target)
            if (inZoneCount >= Mathf.CeilToInt(total / 2f))
            {
                for (int i = 0; i < drone.Cluster.drones.Count; i++)
                {
                    drone.Cluster.drones[i].TryFireTriggerByName("RelocateComplete");
                }
                yield break;
            }

            // Timeout failsafe - prevent infinite loop
            if (Time.time - startTime > maxRelocateTime)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning("DroneRelocateBehavior", $"[Failsafe] Relocate timed out after {maxRelocateTime}s. Forcing Idle.");
#endif
                for (int i = 0; i < drone.Cluster.drones.Count; i++)
                {
                    drone.Cluster.drones[i].TryFireTriggerByName("RelocateComplete");
                }
                yield break;
            }

            // Failsafe: check movement over the entire interval
            if (Time.time - stuckCheckStartTime > stuckCheckInterval)
            {
                int stuckCount = 0;
                for (int i = 0; i < drone.Cluster.drones.Count; i++)
                {
                    var member = drone.Cluster.drones[i];
                    if (stuckCheckStartPositions.TryGetValue(member, out var startPos))
                    {
                        float moved = Vector3.Distance(member.transform.position, startPos);
                        if (moved < stuckThreshold)
                            stuckCount++;
                    }
                }

                // If the majority of drones are stuck, trigger failsafe
                if (stuckCount >= Mathf.CeilToInt(total / 2f))
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogWarning("DroneRelocateBehavior", $"[Failsafe] Cluster appears stuck in Relocate. Forcing Idle.");
#endif
                    for (int i = 0; i < drone.Cluster.drones.Count; i++)
                    {
                        drone.Cluster.drones[i].TryFireTriggerByName("RelocateComplete");
                    }
                    yield break;
                }
                // Reset for next interval
                stuckCheckStartTime = Time.time;
                for (int i = 0; i < drone.Cluster.drones.Count; i++)
                {
                    stuckCheckStartPositions[drone.Cluster.drones[i]] = drone.Cluster.drones[i].transform.position;
                }
            }

            drone.Cluster.UpdateClusterMovement(drone.Cluster.target.position);
            
            // CRITICAL FIX: Use interval instead of every frame to prevent memory leak
            yield return WaitForSecondsCache.Get(checkInterval);
        }
    }

    public override void Tick(BaseEnemy<TState, TTrigger> enemy)
    {
        // Relocate doesn't need a tick - the coroutine handles everything
    }
}