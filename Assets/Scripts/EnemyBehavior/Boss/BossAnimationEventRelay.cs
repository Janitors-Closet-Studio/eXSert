using UnityEngine;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Relay for Animation Events when Mediator is on parent GameObject.
    /// Attach to same GameObject as Animator (child).
    /// Forwards all Animation Events to BossAnimationEventMediator on parent.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class BossAnimationEventRelay : MonoBehaviour
    {
        private BossAnimationEventMediator mediator;

        private void Awake()
        {
            // Find mediator on parent or self
            mediator = GetComponentInParent<BossAnimationEventMediator>();
            
            if (mediator == null)
            {
                EnemyBehaviorDebugLogBools.LogError("[BossAnimationEventRelay] No BossAnimationEventMediator found on parent! Animation Events will fail.");
            }
        }

        // Arm Events
        public void EnableLeftArm() => mediator?.EnableLeftArm();
        public void DisableLeftArm() => mediator?.DisableLeftArm();
        public void EnableRightArm() => mediator?.EnableRightArm();
        public void DisableRightArm() => mediator?.DisableRightArm();
        public void EnableBothArms() => mediator?.EnableBothArms();
        public void DisableBothArms() => mediator?.DisableBothArms();

        // Spin Events
        public void EnableSpin() => mediator?.EnableSpin();
        public void DisableSpin() => mediator?.DisableSpin();

        // Charge Events
        public void EnableCharge() => mediator?.EnableCharge();
        public void DisableCharge() => mediator?.DisableCharge();

        // Arms Deploy Events
        public void OnArmsDeployComplete() => mediator?.OnArmsDeployComplete();
        public void OnArmsRetractComplete() => mediator?.OnArmsRetractComplete();
    }
}
