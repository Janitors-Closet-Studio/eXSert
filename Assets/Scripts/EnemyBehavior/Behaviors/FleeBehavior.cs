// FleeBehavior.cs
// Purpose: Behavior for fleeing when low health or overwhelmed. Handles movement away from threat.
// Works with: BaseEnemy state machine, NavMeshAgent, CrowdController.

using UnityEngine;
using System.Collections;
using UnityEngine.AI;

namespace Behaviors
{
    public class FleeBehavior<TState, TTrigger> : IEnemyStateBehavior<TState, TTrigger>
        where TState : struct, System.Enum
        where TTrigger : struct, System.Enum
    {
        private Coroutine fleeCoroutine;

        public virtual void OnEnter(BaseEnemy<TState, TTrigger> enemy)
        {
            if (fleeCoroutine != null)
                enemy.StopCoroutine(fleeCoroutine);

            if (enemy is BaseCrawlerEnemy crawler)
                fleeCoroutine = enemy.StartCoroutine(FleeToPocketCoroutine(crawler));
            else
                FleeToZone(enemy);
        }

        public virtual void OnExit(BaseEnemy<TState, TTrigger> enemy)
        {
            if (fleeCoroutine != null)
                enemy.StopCoroutine(fleeCoroutine);
        }

        private IEnumerator FleeToPocketCoroutine(BaseCrawlerEnemy crawler)
        {
            // Prevent alarm-spawned crawlers from fleeing/returning to pocket while alarm bot exists
            if (crawler.AlarmSource != null && crawler.AlarmSource.gameObject != null)
                yield break;

            // If no pocket is assigned, transition back to Chase instead of getting stuck
            if (crawler.Pocket == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning("FleeBehavior", $"{crawler.name}: No pocket assigned, transitioning to Chase instead of Flee.");
#endif
                // Wait a frame to avoid re-entrancy issues
                yield return null;
                crawler.TryFireTriggerByName("SeePlayer");
                yield break;
            }

            while (true)
            {
                if (crawler.Pocket == null)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogWarning("FleeBehavior", $"{crawler.name}: Pocket became null, transitioning to Chase.");
#endif
                    yield return null;
                    crawler.TryFireTriggerByName("SeePlayer");
                    yield break;
                }

                Vector3 pocketPos = crawler.PocketPosition;

                // If player comes close, break out
                if (crawler.PlayerTarget != null)
                {
                    float playerToPocket = Vector3.Distance(crawler.PlayerTarget.position, pocketPos);
                    if (playerToPocket <= crawler.fleeDistanceFromPocket)
                    {
                        crawler.enemyAI.Fire(CrawlerEnemyTrigger.SeePlayer);
                        yield break;
                    }
                }

                if (crawler.agent != null && crawler.agent.enabled)
                {
                    crawler.agent.isStopped = false;
                    crawler.agent.speed = Mathf.Max(0.1f, crawler.agent.speed);
                    crawler.agent.SetDestination(pocketPos);
                }
                else
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogWarning("FleeBehavior", $"{crawler.name}: Agent is null or disabled in Flee state!");
#endif
                }

                crawler.ApplySeparation();

                // Arrival: close to pocket center
                if (Vector3.Distance(crawler.transform.position, pocketPos) < 1.0f)
                {
                    if (crawler.Pocket != null)
                    {
                        crawler.Pocket.activeEnemies.Remove(crawler);
                        if (crawler.currentHealth > 0)
                            crawler.Pocket.ReturnEnemyToInactive(crawler);
                        GameObject.Destroy(crawler.gameObject);
                    }
                    yield break;
                }

                yield return WaitForSecondsCache.Get(0.1f);
            }
        }

        private void FleeToZone(BaseEnemy<TState, TTrigger> enemy)
        {
            if (enemy.agent != null && enemy.agent.enabled && enemy.currentZone != null)
            {
                Vector3 zonePos = enemy.currentZone.GetRandomPointInZone();
                enemy.agent.SetDestination(zonePos);
            }
        }
        public void Tick(BaseEnemy<TState, TTrigger> enemy)
        {
            // No per-frame logic needed for death
        }
    }
}