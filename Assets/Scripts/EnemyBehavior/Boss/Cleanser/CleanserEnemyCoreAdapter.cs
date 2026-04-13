using UnityEngine;

namespace EnemyBehavior.Boss.Cleanser
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CleanserBrain))]
    public class CleanserEnemyCoreAdapter : BaseEnemyCore
    {
        [SerializeField] private CleanserBrain brain;

        private bool wasAlive;

        public override bool isAlive => brain != null && brain.IsAlive;
        public override float currentHP => brain != null ? brain.currentHP : 0f;
        public override float maxHP => brain != null ? brain.maxHP : 0f;

        private void Awake()
        {
            brain ??= GetComponent<CleanserBrain>();
            wasAlive = isAlive;
        }

        private void Update()
        {
            bool aliveNow = isAlive;
            if (wasAlive && !aliveNow)
            {
                InvokeOnDeathStarted();
                InvokeOnDeath();
            }

            wasAlive = aliveNow;
        }

        public override void Spawn()
        {
            if (brain == null)
                return;

            brain.HealHP(maxHP);
            wasAlive = isAlive;
            InvokeOnSpawn();
        }

        public override void ResetEnemy()
        {
            if (brain == null)
                return;

            brain.HealHP(maxHP);
            wasAlive = isAlive;
            InvokeOnReset();
        }

        public override void HealHP(float hp)
        {
            brain?.HealHP(hp);
            wasAlive = isAlive;
        }

        public override void LoseHP(float damage)
        {
            if (brain == null)
                return;

            brain.LoseHP(damage);
        }
    }
}
