using UnityEngine;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Mediator for Animation Events to control hitboxes.
    /// Attach to the GameObject with the Animator component.
    /// Animation Events call methods on this script, which forwards to appropriate hitboxes.
    /// </summary>
    public sealed class BossAnimationEventMediator : MonoBehaviour
    {
        [Header("Hitbox References")]
        [SerializeField, Tooltip("Hitbox for left arm attacks")]
        private BossArmHitbox leftArmHitbox;

        [SerializeField, Tooltip("Hitbox for right arm attacks")]
        private BossArmHitbox rightArmHitbox;

        [SerializeField, Tooltip("Hitbox for center/both arm attacks")]
        private BossArmHitbox centerArmHitbox;

        [SerializeField, Tooltip("Hitbox for spin attack (usually body-based)")]
        private BossArmHitbox spinHitbox;

        [SerializeField, Tooltip("Hitbox for charge attacks (body-based)")]
        private BossArmHitbox chargeHitbox;

        [Header("Boss Brain Reference")]
        [SerializeField, Tooltip("Boss brain for arm deploy/retract callbacks (auto-found if null)")]
        private BossRoombaBrain bossBrain;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip windupSound;
        [SerializeField] private AudioClip swingSound;
        [SerializeField] private AudioClip hitSound;
        [SerializeField] private AudioClip recoverySound;

        private void Awake()
        {
            if (bossBrain == null)
            {
                bossBrain = GetComponentInParent<BossRoombaBrain>();
            }
        }

        private void OnValidate()
        {
            if (leftArmHitbox == null || rightArmHitbox == null || centerArmHitbox == null ||
                spinHitbox == null || chargeHitbox == null)
            {
                var hitboxes = GetComponentsInChildren<BossArmHitbox>(true);
                foreach (var hitbox in hitboxes)
                {
                    if (hitbox.name.ToLower().Contains("left") && leftArmHitbox == null)
                        leftArmHitbox = hitbox;
                    else if (hitbox.name.ToLower().Contains("right") && rightArmHitbox == null)
                        rightArmHitbox = hitbox;
                    else if (hitbox.name.ToLower().Contains("center") && centerArmHitbox == null)
                        centerArmHitbox = hitbox;
                    else if (hitbox.name.ToLower().Contains("spin") && spinHitbox == null)
                        spinHitbox = hitbox;
                    else if (hitbox.name.ToLower().Contains("charge") && chargeHitbox == null)
                        chargeHitbox = hitbox;
                }
            }

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            
            if (bossBrain == null)
                bossBrain = GetComponentInParent<BossRoombaBrain>();
        }

        #region Left Arm Events
        public void EnableLeftArm()
        {
            if (leftArmHitbox != null)
            {
                leftArmHitbox.EnableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Left arm enabled");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossAnimationEventMediator), "[AnimMediator] Left arm hitbox not assigned!");
            }
        }

        public void DisableLeftArm()
        {
            if (leftArmHitbox != null)
            {
                leftArmHitbox.DisableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Left arm disabled");
            }
        }
        #endregion

        #region Right Arm Events
        public void EnableRightArm()
        {
            if (rightArmHitbox != null)
            {
                rightArmHitbox.EnableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Right arm enabled");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossAnimationEventMediator), "[AnimMediator] Right arm hitbox not assigned!");
            }
        }

        public void DisableRightArm()
        {
            if (rightArmHitbox != null)
            {
                rightArmHitbox.DisableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Right arm disabled");
            }
        }
        #endregion

        #region Center/Both Arms Events
        public void EnableCenterArm()
        {
            if (centerArmHitbox != null)
            {
                centerArmHitbox.EnableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Center arm enabled");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossAnimationEventMediator), "[AnimMediator] Center arm hitbox not assigned!");
            }
        }

        public void DisableCenterArm()
        {
            if (centerArmHitbox != null)
            {
                centerArmHitbox.DisableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Center arm disabled");
            }
        }


        public void EnableBothArms()
        {
            EnableLeftArm();
            EnableRightArm();
        }
        
        /// <summary>
        /// Enable both arms with dash knockback mode.
        /// Used during dashes to apply knockback even if arm hitboxes don't normally have it enabled.
        /// </summary>
        public void EnableBothArmsWithDashKnockback(float forceOverride = 0f)
        {
            if (leftArmHitbox != null)
            {
                leftArmHitbox.EnableHitboxWithDashKnockback(forceOverride);
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Left arm enabled with DASH KNOCKBACK");
            }
            if (rightArmHitbox != null)
            {
                rightArmHitbox.EnableHitboxWithDashKnockback(forceOverride);
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Right arm enabled with DASH KNOCKBACK");
            }
        }

        public void DisableBothArms()
        {
            DisableLeftArm();
            DisableRightArm();
        }
        #endregion

        #region Spin Attack Events
        public void EnableSpin()
        {
            if (spinHitbox != null)
            {
                spinHitbox.EnableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Spin hitbox enabled");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossAnimationEventMediator), "[AnimMediator] Spin hitbox not assigned!");
            }
        }

        public void DisableSpin()
        {
            if (spinHitbox != null)
            {
                spinHitbox.DisableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Spin hitbox disabled");
            }
        }
        #endregion

        #region Charge Attack Events
        public void EnableCharge()
        {
            if (chargeHitbox != null)
            {
                chargeHitbox.EnableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Charge hitbox enabled");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossAnimationEventMediator), "[AnimMediator] Charge hitbox not assigned!");
            }
        }

        /// <summary>
        /// Enables the charge hitbox with dash knockback mode.
        /// Used for DashLungeNoArms where the charge hitbox needs to apply knockback like arms do.
        /// </summary>
        public void EnableChargeWithDashKnockback(float forceOverride = 0f)
        {
            if (chargeHitbox != null)
            {
                chargeHitbox.EnableHitboxWithDashKnockback(forceOverride);
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Charge hitbox enabled with DASH KNOCKBACK");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossAnimationEventMediator), "[AnimMediator] Charge hitbox not assigned!");
            }
        }

        public void DisableCharge()
        {
            if (chargeHitbox != null)
            {
                chargeHitbox.DisableHitbox();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Charge hitbox disabled");
            }
        }
        #endregion

        #region Arms Deploy/Retract Events
        /// <summary>
        /// Called by Animation Event at end of Arms_Deploy clip.
        /// Forwards to BossRoombaBrain.
        /// </summary>
        public void OnArmsDeployComplete()
        {
            if (bossBrain != null)
            {
                bossBrain.OnArmsDeployComplete();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Arms deployment complete - forwarded to brain");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossAnimationEventMediator), "[AnimMediator] Boss brain not found! Cannot forward OnArmsDeployComplete");
            }
        }

        /// <summary>
        /// Called by Animation Event at end of Arms_Retract clip.
        /// Forwards to BossRoombaBrain.
        /// </summary>
        public void OnArmsRetractComplete()
        {
            if (bossBrain != null)
            {
                bossBrain.OnArmsRetractComplete();
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Arms retract complete - forwarded to brain");
            }
            else
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossAnimationEventMediator), "[AnimMediator] Boss brain not found! Cannot forward OnArmsRetractComplete");
            }
        }
        #endregion

        #region Disable All
        public void DisableAllHitboxes()
        {
            DisableLeftArm();
            DisableRightArm();
            DisableCenterArm();
            DisableSpin();
            DisableCharge();
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] All hitboxes disabled");
        }
        #endregion

        #region Audio Events
        public void PlayWindupSound()
        {
            PlaySound(windupSound);
        }

        public void PlaySwingSound()
        {
            PlaySound(swingSound);
        }

        public void PlayHitSound()
        {
            PlaySound(hitSound);
        }

        public void PlayRecoverySound()
        {
            PlaySound(recoverySound);
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
        #endregion

        #region Visual Effects Events
        public void SpawnWindupEffect()
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Windup effect triggered");
        }

        public void SpawnHitEffect()
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Hit effect triggered");
        }

        public void SpawnRecoveryEffect()
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimationEventMediator), "[AnimMediator] Recovery effect triggered");
        }
        #endregion

        private void OnDisable()
        {
            DisableAllHitboxes();
        }
    }
}
