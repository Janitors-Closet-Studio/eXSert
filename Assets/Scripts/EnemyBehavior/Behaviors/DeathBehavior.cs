// DeathBehavior.cs
// Purpose: Handles enemy death sequence: disabling, playing SFX, cleanup, and object destruction.
// Works with: BaseEnemy, BaseCrawlerEnemy pocket removal, EnemyHealthBar UI.

using UnityEngine;
using System.Collections;

namespace Behaviors
{
    public class DeathBehavior<TState, TTrigger> : IEnemyStateBehavior<TState, TTrigger>
        where TState : struct, System.Enum
        where TTrigger : struct, System.Enum
    {
        private Coroutine deathSequenceCoroutine;
        private BaseEnemy<TState, TTrigger> enemy;

        public virtual void OnEnter(BaseEnemy<TState, TTrigger> enemy)
        {
            this.enemy = enemy;

            // Disable movement and other components
            if (enemy.agent != null)
                enemy.agent.enabled = false;

            // Optionally set a "dead" color or visual
            enemy.SetEnemyColor(Color.black);

            // Start the death sequence coroutine
            if (deathSequenceCoroutine != null)
                enemy.StopCoroutine(deathSequenceCoroutine);
            deathSequenceCoroutine = enemy.StartCoroutine(DeathSequence());
        }

        public virtual void OnExit(BaseEnemy<TState, TTrigger> enemy)
        {
            if (deathSequenceCoroutine != null)
            {
                enemy.StopCoroutine(deathSequenceCoroutine);
                deathSequenceCoroutine = null;
            }
        }

        private IEnumerator DeathSequence()
        {
            // Wait a few seconds before playing SFX
            yield return WaitForSecondsCache.Get(2f);

            // Play SFX (placeholder, replace with actual SFX logic)
            PlayDeathSFX();

            // Wait for SFX duration
            yield return WaitForSecondsCache.Get(1f);

            // Death animation/sequence is now complete - fire the OnDeath event
            enemy.OnDeathSequenceComplete();

            // Hide authored helper roots that live under the pooled enemy root,
            // and clean up any runtime helpers that were registered externally.
            enemy.CleanupExternalHelpersForDeath();

            // Hide health bar but don't destroy it (can be re-enabled on reset)
            if (enemy.healthBarInstance != null)
            {
                enemy.healthBarInstance.gameObject.SetActive(false);
            }

            // Only remove from pocket if this is a crawler
            if (enemy is BaseCrawlerEnemy crawler && crawler.Pocket != null)
            {
                crawler.Pocket.activeEnemies.Remove(crawler);
            }

            // Disable instead of destroy for pooling/encounter reset support
            enemy.gameObject.SetActive(false);
        }

        private void PlayDeathSFX()
        {
            // Placeholder for SFX logic
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log("DeathBehavior", $"{enemy.gameObject.name} death SFX played.");
#endif
        }
        public void Tick(BaseEnemy<TState, TTrigger> enemy)
        {
            // No per-frame logic needed for death
        }
    }
}