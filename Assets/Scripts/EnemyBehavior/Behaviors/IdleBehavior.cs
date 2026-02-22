// IdleBehavior.cs
// Purpose: Idle/Patrol behavior; supports wandering and idle timers that trigger relocations.
// Works with: BaseEnemy, Zone management.

using UnityEngine;
using System.Collections;
using UnityEngine.AI;

namespace Behaviors
{
    public class IdleBehavior<TState, TTrigger> : IEnemyStateBehavior<TState, TTrigger>
        where TState : struct, System.Enum
        where TTrigger : struct, System.Enum
    {
        private Coroutine idleTimerCoroutine;
        private Coroutine idleWanderCoroutine;
        private BaseEnemy<TState, TTrigger> enemy;

        // Commit-to-destination settings for Idle
        private Vector3 currentIdleTarget;
        private bool hasIdleTarget;
        private float targetStartTime;
        private const float arriveThreshold = 0.25f;      // meters in addition to agent.stoppingDistance
        private const float maxTravelSeconds = 8.0f;      // failsafe: choose a new target if not reached by then
        private const float monitorInterval = 0.15f;      // seconds between checks
        private const float fallbackWanderRadius = 6f;    // meters around current pos when no zone exists

        // Random idle pause between targets
        private float nextPickTime;
        private readonly Vector2 idlePauseRange = new Vector2(0.5f, 3.0f);

        public virtual void OnEnter(BaseEnemy<TState, TTrigger> enemy)
        {
            this.enemy = enemy;
            //EnemyBehaviorDebugLogBools.Log("IdleBehavior", "IdleBehavior.OnEnter called!");
            if (enemy.agent == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogError("NavMeshAgent not initialized!");
#endif
                return;
            }
            enemy.SetEnemyColor(enemy.patrolColor);
            //EnemyBehaviorDebugLogBools.Log("IdleBehavior", $"{enemy.gameObject.name} entered Idle state.");
            enemy.hasFiredLowHealth = false;
            enemy.CheckHealthThreshold();

            ResetIdleTimer();
            enemy.UpdateCurrentZone();

            hasIdleTarget = false; // force pick on first tick
            nextPickTime = Time.time; // allow immediate pick on start
            idleWanderCoroutine = enemy.StartCoroutine(IdleWanderLoop());
        }

        public virtual void OnExit(BaseEnemy<TState, TTrigger> enemy)
        {
            if (idleTimerCoroutine != null)
            {
                enemy.StopCoroutine(idleTimerCoroutine);
                idleTimerCoroutine = null;
            }
            if (idleWanderCoroutine != null)
            {
                enemy.StopCoroutine(idleWanderCoroutine);
                idleWanderCoroutine = null;
            }
            hasIdleTarget = false;
        }

        private void ResetIdleTimer()
        {
            if (idleTimerCoroutine != null)
            {
                enemy.StopCoroutine(idleTimerCoroutine);
            }
            idleTimerCoroutine = enemy.StartCoroutine(IdleTimerCoroutine());
        }

        private IEnumerator IdleTimerCoroutine()
        {
            yield return WaitForSecondsCache.Get(enemy.idleTimerDuration);
            
            // Use generic comparison - check if current state name contains "Idle"
            string currentStateName = enemy.enemyAI.State.ToString();
            if (currentStateName.Contains("Idle"))
            {
                // Use ZoneManager if available, otherwise fallback
                Zone[] zones = ZoneManager.Instance != null 
                    ? ZoneManager.Instance.GetAllZones() 
                    : Object.FindObjectsByType<Zone>(FindObjectsSortMode.None);
                if (zones == null || zones.Length <= 1)
                {
                    // Continue idling: restart timer and keep wandering
                    ResetIdleTimer();
                }
                else
                {
                    enemy.TryFireTriggerByName("IdleTimerElapsed");
                }
            }
            idleTimerCoroutine = null;
        }

        private IEnumerator IdleWanderLoop()
        {
            var wait = WaitForSecondsCache.Get(monitorInterval);
            while (true)
            {
                if (!hasIdleTarget)
                {
                    // Wait for a random pause before picking the next target
                    if (Time.time >= nextPickTime)
                    {
                        PickNewIdleTarget();
                    }
                }
                else
                {
                    // Monitor progress to the current target; commit until reached or timeout
                    bool arrived = false;
                    if (enemy.agent != null && enemy.agent.enabled && enemy.agent.isOnNavMesh)
                    {
                        if (enemy.agent.hasPath)
                        {
                            arrived = enemy.agent.remainingDistance <= (enemy.agent.stoppingDistance + arriveThreshold);
                        }
                        else
                        {
                            // fallback distance check
                            float d = Vector3.Distance(enemy.transform.position, currentIdleTarget);
                            arrived = d <= (enemy.agent.stoppingDistance + arriveThreshold);
                        }
                    }

                    if (arrived)
                    {
                        hasIdleTarget = false;
                        // schedule next pick after a random pause
                        nextPickTime = Time.time + Random.Range(idlePauseRange.x, idlePauseRange.y);
                    }
                    else if (Time.time - targetStartTime > maxTravelSeconds)
                    {
                        // Failsafe: pick a new one after a small pause
                        hasIdleTarget = false;
                        nextPickTime = Time.time + Random.Range(idlePauseRange.x, idlePauseRange.y);
                    }
                }
                yield return wait;
            }
        }

        private void PickNewIdleTarget()
        {
            Vector3 target;
            if (enemy.currentZone == null)
            {
                Vector2 circle = Random.insideUnitCircle * fallbackWanderRadius;
                target = enemy.transform.position + new Vector3(circle.x, 0f, circle.y);
            }
            else
            {
                target = enemy.currentZone.GetRandomPointInZone();
            }

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                currentIdleTarget = hit.position;
                targetStartTime = Time.time;
                hasIdleTarget = true;
                enemy.agent.isStopped = false;
                enemy.agent.SetDestination(currentIdleTarget);
            }
            else
            {
                // if sample fails, try again later
                nextPickTime = Time.time + Random.Range(idlePauseRange.x, idlePauseRange.y);
            }
        }

        public virtual void Tick(BaseEnemy<TState, TTrigger> enemy) { }
    }
}