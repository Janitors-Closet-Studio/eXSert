using UnityEngine;

/// <summary>
/// Diagnostic tool: attach to Boss_Roomba to log all animator.SetTrigger() calls.
/// This helps debug why animations aren't playing.
/// </summary>
public class BossAnimatorDebugger : MonoBehaviour
{
    private Animator animator;
    
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            EnemyBehaviorDebugLogBools.LogError("[BossAnimatorDebugger] No Animator found!");
            return;
        }

        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"[BossAnimatorDebugger] Monitoring animator on '{animator.gameObject.name}'");

        // CRITICAL DIAGNOSTICS - Only log if enabled
        if (!DebugLogSettingsM.IsLogEnabled(DebugLogCategory.AnimatorDebug)) return;

        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"[BossAnimatorDebugger] ===== ANIMATOR CONFIGURATION =====");
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"  Animator Enabled: {animator.enabled}");
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"  Animator Speed: {animator.speed}");
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"  Controller Assigned: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NONE - THIS IS THE PROBLEM!")}");
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"  Update Mode: {animator.updateMode}");
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"  Culling Mode: {animator.cullingMode}");
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"  Layer Count: {animator.layerCount}");

        for (int i = 0; i < animator.layerCount; i++)
        {
            DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"    Layer {i}: {animator.GetLayerName(i)}, Weight: {animator.GetLayerWeight(i)}");
        }
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"==========================================");

        LogAnimatorParameters();
    }
    
    private void Update()
    {
        if (animator == null) return;
        
        // Log current state every frame (can be noisy, toggle off if needed)
        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        var stateName = GetCurrentStateName();
        
        if (Input.GetKeyDown(KeyCode.F5))
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"[BossAnimatorDebugger] ===== CURRENT STATE =====");
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  State: {stateName}");
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  State Hash: {stateInfo.shortNameHash}");
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  Normalized Time: {stateInfo.normalizedTime:F2}");
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  Speed Param: {animator.GetFloat("Speed"):F2}");
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  IsMoving Param: {animator.GetBool("IsMoving")}");
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  Animator.speed: {animator.speed}");
            
            // Check if any transitions are active
            if (animator.IsInTransition(0))
            {
                var transInfo = animator.GetAnimatorTransitionInfo(0);
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  IN TRANSITION - Progress: {transInfo.normalizedTime:F2}");
            }
            else
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  Not in transition");
            }
            
            // Check for triggers that are set
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  Checking trigger states...");
            foreach (var param in animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger)
                {
                    bool isSet = animator.GetBool(param.nameHash); // Triggers are stored as bools internally
                    if (isSet)
                    {
                        EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"    TRIGGER SET: {param.name}");
                    }
                }
            }
            
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"=============================");
            LogAnimatorParameters();
            
            // Check next state info
            var nextInfo = animator.GetNextAnimatorStateInfo(0);
            if (nextInfo.fullPathHash != 0)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"  Next State Hash: {nextInfo.shortNameHash}");
            }
        }
        
        // Auto-log every 2 seconds to catch state changes
        if (Time.frameCount % 120 == 0) // Every 2 seconds at 60fps
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossAnimatorDebugger), $"[BossAnimatorDebugger] Current state: {stateName}, Normalized time: {stateInfo.normalizedTime:F2}");
        }
    }
    
    private string GetCurrentStateName()
    {
        if (animator == null) return "None";
        
        var clips = animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length > 0)
        {
            return clips[0].clip.name;
        }
        
        // Fallback: try to get state name from state info
        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        
        // Try to find matching state by hash
        var controller = animator.runtimeAnimatorController;
        if (controller != null)
        {
            // This won't give us the name directly, but at least we have the hash
            return $"StateHash_{stateInfo.shortNameHash}";
        }
        
        return "Unknown";
    }
    
    private void LogAnimatorParameters()
    {
        if (animator == null) return;

        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, "[BossAnimatorDebugger] ===== ANIMATOR PARAMETERS =====");
        foreach (var param in animator.parameters)
        {
            string value = param.type switch
            {
                AnimatorControllerParameterType.Float => animator.GetFloat(param.name).ToString("F2"),
                AnimatorControllerParameterType.Int => animator.GetInteger(param.name).ToString(),
                AnimatorControllerParameterType.Bool => animator.GetBool(param.name).ToString(),
                AnimatorControllerParameterType.Trigger => "(trigger)",
                _ => "Unknown"
            };

            DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, $"  {param.type} '{param.name}' = {value}");
        }
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.AnimatorDebug, "==========================================");
    }
}
