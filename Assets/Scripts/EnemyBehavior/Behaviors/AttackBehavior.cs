// AttackBehavior.cs
// Purpose: Handles attack state logic for enemies (melee/ranged), damage application, and hit detection.
// Works with: BaseEnemy attack triggers, EnemyProjectile, Player health systems, EnemyAttackQueueManager.
// Notes: Does not manage movement; only attack timing and hit application.

using System.Collections;
using EnemyBehavior;
using UnityEngine;
using Utilities.Combat;

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
        private TState attackStateValue;
        private bool damageSentThisEnable;

        private static readonly Collider[] hitBuffer = new Collider[16];

        public virtual void OnEnter(BaseEnemy<TState, TTrigger> enemy)
        {
            this.enemy = enemy;
            playerTarget = enemy.PlayerTarget;
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

            enemy.DisableAttackHitbox();
            ResetDamageFlag();
        }

        private IEnumerator LookAtPlayerLoop()
        {
            while (enemy.enemyAI.State.Equals(attackStateValue) && playerTarget != null)
            {
                if (enemy.IsParryStunned)
                {
                    yield return null;
                    continue;
                }

                if (!enemy.isAttackBoxActive)
                {
                    Vector3 direction = (playerTarget.position - enemy.transform.position).normalized;
                    direction.y = 0f;

                    if (direction != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(direction);
                        float t = 0f;
                        Quaternion startRotation = enemy.transform.rotation;

                        while (t < 1f && !enemy.isAttackBoxActive && !enemy.IsParryStunned)
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
            const int maxIterations = 10000;

            while (enemy.enemyAI.State.Equals(attackStateValue))
            {
                if (enemy.IsParryStunned)
                {
                    enemy.DisableAttackHitbox();
                    yield return null;
                    continue;
                }

                safetyCounter++;
                if (safetyCounter > maxIterations)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogError("AttackLoop exceeded max iterations! Breaking to prevent freeze.");
#endif
                    yield break;
                }

                if (!enemy.CanAttackFromQueue())
                {
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
                        Collider hit = hitBuffer[i];
                        if (!hit.CompareTag("Player"))
                            continue;

                        playerInAttackBox = true;
                        playerCollider = hit;
                        break;
                    }

                    if (playerInAttackBox)
                    {
                        enemy.NotifyAttackBegin();

                        if (!enemy.useAnimationEventAttacks)
                        {
                            enemy.EnableAttackHitbox();
                            DealDamageToPlayerOnce(playerCollider);
                        }

                        didAttack = true;
                        enemy.TriggerAttackAnimation();
                    }
                    else if (!enemy.useAnimationEventAttacks)
                    {
                        enemy.DisableAttackHitbox();
                    }
                }
                catch (System.Exception ex)
                {
#if UNITY_EDITOR
                    EnemyBehaviorDebugLogBools.LogError("Exception in AttackLoop: " + ex);
#endif
                    yield break;
                }

                if (!didAttack)
                {
                    yield return WaitForSecondsCache.Get(0.1f);
                    continue;
                }

                yield return WaitForSecondsCache.Get(enemy.attackActiveDuration);

                if (!enemy.useAnimationEventAttacks)
                    enemy.DisableAttackHitbox();

                ResetDamageFlag();

                while (enemy.IsParryStunned && enemy.enemyAI.State.Equals(attackStateValue))
                    yield return null;

                yield return WaitForSecondsCache.Get(enemy.attackInterval);
                enemy.NotifyAttackEnd();

                if (enemy is BaseCrawlerEnemy crawler)
                {
                    yield return HandleAfterAttack(crawler);
                    if (SwarmManager.Instance != null)
                        SwarmManager.Instance.RotateAttackers();
                }
            }

            enemy.DisableAttackHitbox();
            ResetDamageFlag();
        }

        private void DealDamageToPlayerOnce(Collider playerCollider)
        {
            if (damageSentThisEnable)
                return;

            damageSentThisEnable = true;

            if (!playerCollider.CompareTag("Player"))
                return;

            float dmg = enemy.damage;

            if (CombatManager.isParrying && enemy.canBeParried)
            {
                enemy.ApplyParryStun();
                CombatManager.ParrySuccessful();
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log("AttackBehavior", $"{enemy.gameObject.name} attack parried by player.");
#endif
                return;
            }

            playerCollider.TryGetComponent<IHealthSystem>(out IHealthSystem healthSystem);
            if (healthSystem == null)
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.LogWarning("AttackBehavior", $"{playerCollider.gameObject.name} has Player tag but no IHealthSystem component.");
#endif
                return;
            }

            if (CombatManager.isGuarding)
            {
                dmg *= 0.25f;
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log("AttackBehavior", $"{enemy.gameObject.name} attack guarded. Applying reduced damage {dmg}.");
#endif
            }

            healthSystem.LoseHP(dmg);
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log("AttackBehavior", $"{enemy.gameObject.name} attacked {playerCollider.gameObject.name} for {dmg} damage.");
#endif
        }

        private void ResetDamageFlag()
        {
            damageSentThisEnable = false;
        }

        private IEnumerator HandleAfterAttack(BaseCrawlerEnemy crawler)
        {
            if (!crawler.enableSwarmBehavior)
                yield break;

            Vector3 awayDirection = (enemy.transform.position - playerTarget.position).normalized;
            float backupDistance = 2.0f;
            Vector3 backupTarget = enemy.transform.position + awayDirection * backupDistance;

            UnityEngine.AI.NavMeshAgent navMeshAgent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navMeshAgent != null)
                navMeshAgent.SetDestination(backupTarget);

            yield return WaitForSecondsCache.Get(0.5f);
        }

        public void Tick(BaseEnemy<TState, TTrigger> enemy)
        {
            // No per-frame logic needed for death.
        }
    }
}
