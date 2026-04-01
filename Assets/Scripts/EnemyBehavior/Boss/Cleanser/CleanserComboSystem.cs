// CleanserComboSystem.cs
// Purpose: Designer-friendly combo system for the Cleanser boss.
// Works with: CleanserBrain, CleanserAttackType
// Allows designers to create combos via inspector with dropdowns for attack selection.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Represents a single attack step in a combo.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.dv62mfwpnfb8")]
    [System.Serializable]
    public class ComboStep
    {
        [Tooltip("The basic attack for this step.")]
        public CleanserBasicAttack BasicAttack = CleanserBasicAttack.None;
        
        [Tooltip("If true, this is the final step and uses a strong attack instead.")]
        public bool IsFinisher = false;
        
        [Tooltip("The strong attack to use if this is a finisher step.")]
        public CleanserStrongAttack StrongAttack = CleanserStrongAttack.None;
        
        [Tooltip("Delay before executing this step (seconds).")]
        public float PreDelay = 0f;
        
        [Tooltip("How many spare weapons to acquire into the hover stockpile before this step.")]
        [FormerlySerializedAs("PickupSpareWeaponBefore")]
        [Min(0)] public int SpareWeaponsToAddBeforeStep = 0;
        
        /// <summary>
        /// Returns true if this step has a valid attack assigned.
        /// </summary>
        public bool IsValid => IsFinisher 
            ? StrongAttack != CleanserStrongAttack.None 
            : BasicAttack != CleanserBasicAttack.None;
    }


    /// <summary>
    /// A complete combo sequence for the Cleanser boss.
    /// </summary>
    [System.Serializable]
    public class CleanserCombo
    {
        [Tooltip("Name for this combo (for debugging/identification).")]
        public string ComboName = "Combo";
        
        [Tooltip("Weight for random selection (higher = more likely to be chosen).")]
        [Range(1, 10)] public int SelectionWeight = 1;
        
        [Tooltip("Minimum distance to player for this combo to be eligible.")]
        public float MinRange = 0f;
        
        [Tooltip("Maximum distance to player for this combo to be eligible.")]
        public float MaxRange = 10f;
        
        [Tooltip("Cooldown after using this combo before it can be selected again.")]
        public float ComboCooldown = 5f;

        [Header("Movement")]
        [Tooltip("Multiplier applied to Cleanser walk/reposition speed while executing this combo.")]
        [Min(0.1f)] public float ComboMovementSpeedMultiplier = 1f;

        [Header("Aggression Requirements")]
        [Tooltip("Minimum aggression level required to use this combo (1-5).")]
        [Range(1, 5)] public int MinAggressionLevel = 1;
        
        [Tooltip("Maximum aggression level at which this combo can be used (1-5). Set to 5 for no upper limit.")]
        [Range(1, 5)] public int MaxAggressionLevel = 5;
        
        [Tooltip("The steps in this combo.")]
        public List<ComboStep> Steps = new List<ComboStep>();

        /// <summary>
        /// Returns true if this combo has at least one valid step.
        /// </summary>
        public bool IsValid => Steps != null && Steps.Count > 0 && Steps[0].IsValid;
        
        /// <summary>
        /// Returns the number of steps in this combo.
        /// </summary>
        public int StepCount => Steps?.Count ?? 0;
        
        /// <summary>
        /// Returns the index of the finisher step, or -1 if none exists.
        /// </summary>
        public int FinisherIndex
        {
            get
            {
                for (int i = 0; i < Steps.Count; i++)
                {
                    if (Steps[i].IsFinisher)
                        return i;
                }
                return -1;
            }
        }

        /// <summary>
        /// Checks if this combo is available at the given aggression level.
        /// </summary>
        public bool IsAvailableAtAggressionLevel(int level)
        {
            return level >= MinAggressionLevel && level <= MaxAggressionLevel;
        }
    }

    /// <summary>
    /// Manages combo selection and execution for the Cleanser boss.
    /// Attach to the same GameObject as CleanserBrain.
    /// </summary>
    public class CleanserComboSystem : MonoBehaviour
    {
        [Header("Combo Configuration")]
        [Tooltip("List of available combos. Designer can add/remove combos and configure steps.")]
        public List<CleanserCombo> Combos = new List<CleanserCombo>();

        [Header("Post-Finisher Settings")]
        [Tooltip("Recovery/cooldown time after completing a combo finisher (player attack window).")]
        public float PostFinisherRecovery = 3f;
        
        [Tooltip("If true, Cleanser is vulnerable to increased damage during post-finisher recovery.")]
        public bool VulnerableDuringRecovery = true;
        
        [Tooltip("Damage multiplier during post-finisher recovery.")]
        [Range(1f, 3f)] public float RecoveryDamageMultiplier = 1.5f;

        [Header("Spare Weapon Integration")]
        [Tooltip("If true, automatically picks up spare weapon before strong attack finishers.")]
        public bool AutoPickupBeforeStrongAttack = true;

        // Runtime state
        private Dictionary<string, float> comboCooldowns = new Dictionary<string, float>();
        private CleanserCombo currentCombo;
        private int currentStepIndex;
        private bool isExecutingCombo;
        private bool isInRecovery;
        private float recoveryEndTime;
        private CleanserAggressionSystem aggressionSystem;

        private void Awake()
        {
            aggressionSystem = GetComponent<CleanserAggressionSystem>();
        }

        /// <summary>
        /// Returns true if the system is currently executing a combo.
        /// </summary>
        public bool IsExecutingCombo => isExecutingCombo;
        
        /// <summary>
        /// Returns true if in post-finisher recovery (vulnerability window).
        /// </summary>
        public bool IsInRecovery => isInRecovery && Time.time < recoveryEndTime;
        
        /// <summary>
        /// Returns the damage multiplier based on current state.
        /// </summary>
        public float GetDamageMultiplier()
        {
            if (VulnerableDuringRecovery && IsInRecovery)
                return RecoveryDamageMultiplier;
            return 1f;
        }

        /// <summary>
        /// Selects a random eligible combo based on distance to player, cooldowns, and aggression level.
        /// </summary>
        /// <param name="distanceToPlayer">Current distance to the player.</param>
        /// <returns>Selected combo or null if none are eligible.</returns>
        public CleanserCombo SelectCombo(float distanceToPlayer)
        {
            int currentAggressionLevel = aggressionSystem != null ? (int)aggressionSystem.CurrentLevel : 1;
            return SelectCombo(distanceToPlayer, currentAggressionLevel);
        }

        /// <summary>
        /// Selects a random eligible combo based on distance to player, cooldowns, and aggression level.
        /// </summary>
        /// <param name="distanceToPlayer">Current distance to the player.</param>
        /// <param name="aggressionLevel">Current aggression level (1-5).</param>
        /// <returns>Selected combo or null if none are eligible.</returns>
        public CleanserCombo SelectCombo(float distanceToPlayer, int aggressionLevel)
        {
            List<CleanserCombo> eligible = new List<CleanserCombo>();
            int totalWeight = 0;

            foreach (var combo in Combos)
            {
                if (!combo.IsValid)
                    continue;
                    
                if (distanceToPlayer < combo.MinRange || distanceToPlayer > combo.MaxRange)
                    continue;
                    
                if (comboCooldowns.TryGetValue(combo.ComboName, out float cooldownEnd) && Time.time < cooldownEnd)
                    continue;

                // Check aggression level requirements
                if (!combo.IsAvailableAtAggressionLevel(aggressionLevel))
                    continue;
                    
                eligible.Add(combo);
                totalWeight += combo.SelectionWeight;
            }

            if (eligible.Count == 0)
                return null;

            // Weighted random selection
            int roll = Random.Range(0, totalWeight);
            int accumulated = 0;
            foreach (var combo in eligible)
            {
                accumulated += combo.SelectionWeight;
                if (roll < accumulated)
                    return combo;
            }

            return eligible[eligible.Count - 1];
        }

        /// <summary>
        /// Begins executing a combo.
        /// </summary>
        /// <param name="combo">The combo to execute.</param>
        public void StartCombo(CleanserCombo combo)
        {
            if (combo == null || !combo.IsValid)
                return;

            currentCombo = combo;
            currentStepIndex = 0;
            isExecutingCombo = true;
            isInRecovery = false;
            
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserComboSystem), $"[CleanserCombo] Starting combo: {combo.ComboName} with {combo.StepCount} steps.");

            ComboStep firstStep = GetCurrentStep();
            string firstAttackId = "None";
            if (firstStep != null)
            {
                firstAttackId = firstStep.IsFinisher
                    ? firstStep.StrongAttack.ToString()
                    : firstStep.BasicAttack.ToString();
            }

            EnemyBehaviorDebugLogBools.Log(
                nameof(CleanserComboSystem),
                $"[CleanserCombo] Step 1/{combo.StepCount}. Attack={firstAttackId}");
