using UnityEngine;

namespace EnemyBehavior.Boss.Cleanser
{
    public class CleanserSpinDashHitboxRelay : MonoBehaviour
    {
        public CleanserBrain Owner;

        private void OnTriggerEnter(Collider other)
        {
            Owner?.HandleSpinDashHitboxTrigger(other);
        }

        private void OnTriggerStay(Collider other)
        {
            Owner?.HandleSpinDashHitboxTrigger(other);
        }
    }
}
