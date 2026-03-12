// ChaseBehavior.cs
// Purpose: Behavior module implementing Chase logic: pursuit and attack-range checks.
// Works with: BaseEnemy state machine, PathRequestManager for pathing, NavMeshAgent movement.

using UnityEngine;
using System.Collections;
using UnityEngine.AI;

namespace Behaviors
{
    public class ChaseBehavior<TState, TTrigger> : IEnemyStateBehavior<TState, TTrigger>
        where TState : struct, System.Enum
        where TTrigger : struct, System.Enum
    {
        private Coroutine chaseCoroutine;
        private BaseEnemy<TState, TTrigger> enemy;
        private Transform playerTarget;

        // Cache the state value once (add at class level)
        private TState chaseStateValue;

        public virtual void OnEnter(BaseEnemy<TState, TTrigger> enemy)
        {
            this.enemy = enemy;
            playerTarget = enemy.PlayerTarget;

            // Cache the Chase state value for this enum type
            chaseStateValue = (TState)System.Enum.Parse(typeof(TState), "Chase");

            // Special handling for BaseCrawlerEnemy with ForceChasePlayer
            if (enemy is BaseCrawlerEnemy crawler && crawler.ForceChasePlayer)
            {
                if (crawler.PlayerTarget != null && crawler.agent != null && crawler.agent.enabled)
                {
                    crawler.agent.isStopped = false;
                    crawler.agent.SetDestination(crawler.PlayerTarget.position);
                }
                crawler.SetEnemyColor(crawler.chaseColor);

                if (chaseCoroutine != null)
                    crawler.StopCoroutine(chaseCoroutine);

                // Still run the blob chase coroutine to allow transitions (attack, flee, etc.)
                chaseCoroutine = crawler.StartCoroutine(CrawlerChaseBlob(crawler));
                return;
            }

            if (playerTarget != null && enemy.agent != null && enemy.agent.enabled)
            {
                enemy.agent.isStopped = false;
                // First tick toward player; loop will maintain pursuit
                enemy.agent.SetDestination(playerTarget.position);
            }

            enemy.SetEnemyColor(enemy.chaseColor);

            if (chaseCoroutine != null)
                enemy.StopCoroutine(chaseCoroutine);

            // Use blob chase for crawlers, default for others
            if (enemy is BaseCrawlerEnemy baseCrawler)
                chaseCoroutine = enemy.StartCoroutine(CrawlerChaseBlob(baseCrawler));
            else
                chaseCoroutine = enemy.StartCoroutine(DefaultChasePlayerLoop());
        }

        public virtual void OnExit(BaseEnemy<TState, TTrigger> enemy)
        {
            if (chaseCoroutine != null)
            {
                enemy.StopCoroutine(chaseCoroutine);
                chaseCoroutine = null;
            }
            if (enemy.agent != null)
                enemy.agent.ResetPath();
        }

        // Blob chase for crawlers
        private IEnumerator CrawlerChaseBlob(BaseCrawlerEnemy crawler)
        {
            // Wait one frame to ensure state transition is complete
            yield return null;
            
            const float updateInterval = 0.05f; // More frequent updates for smoother motion
            const float destinationUpdateThreshold = 0.4f; // Only update destination if player moved significantly
            Vector3 lastDestination = Vector3.zero;
            
            // For boss fight crawlers with ForceChasePlayer, run indefinitely until attack/death
            // For normal crawlers, check state normally
            bool shouldContinue = crawler.ForceChasePlayer 
                ? (crawler.enemyAI.State == CrawlerEnemyState.Chase || 
                   crawler.enemyAI.State == CrawlerEnemyState.Attack ||
                   crawler.enemyAI.State == CrawlerEnemyState.Swarm) // Any active state
                : crawler.enemyAI.State.Equals(CrawlerEnemyState.Chase);
                
            while (shouldContinue && crawler != null && crawler.gameObject != null)
            {
                // Re-read PlayerTarget each frame to support late assignment from boss controller
                Transform player = crawler.PlayerTarget;
                
                // If no player target, wait and retry (boss controller may assign it shortly)
                if (player == null)
                {
                    yield return WaitForSecondsCache.Get(0.1f);
                    shouldContinue = crawler.ForceChasePlayer 
                        ? (crawler.enemyAI.State != CrawlerEnemyState.Death)
                        : crawler.enemyAI.State.Equals(CrawlerEnemyState.Chase);
                    continue;
                }
                
                // Move as a blob toward the player, apply separation
                if (crawler.agent != null && crawler.agent.enabled)
                {
                    // Only recalculate path if player has moved significantly
                    float playerMovement = Vector3.Distance(player.position, lastDestination);
                    if (playerMovement > destinationUpdateThreshold || !crawler.agent.hasPath)
                    {
                        crawler.agent.isStopped = false;
                        crawler.agent.SetDestination(player.position);
                        lastDestination = player.position;
                    }
                }

                crawler.ApplySeparation();

                // If close enough to attack, fire the correct trigger
                float minRadius = crawler.attackBoxDistance + (crawler.attackBoxSize.x * 0.5f);
                if (Vector3.Distance(crawler.transform.position, player.position) <= minRadius + 0.5f)
                {
                    if (!crawler.enableSwarmBehavior)
                        crawler.TryFireTriggerByName("InAttackRange");
                    else
                        crawler.TryFireTriggerByName("ReachSwarm");
                    yield break;
                }

                // --- FIX: Only allow Flee if not forced to chase by alarm ---
                // Only allow flee if not alarm-spawned or alarm is dead
                // Also skip flee check entirely if ForceChasePlayer is true (boss fight spawned enemies)
                // Also skip flee check if there's no pocket assigned (crawler spawned without a pocket)
                bool ignoreFlee = crawler.ForceChasePlayer; // Boss-spawned crawlers should NEVER flee
                
                // No pocket = no flee destination, so skip flee logic entirely
                if (!ignoreFlee && crawler.Pocket == null)
                {
                    ignoreFlee = true;
                }
                
                if (!ignoreFlee && crawler.AlarmSource != null && crawler.AlarmSource.enemyAI != null)
                {
                    ignoreFlee = crawler.AlarmSource.enemyAI.State == AlarmCarrierState.Summoning;
                }

                if (!ignoreFlee)
                {
                    float playerToPocket = Vector3.Distance(player.position, crawler.PocketPosition);
                    if (playerToPocket > crawler.fleeDistanceFromPocket)
                    {
                        crawler.TryFireTriggerByName("Flee");
                        yield break;
                    }
                }
                // else: do NOT fire Flee, keep swarming/chasing

                yield return WaitForSecondsCache.Get(updateInterval);
                
                // Update continue condition at end of loop
                shouldContinue = crawler.ForceChasePlayer 
                    ? (crawler.enemyAI.State != CrawlerEnemyState.Death)
                    : crawler.enemyAI.State.Equals(CrawlerEnemyState.Chase);
            }
        }

        // Chase logic for non-crawlers
        private IEnumerator DefaultChasePlayerLoop()
        {
            const float losePlayerDistance = 25f;
            const float updateInterval = 0.05f; // More frequent updates for smoother motion
            const float destinationUpdateThreshold = 0.5f; // Only update destination if player moved significantly
            var wait = WaitForSecondsCache.Get(updateInterval);

            Vector3 lastDestination = playerTarget != null ? playerTarget.position : Vector3.zero;
            
            while (enemy.enemyAI.State.Equals(chaseStateValue) && playerTarget != null)
            {
                if (enemy.agent != null && enemy.agent.enabled)
                {
                    // Only recalculate path if player has moved significantly
                    float playerMovement = Vector3.Distance(playerTarget.position, lastDestination);
                    if (playerMovement > destinationUpdateThreshold || !enemy.agent.hasPath)
                    {
                        MoveToAttackRange(playerTarget);
                        lastDestination = playerTarget.position;
                    }
                }

                float attackRange = (Mathf.Max(enemy.attackBoxSize.x, enemy.attackBoxSize.z) * 0.5f) + enemy.attackBoxDistance;
                float distance = Vector3.Distance(enemy.transform.position, playerTarget.position);

                if (distance <= attackRange)
                {
                    enemy.TryFireTriggerByName("InAttackRange");
                    yield break;
                }

                if (distance >= losePlayerDistance)
                {
                    enemy.TryFireTriggerByName("LosePlayer");
                    yield break;
                }

                yield return wait;
            }
        }

        // Picks an approach point around the player at the desired reach and avoids obstacle corners
        private void MoveToAttackRange(Transform player)
        {
            if (enemy.agent == null) return;

            // Desired radial distance from player to stand at before attacking
            float chaseBuffer = 0.2f;
            float reach = (Mathf.Max(enemy.attackBoxSize.x, enemy.attackBoxSize.z) * 0.5f) + enemy.attackBoxDistance - chaseBuffer;
            reach = Mathf.Max(0.1f, reach);

            Vector3 toPlayer = player.position - enemy.transform.position; toPlayer.y = 0f;
            Vector3 baseDir = toPlayer.sqrMagnitude < 0.001f ? enemy.transform.forward : toPlayer.normalized;

            // Try candidates around an arc near the facing direction: 0, ±20, ±40, ±60 degrees
            float[] angles = new float[] { 0f, 20f, -20f, 40f, -40f, 60f, -60f };
            Vector3 best = Vector3.zero;
            bool found = false;
            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(angles[i], Vector3.up) * baseDir;
                Vector3 candidate = player.position - dir * reach;
                candidate.y = enemy.transform.position.y;

                // Snap to closest navmesh point near candidate
                if (!NavMesh.SamplePosition(candidate, out var hit, 1.0f, NavMesh.AllAreas))
                    continue;

                // Prefer straight clear ray on navmesh between current and candidate
                if (!NavMesh.Raycast(enemy.transform.position, hit.position, out var navHit, NavMesh.AllAreas))
                {
                    best = hit.position;
                    found = true;
                    break;
                }

                // Keep first valid sample as fallback
                if (!found)
                {
                    best = hit.position;
                    found = true;
                }
            }

            if (!found)
            {
                // Final fallback: head directly to player's sampled position
                if (NavMesh.SamplePosition(player.position, out var phit, 1.5f, NavMesh.AllAreas))
                {
                    best = phit.position;
                    found = true;
                }
            }

            if (found)
            {
                enemy.agent.isStopped = false;
                enemy.agent.SetDestination(best);
            }
        }
        public void Tick(BaseEnemy<TState, TTrigger> enemy)
        {
            // No per-frame logic needed for death
        }
    }
}