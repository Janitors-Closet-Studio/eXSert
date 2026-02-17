using UnityEngine;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Attach to the zone collider that stays on the boss.
    /// Implements IHealthSystem so player weapons can damage panels directly using their own damage values.
    /// 
    /// SETUP:
    /// 1. Create zone GameObject with Collider (BoxCollider recommended)
    /// 2. Tag the zone as "Enemy" so player weapons detect it
    /// 3. Add this script to the zone GameObject
    /// 4. In BossRoombaBrain.SidePanels, assign the visual mesh to panelVisualMesh
    /// 
    /// FLOW:
    /// - Player weapon hits zone → LoseHP() called with weapon's damage
    /// - Before break: Damage reduces panel health in BossRoombaBrain
    /// - After break: Damage is amplified and applied to boss
    /// </summary>
    public sealed class BossSidePanelCollider : MonoBehaviour, IHealthSystem
    {
        [SerializeField, Tooltip("Index of this panel in the boss's SidePanels list (0-7)")]
        private int panelIndex;

        [SerializeField, Tooltip("Reference to boss brain (auto-finds parent if null)")]
        private BossRoombaBrain bossBrain;

        [Header("Audio/Visual Feedback")]
        [SerializeField, Tooltip("Optional: AudioSource to play hit sound")]
        private AudioSource hitAudioSource;
        [SerializeField, Tooltip("Sound clips for hits before panel breaks")]
        private AudioClip[] panelHitSounds;
        [SerializeField, Tooltip("Sound clips for hits after panel breaks (vulnerable)")]
        private AudioClip[] vulnerableHitSounds;
        [SerializeField, Tooltip("Sound clips for when the panel breaks and falls off")]
        private AudioClip[] panelBreakSounds;

        /// <summary>
        /// Current health of this panel. Synced from BossRoombaBrain.SidePanels.
        /// </summary>
        public float currentHP
        {
            get
            {
                if (bossBrain == null || panelIndex < 0 || panelIndex >= bossBrain.SidePanels.Count)
                    return 0f;
                return bossBrain.SidePanels[panelIndex].currentHealth;
            }
        }

        /// <summary>
        /// Max health of this panel. Read from BossRoombaBrain.SidePanels.
        /// </summary>
        public float maxHP
        {
            get
            {
                if (bossBrain == null || panelIndex < 0 || panelIndex >= bossBrain.SidePanels.Count)
                    return 100f;
                return bossBrain.SidePanels[panelIndex].maxHealth;
            }
        }

        private void OnValidate()
        {
            if (bossBrain == null)
            {
                bossBrain = GetComponentInParent<BossRoombaBrain>();
            }
            
            // Ensure the zone is tagged as Enemy so player weapons detect it
            if (!gameObject.CompareTag("Enemy"))
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossSidePanelCollider), $"[BossSidePanelCollider] {gameObject.name} should be tagged 'Enemy' for player weapons to detect it!");
            }
        }

        private void Start()
        {
            if (bossBrain == null)
            {
                bossBrain = GetComponentInParent<BossRoombaBrain>();
                if (bossBrain == null)
                {
                    EnemyBehaviorDebugLogBools.LogError($"[BossSidePanelCollider] No BossRoombaBrain found for panel {panelIndex} on {gameObject.name}!");
                }
            }
            
            // Warn if not tagged as Enemy
            if (!gameObject.CompareTag("Enemy"))
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossSidePanelCollider), $"[BossSidePanelCollider] {gameObject.name} is not tagged 'Enemy' - player weapons won't detect it!");
            }
        }

        /// <summary>
        /// Called by player weapon systems (HitboxDamageManager) via IHealthSystem.
        /// Damage amount comes from the player's weapon, not a fixed value.
        /// </summary>
        public void LoseHP(float damage)
        {
            if (bossBrain == null) return;

            // Check if panel is already destroyed (vulnerable state)
            if (bossBrain.IsPanelDestroyed(panelIndex))
            {
                // Panel is gone - this is now a vulnerable zone hit
                bossBrain.DamageVulnerableZone(panelIndex, damage);
                PlayHitSound(vulnerableHitSounds);
            }
            else
            {
                // Panel still intact - damage the panel
                bossBrain.DamageSidePanel(panelIndex, damage);
                PlayHitSound(panelHitSounds);
            }
        }

        /// <summary>
        /// Not used for panels, but required by IHealthSystem interface.
        /// </summary>
        public void HealHP(float hp)
        {
            // Panels don't heal, but we could add this functionality later
            EnemyBehaviorDebugLogBools.Log(nameof(BossSidePanelCollider), $"[BossSidePanelCollider] HealHP called on panel {panelIndex} - panels don't heal.");
        }

        private void PlayHitSound(AudioClip[] clips)
        {
            if (hitAudioSource == null || clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip != null)
            {
                hitAudioSource.PlayOneShot(clip);
            }
        }

        /// <summary>
        /// Plays the panel break sound effect. Called by BossRoombaBrain when the panel is destroyed.
        /// </summary>
        public void PlayPanelBreakSound()
        {
            PlayHitSound(panelBreakSounds);
        }

        /// <summary>
        /// Returns the panel index for external systems.
        /// </summary>
        public int PanelIndex => panelIndex;
    }
}
