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
        private float savedStoppingDistance;

        // Cache the state value once (add at class level)
        private TState chaseStateValue;

        public virtual void OnEnter(BaseEnemy<TState, TTrigger> enemy)
        {
            this.enemy = enemy;
            playerTarget = enemy.PlayerTarget;

            // Cache the Chase state value for this enum type
            chaseStateValue = (TState)System.Enum.Parse(typeof(TState), "Chase");

            // Save current stoppingDistance so we can restore it on exit.
            // MoveToAttackRange will overwrite it during the chase.
            if (enemy.agent != null)
                savedStoppingDistance = enemy.agent.stoppingDistance;

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
            {
                enemy.agent.ResetPath();
                // Restore the stoppingDistance that was overwritten by MoveToAttackRange.
                enemy.agent.stoppingDistance = savedStoppingDistance;
            }
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
            const float destinationUpdateThreshold = 0.15f; // Recalculate when player moves this far
            const float attackRangeTolerance = 0.35f; // Prevent guard-state stalls when the agent stops just outside raw attack range.
            var wait = WaitForSecondsCache.Get(updateInterval);

            // Wait one frame before the first range check so the agent has a chance to
            // start moving. Without this, re-entering Chase from Attack (e.g. after the
            // enemy backed off while the player was guarding) can instantly re-fire
            // InAttackRange before any movement occurs, causing an Attack<->Chase loop.
            yield return null;

            Vector3 lastDestination = playerTarget != null ? playerTarget.position : Vector3.zero;

            while (enemy.enemyAI.State.Equals(chaseStateValue) && playerTarget != null)
            {
                if (enemy.agent != null && enemy.agent.enabled)
                {
                    float playerMovement = Vector3.Distance(playerTarget.position, lastDestination);
                    // Recalculate path if: player moved meaningfully, no path yet, or the
                    // agent has reached its destination but is still out of attack range
                    // (catches the slow-walk-away case where incremental moves < threshold).
                    bool agentIdleShortOfTarget = enemy.agent.hasPath
                        && !enemy.agent.pathPending
                        && enemy.agent.remainingDistance <= enemy.agent.stoppingDistance + 0.05f;

                    if (playerMovement > destinationUpdateThreshold || !enemy.agent.hasPath || agentIdleShortOfTarget)
                    {
                        MoveToAttackRange(playerTarget);
                        lastDestination = playerTarget.position;
                    }
                }

                float attackRange = (Mathf.Max(enemy.attackBoxSize.x, enemy.attackBoxSize.z) * 0.5f) + enemy.attackBoxDistance;
                float distance = Vector3.Distance(enemy.transform.position, playerTarget.position);

                if (distance <= attackRange + attackRangeTolerance)
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

        private void MoveToAttackRange(Transform player)
        {
            if (enemy.agent == null) return;

            // Compute the radius at which the enemy is considered in attack range.
            float attackRange = (Mathf.Max(enemy.attackBoxSize.x, enemy.attackBoxSize.z) * 0.5f)
                                + enemy.attackBoxDistance;

            // Set stoppingDistance so the agent naturally halts just inside attack range.
            // A small inward margin ensures the enemy lands firmly inside the attack box
            // rather than right on its edge, without requiring any offset point calculation.
            // Previous approach computed an explicit offset candidate and called
            // NavMesh.SamplePosition on it, which could snap the destination up to 1 m
            // away from the intended point — causing inconsistent stopping distances that
            // no inspector value could reliably compensate for.
            const float arrivalMargin = 0.15f;
            enemy.agent.stoppingDistance = Mathf.Max(0f, attackRange - arrivalMargin);

            enemy.agent.isStopped = false;
            enemy.agent.SetDestination(player.position);
        }
        public void Tick(BaseEnemy<TState, TTrigger> enemy)
        {
            // No per-frame logic needed for death
        }
    }
}