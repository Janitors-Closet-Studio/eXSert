// AttackBehavior.cs
// Purpose: Handles attack state logic for enemies (melee/ranged), damage application, and hit detection.
// Works with: BaseEnemy attack triggers, EnemyProjectile, Player health systems, EnemyAttackQueueManager.
// Notes: Does not manage movement; only attack timing and hit application.

using UnityEngine;
using System.Collections;
using Utilities.Combat;
using EnemyBehavior;

namespace Behaviors
{
    public class AttackBehavior<TState, TTrigger> : IEnemyStateBehavior<TState, TTrigger>
        where TState : struct, System.Enum
        where TTrigger : struct, System.Enum
    {
        private Coroutine lookAtPlayerCoroutine;
        private Coroutine attackRangeMonitorCoroutine;
        private Coroutine attackLoopCoroutine;
        private BaseEnemy<TState, TTrigger> enemy;
        private Transform playerTarget;

        // Cache the Attack state value for this enum type
        private TState attackStateValue;

        private static readonly Collider[] hitBuffer = new Collider[16];

        public virtual void OnEnter(BaseEnemy<TState, TTrigger> enemy)
        {
            this.enemy = enemy;
            playerTarget = enemy.PlayerTarget;

            // Cache the Attack state value once
            attackStateValue = (TState)System.Enum.Parse(typeof(TState), "Attack");

            enemy.SetEnemyColor(enemy.attackColor);

            if (lookAtPlayerCoroutine != null)
                enemy.StopCoroutine(lookAtPlayerCoroutine);
            lookAtPlayerCoroutine = enemy.StartCoroutine(LookAtPlayerLoop());

            if (attackRangeMonitorCoroutine != null)
                enemy.StopCoroutine(attackRangeMonitorCoroutine);
            attackRangeMonitorCoroutine = enemy.StartCoroutine(MonitorAttackRangeLoop());

            if (attackLoopCoroutine != null)
                enemy.StopCoroutine(attackLoopCoroutine);
            attackLoopCoroutine = enemy.StartCoroutine(AttackLoop());
        }

        public virtual void OnExit(BaseEnemy<TState, TTrigger> enemy)
        {
            if (lookAtPlayerCoroutine != null)
            {
                enemy.StopCoroutine(lookAtPlayerCoroutine);
                lookAtPlayerCoroutine = null;
            }
            if (attackRangeMonitorCoroutine != null)
            {
                enemy.StopCoroutine(attackRangeMonitorCoroutine);
                attackRangeMonitorCoroutine = null;
            }
            if (attackLoopCoroutine != null)
            {
                enemy.StopCoroutine(attackLoopCoroutine);
                attackLoopCoroutine = null;
            }
            enemy.attackCollider.enabled = false;
        }

        private IEnumerator LookAtPlayerLoop()
        {
            while (enemy.enemyAI.State.Equals(attackStateValue) && playerTarget != null)
            {
                if (!enemy.isAttackBoxActive)
                {
                    Vector3 direction = (playerTarget.position - enemy.transform.position).normalized;
                    direction.y = 0;
                    if (direction != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(direction);
                        float t = 0f;
                        Quaternion startRotation = enemy.transform.rotation;
                        while (t < 1f && !enemy.isAttackBoxActive)
                        {
                            t += Time.deltaTime;
                            enemy.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                            yield return null;
                        }
                    }
                }
                yield return WaitForSecondsCache.Get(1f);
            }
        }

        private IEnumerator MonitorAttackRangeLoop()
        {
            while (enemy.enemyAI.State.Equals(attackStateValue) && playerTarget != null)
            {
                float attackRange = (Mathf.Max(enemy.attackBoxSize.x, enemy.attackBoxSize.z) * 0.5f) + enemy.attackBoxDistance;
                float distance = Vector3.Distance(enemy.transform.position, playerTarget.position);

                if (distance > attackRange)
                {
                    enemy.TryFireTriggerByName("OutOfAttackRange");
                    yield break;
                }

                yield return WaitForSecondsCache.Get(0.1f);
            }
        }

        private IEnumerator AttackLoop()
        {
            int safetyCounter = 0;
            const int maxIterations = 10000; // Prevent infinite loop in editor

            while (enemy.enemyAI.State.Equals(attackStateValue))
            {
                safetyCounter++;
                if (safetyCounter > maxIterations)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogError("AttackLoop exceeded max iterations! Breaking to prevent freeze.");
#endif
                    yield break;
                }

                // Check if this enemy can attack (is at front of queue)
                if (!enemy.CanAttackFromQueue())
                {
                    // Not our turn - wait and check again
                    yield return WaitForSecondsCache.Get(0.15f);
                    continue;
                }

                bool playerInAttackBox = false;
                Collider playerCollider = null;
                bool didAttack = false;

                try
                {
                    Vector3 boxCenter = enemy.transform.position + enemy.transform.forward * enemy.attackBoxDistance;
                    boxCenter += Vector3.up * enemy.attackBoxHeightOffset;
                    Vector3 boxHalfExtents = enemy.attackBoxSize * 0.5f;
                    Quaternion boxRotation = enemy.transform.rotation;

                    if (boxHalfExtents == Vector3.zero)
                    {
#if UNITY_EDITOR
                        EnemyBehaviorDebugLogBools.LogWarning("AttackBehavior", "Attack box size is zero!");
#endif
                        yield break;
                    }

                    int hitCount = Physics.OverlapBoxNonAlloc(boxCenter, boxHalfExtents, hitBuffer, boxRotation);

                    for (int i = 0; i < hitCount; i++)
                    {
                        var hit = hitBuffer[i];
                        if (hit.CompareTag("Player"))
                        {
                            playerInAttackBox = true;
                            playerCollider = hit;
                            break;
                        }
                    }

                if (playerInAttackBox)
                    {
                        // Notify queue that we're attacking
                        enemy.NotifyAttackBegin();
                        
                        // For animation-event-based attacks, don't enable hitbox here - let animation events handle it
                        if (!enemy.useAnimationEventAttacks)
                        {
                            enemy.isAttackBoxActive = true;
                            enemy.attackCollider.enabled = true;
                            enemy.SetEnemyColor(enemy.hitboxActiveColor);
                            DealDamageToPlayerOnce(playerCollider);
                        }
                        // else: Animation events (Attack/AttackEnd) will control hitbox timing

                        didAttack = true;
                        enemy.TriggerAttackAnimation();
                    }
                    else
                    {
                        // Only disable hitbox here if NOT using animation events
                        // (animation events handle their own disable via AttackEnd)
                        if (!enemy.useAnimationEventAttacks)
                        {
                            enemy.isAttackBoxActive = false;
                            enemy.attackCollider.enabled = false;
                            enemy.SetEnemyColor(enemy.attackColor);
                        }
                    }
                }
                catch (System.Exception ex)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogError("Exception in AttackLoop: " + ex);
#endif
                    yield break;
                }

                if (didAttack)
                {
                    // For animation-event-based attacks, wait for animation to complete
                    // The attackActiveDuration is used as a minimum wait time for the full attack cycle
                    yield return WaitForSecondsCache.Get(enemy.attackActiveDuration);
                    
                    // Only disable hitbox via timer if NOT using animation events
                    // Animation events will call AttackEnd() to disable the hitbox at the right time
                    if (!enemy.useAnimationEventAttacks)
                    {
                        enemy.isAttackBoxActive = false;
                        enemy.attackCollider.enabled = false;
                        enemy.SetEnemyColor(enemy.attackColor);
                    }
                    
                    ResetDamageFlag();
                    yield return WaitForSecondsCache.Get(enemy.attackInterval);

                    // Notify queue that attack is finished - move to back of queue
                    enemy.NotifyAttackEnd();

                    // Only do backup and rotate for crawlers
                    if (enemy is BaseCrawlerEnemy crawler)
                    {
                        yield return HandleAfterAttack();
                        if (SwarmManager.Instance != null)
                            SwarmManager.Instance.RotateAttackers();
                    }
                    // For other enemy types, you could add custom post-attack logic here if needed
                }
                else
                {
                    yield return WaitForSecondsCache.Get(0.1f);
                }
            }
            // Ensure hitbox is disabled when exiting attack state
            enemy.isAttackBoxActive = false;
            enemy.attackCollider.enabled = false;
            enemy.SetEnemyColor(enemy.attackColor);
        }

        private bool damageSentThisEnable = false;

        private void DealDamageToPlayerOnce(Collider playerCollider)
        {
            if (damageSentThisEnable) return;
            damageSentThisEnable = true;

            if (!playerCollider.CompareTag("Player")) return;

            float dmg = enemy.damage;

            // Parry: no damage
            if (CombatManager.isParrying && enemy.canBeParried)
            {
                CombatManager.ParrySuccessful();
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log("AttackBehavior", $"{enemy.gameObject.name} attack parried by player.");
#endif
                dmg = 0;
                return;
            }

            playerCollider.TryGetComponent<IHealthSystem>(out var healthSystem);
            if (healthSystem == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning("AttackBehavior", $"{playerCollider.gameObject.name} has Player tag but no IHealthSystem component.");
#endif
                return;
            }

            
            // Guard: half damage
            if (CombatManager.isGuarding)
            {
                dmg *= 0.25f; // temporary guard mitigation
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log("AttackBehavior", $"{enemy.gameObject.name} attack guarded. Applying reduced damage {dmg}.");
#endif
            }

            healthSystem.LoseHP(dmg);
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log("AttackBehavior", $"{enemy.gameObject.name} attacked {playerCollider.gameObject.name} for {dmg} damage.");
#endif
        }

        // Reset flag when hitbox is disabled
        // Call this at the end of each attack cycle
        private void ResetDamageFlag()
        {
            damageSentThisEnable = false;
        }

        private IEnumerator HandleAfterAttack()
        {
            // For crawlers with swarm disabled, skip back-away entirely
            if (enemy is BaseCrawlerEnemy crawler && !crawler.enableSwarmBehavior)
            {
                yield break;
            }

            // After attack finishes, before returning to swarm
            Vector3 awayDirection = (enemy.transform.position - playerTarget.position).normalized;
            float backupDistance = 2.0f; // Adjust as needed
            Vector3 backupTarget = enemy.transform.position + awayDirection * backupDistance;

            // Move the enemy back for a short time
            if (enemy.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
                enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().SetDestination(backupTarget);

            // Optionally, wait a short time before rejoining the swarm
            yield return WaitForSecondsCache.Get(0.5f);

            // Here you can add the code to make the enemy rejoin the swarm
        }
        public void Tick(BaseEnemy<TState, TTrigger> enemy)
        {
            // No per-frame logic needed for death
        }
    }
}