#endif
        }

        /// <summary>
        /// Gets the current step in the combo.
        /// </summary>
        public ComboStep GetCurrentStep()
        {
            if (currentCombo == null || currentStepIndex >= currentCombo.StepCount)
                return null;
                
            return currentCombo.Steps[currentStepIndex];
        }

        public ComboStep GetNextStep()
        {
            if (currentCombo == null)
                return null;

            int nextIndex = currentStepIndex + 1;
            if (nextIndex < 0 || nextIndex >= currentCombo.StepCount)
                return null;

            return currentCombo.Steps[nextIndex];
        }

        /// <summary>
        /// Advances to the next step in the combo.
        /// </summary>
        /// <returns>True if there's another step, false if combo is complete.</returns>
        public bool AdvanceStep()
        {
            if (currentCombo == null)
                return false;
                
            currentStepIndex++;
            
            if (currentStepIndex >= currentCombo.StepCount)
            {
                CompleteCombo();
                return false;
            }
            
#if UNITY_EDITOR
            ComboStep currentStep = GetCurrentStep();
            string attackId = "None";
            if (currentStep != null)
            {
                attackId = currentStep.IsFinisher
                    ? currentStep.StrongAttack.ToString()
                    : currentStep.BasicAttack.ToString();
            }

            EnemyBehaviorDebugLogBools.Log(
                nameof(CleanserComboSystem),
                $"[CleanserCombo] Advanced to step {currentStepIndex + 1}/{currentCombo.StepCount}. Attack={attackId}");
#endif
            return true;
        }

        /// <summary>
        /// Completes the current combo and enters recovery if it had a finisher.
        /// </summary>
        private void CompleteCombo()
        {
            if (currentCombo == null)
                return;

            // Set cooldown for this combo
            comboCooldowns[currentCombo.ComboName] = Time.time + currentCombo.ComboCooldown;
            
            // Check if the last step was a finisher
            bool hadFinisher = currentCombo.Steps.Count > 0 && 
                               currentCombo.Steps[currentCombo.Steps.Count - 1].IsFinisher;
            
            if (hadFinisher)
            {
                isInRecovery = true;
                recoveryEndTime = Time.time + PostFinisherRecovery;
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserComboSystem), $"[CleanserCombo] Combo {currentCombo.ComboName} complete. Entering {PostFinisherRecovery}s recovery.");
#endif
            }
            else
            {
#if UNITY_EDITOR
                EnemyBehaviorDebugLogBools.Log(nameof(CleanserComboSystem), $"[CleanserCombo] Combo {currentCombo.ComboName} complete (no finisher).");
#endif
            }
            
            isExecutingCombo = false;
            currentCombo = null;
            currentStepIndex = 0;
        }

        /// <summary>
        /// Cancels the current combo immediately.
        /// </summary>
        public void CancelCombo()
        {
            if (!isExecutingCombo)
                return;
                
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserComboSystem), $"[CleanserCombo] Combo canceled at step {currentStepIndex + 1}.");
#endif
            
            isExecutingCombo = false;
            currentCombo = null;
            currentStepIndex = 0;
        }

        /// <summary>
        /// Checks if a step requires picking up a spare weapon.
        /// </summary>
        public bool ShouldPickupWeaponForStep(ComboStep step)
        {
            if (step == null)
                return false;

            return GetSpareWeaponPickupCountForStep(step) > 0;
        }

        public int GetSpareWeaponPickupCountForStep(ComboStep step)
        {
            if (step == null)
                return 0;

            int explicitCount = Mathf.Max(0, step.SpareWeaponsToAddBeforeStep);
            if (explicitCount > 0)
                return explicitCount;

            if (AutoPickupBeforeStrongAttack && step.IsFinisher && step.StrongAttack != CleanserStrongAttack.None)
                return 1;

            return 0;
        }

        /// <summary>
        /// Returns the recovery time remaining, or 0 if not in recovery.
        /// </summary>
        public float GetRecoveryTimeRemaining()
        {
            if (!IsInRecovery)
                return 0f;
            return Mathf.Max(0f, recoveryEndTime - Time.time);
        }

        /// <summary>
        /// Forces recovery to end early (e.g., if boss is hit hard enough).
        /// </summary>
        public void EndRecoveryEarly()
        {
            isInRecovery = false;
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(CleanserComboSystem), "[CleanserCombo] Recovery ended early.");
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor helper to create a default combo structure.
        /// </summary>
        [ContextMenu("Add Sample Combo")]
        private void AddSampleCombo()
        {
            var combo = new CleanserCombo
            {
                ComboName = $"Combo_{Combos.Count + 1}",
                SelectionWeight = 1,
                MinRange = 0f,
                MaxRange = 8f,
                ComboCooldown = 5f,
                Steps = new List<ComboStep>
                {
                    new ComboStep { BasicAttack = CleanserBasicAttack.Lunge },
                    new ComboStep { BasicAttack = CleanserBasicAttack.OverheadCleave },
                    new ComboStep { IsFinisher = true, StrongAttack = CleanserStrongAttack.HighDive }
                }
            };
            Combos.Add(combo);
        }
#endif
    }
}
