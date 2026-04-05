using UnityEngine;

public class DebugLogSettingsM : MonoBehaviour
{
    public static DebugLogSettingsM Instance { get; private set; }

    [Header("=== SYSTEM LOGS ===")]
    [SerializeField] private bool enableSingletonLogs = false;
    [SerializeField] private bool enableSceneLoadingLogs = false;
    [SerializeField] private bool enableInputLogs = false;
    [SerializeField] private bool enableSettingsLogs = false;
    [SerializeField] private bool enableAnimatorDebugLogs = false;
    [SerializeField] private bool enableUILogs = false;
    [SerializeField] private bool enableAudioLogs = false;

    [Header("=== ENEMY TYPES ===")]
    [SerializeField] private bool enableBaseEnemyLogs = false;
    [SerializeField] private bool enableBaseCrawlerEnemyLogs = false;
    [SerializeField] private bool enableBoxerEnemyLogs = false;
    [SerializeField] private bool enableDroneEnemyLogs = false;
    [SerializeField] private bool enableAlarmCarrierEnemyLogs = false;
    [SerializeField] private bool enableBombCarrierEnemyLogs = false;
    [SerializeField] private bool enableTestingEnemyLogs = false;

    [Header("=== BEHAVIORS ===")]
    [SerializeField] private bool enableAttackBehaviorLogs = false;
    [SerializeField] private bool enableDeathBehaviorLogs = false;
    [SerializeField] private bool enableIdleBehaviorLogs = false;
    [SerializeField] private bool enableFleeBehaviorLogs = false;
    [SerializeField] private bool enableDroneIdleBehaviorLogs = false;
    [SerializeField] private bool enableDroneRelocateBehaviorLogs = false;

    [Header("=== MANAGERS ===")]
    [SerializeField] private bool enableSwarmManagerLogs = false;
    [SerializeField] private bool enableDroneSwarmManagerLogs = false;
    [SerializeField] private bool enableEnemyAttackQueueManagerLogs = false;
    [SerializeField] private bool enableEnemyHealthManagerLogs = false;
    [SerializeField] private bool enableCrowdControllerLogs = false;
    [SerializeField] private bool enableScenePoolManagerLogs = false;

    [Header("=== BOSS ===")]
    [SerializeField] private bool enableBossRoombaControllerLogs = false;
    [SerializeField] private bool enableBossRoombaBrainLogs = false;
    [SerializeField] private bool enableBossArenaManagerLogs = false;
    [SerializeField] private bool enableBossHealthLogs = false;
    [SerializeField] private bool enableBossTopZoneLogs = false;
    [SerializeField] private bool enableBossSidePanelColliderLogs = false;
    [SerializeField] private bool enableBossPillarColliderLogs = false;
    [SerializeField] private bool enableBossPlayerEjectorLogs = false;
    [SerializeField] private bool enableBossScenePlayerManagerLogs = false;
    [SerializeField] private bool enableVacuumSuctionEffectLogs = false;
    [SerializeField] private bool enableArenaWallColliderLogs = false;
    [SerializeField] private bool enableBossAnimationEventMediatorLogs = false;
    [SerializeField] private bool enableBossAnimationEventRelayLogs = false;
    [SerializeField] private bool enableBossAnimatorDebuggerLogs = false;
    [SerializeField] private bool enableBossArmHitboxLogs = false;
    [SerializeField] private bool enableBossAlarmDamageReceiverLogs = false;

    [Header("=== PATHFINDING ===")]
    [SerializeField] private bool enableNavMeshAStarPlannerLogs = false;
    [SerializeField] private bool enablePathRequestManagerLogs = false;
    [SerializeField] private bool enableAutomatedPathTestRunnerLogs = false;

    [Header("=== MISC ===")]
    [SerializeField] private bool enableEnemyProjectileLogs = false;
    [SerializeField] private bool enableExplosiveEnemyProjectileLogs = false;
    [SerializeField] private bool enableEnemyDeathSoundConfigLogs = false;
    [SerializeField] private bool enableGeneralDebugLogs = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static bool IsLogEnabled(DebugLogCategory category)
    {
        if (Instance == null) return false;
        return category switch {
            DebugLogCategory.Singleton => Instance.enableSingletonLogs,
            DebugLogCategory.SceneLoading => Instance.enableSceneLoadingLogs,
            DebugLogCategory.Input => Instance.enableInputLogs,
            DebugLogCategory.Settings => Instance.enableSettingsLogs,
            DebugLogCategory.AnimatorDebug => Instance.enableAnimatorDebugLogs,
            DebugLogCategory.UI => Instance.enableUILogs,
            DebugLogCategory.Audio => Instance.enableAudioLogs,
            DebugLogCategory.BaseEnemy => Instance.enableBaseEnemyLogs,
            DebugLogCategory.BaseCrawlerEnemy => Instance.enableBaseCrawlerEnemyLogs,
            DebugLogCategory.BoxerEnemy => Instance.enableBoxerEnemyLogs,
            DebugLogCategory.DroneEnemy => Instance.enableDroneEnemyLogs,
            DebugLogCategory.AlarmCarrierEnemy => Instance.enableAlarmCarrierEnemyLogs,
            DebugLogCategory.BombCarrierEnemy => Instance.enableBombCarrierEnemyLogs,
            DebugLogCategory.TestingEnemy => Instance.enableTestingEnemyLogs,
            DebugLogCategory.AttackBehavior => Instance.enableAttackBehaviorLogs,
            DebugLogCategory.DeathBehavior => Instance.enableDeathBehaviorLogs,
            DebugLogCategory.IdleBehavior => Instance.enableIdleBehaviorLogs,
            DebugLogCategory.FleeBehavior => Instance.enableFleeBehaviorLogs,
            DebugLogCategory.DroneIdleBehavior => Instance.enableDroneIdleBehaviorLogs,
            DebugLogCategory.DroneRelocateBehavior => Instance.enableDroneRelocateBehaviorLogs,
            DebugLogCategory.SwarmManager => Instance.enableSwarmManagerLogs,
            DebugLogCategory.DroneSwarmManager => Instance.enableDroneSwarmManagerLogs,
            DebugLogCategory.EnemyAttackQueueManager => Instance.enableEnemyAttackQueueManagerLogs,
            DebugLogCategory.EnemyHealthManager => Instance.enableEnemyHealthManagerLogs,
            DebugLogCategory.CrowdController => Instance.enableCrowdControllerLogs,
            DebugLogCategory.ScenePoolManager => Instance.enableScenePoolManagerLogs,
            DebugLogCategory.BossRoombaController => Instance.enableBossRoombaControllerLogs,
            DebugLogCategory.BossRoombaBrain => Instance.enableBossRoombaBrainLogs,
            DebugLogCategory.BossArenaManager => Instance.enableBossArenaManagerLogs,
            DebugLogCategory.BossHealth => Instance.enableBossHealthLogs,
            DebugLogCategory.BossTopZone => Instance.enableBossTopZoneLogs,
            DebugLogCategory.BossSidePanelCollider => Instance.enableBossSidePanelColliderLogs,
            DebugLogCategory.BossPillarCollider => Instance.enableBossPillarColliderLogs,
            DebugLogCategory.BossPlayerEjector => Instance.enableBossPlayerEjectorLogs,
            DebugLogCategory.BossScenePlayerManager => Instance.enableBossScenePlayerManagerLogs,
            DebugLogCategory.VacuumSuctionEffect => Instance.enableVacuumSuctionEffectLogs,
            DebugLogCategory.ArenaWallCollider => Instance.enableArenaWallColliderLogs,
            DebugLogCategory.BossAnimationEventMediator => Instance.enableBossAnimationEventMediatorLogs,
            DebugLogCategory.BossAnimationEventRelay => Instance.enableBossAnimationEventRelayLogs,
            DebugLogCategory.BossAnimatorDebugger => Instance.enableBossAnimatorDebuggerLogs,
            DebugLogCategory.BossArmHitbox => Instance.enableBossArmHitboxLogs,
            DebugLogCategory.BossAlarmDamageReceiver => Instance.enableBossAlarmDamageReceiverLogs,
            DebugLogCategory.NavMeshAStarPlanner => Instance.enableNavMeshAStarPlannerLogs,
            DebugLogCategory.PathRequestManager => Instance.enablePathRequestManagerLogs,
            DebugLogCategory.AutomatedPathTestRunner => Instance.enableAutomatedPathTestRunnerLogs,
            DebugLogCategory.EnemyProjectile => Instance.enableEnemyProjectileLogs,
            DebugLogCategory.ExplosiveEnemyProjectile => Instance.enableExplosiveEnemyProjectileLogs,
            DebugLogCategory.EnemyDeathSoundConfig => Instance.enableEnemyDeathSoundConfigLogs,
            DebugLogCategory.General => Instance.enableGeneralDebugLogs,
            _ => false
        };
    }

    public static void ConditionalLog(DebugLogCategory category, string message, Object context = null)
    {
        if (!IsLogEnabled(category)) return;
        if (context != null) Debug.Log(message, context); else Debug.Log(message);
    }

    public static void ConditionalLogWarning(DebugLogCategory category, string message, Object context = null)
    {
        if (!IsLogEnabled(category)) return;
        if (context != null) Debug.LogWarning(message, context); else Debug.LogWarning(message);
    }

    public static bool IsEnabledByName(string name)
    {
        if (Instance == null) return false;
        return name switch {
            "BaseEnemy" => Instance.enableBaseEnemyLogs,
            "BaseCrawlerEnemy" => Instance.enableBaseCrawlerEnemyLogs,
            "BoxerEnemy" => Instance.enableBoxerEnemyLogs,
            "DroneEnemy" => Instance.enableDroneEnemyLogs,
            "AlarmCarrierEnemy" => Instance.enableAlarmCarrierEnemyLogs,
            "BombCarrierEnemy" => Instance.enableBombCarrierEnemyLogs,
            "TestingEnemy" => Instance.enableTestingEnemyLogs,
            "AttackBehavior" => Instance.enableAttackBehaviorLogs,
            "DeathBehavior" => Instance.enableDeathBehaviorLogs,
            "IdleBehavior" => Instance.enableIdleBehaviorLogs,
            "FleeBehavior" => Instance.enableFleeBehaviorLogs,
            "DroneIdleBehavior" => Instance.enableDroneIdleBehaviorLogs,
            "DroneRelocateBehavior" => Instance.enableDroneRelocateBehaviorLogs,
            "SwarmManager" => Instance.enableSwarmManagerLogs,
            "DroneSwarmManager" => Instance.enableDroneSwarmManagerLogs,
            "EnemyAttackQueueManager" => Instance.enableEnemyAttackQueueManagerLogs,
            "EnemyHealthManager" => Instance.enableEnemyHealthManagerLogs,
            "CrowdController" => Instance.enableCrowdControllerLogs,
            "ScenePoolManager" => Instance.enableScenePoolManagerLogs,
            "BossRoombaController" => Instance.enableBossRoombaControllerLogs,
            "BossRoombaBrain" => Instance.enableBossRoombaBrainLogs,
            "BossArenaManager" => Instance.enableBossArenaManagerLogs,
            "BossHealth" => Instance.enableBossHealthLogs,
            "BossTopZone" => Instance.enableBossTopZoneLogs,
            "BossSidePanelCollider" => Instance.enableBossSidePanelColliderLogs,
            "BossPillarCollider" => Instance.enableBossPillarColliderLogs,
            "BossPlayerEjector" => Instance.enableBossPlayerEjectorLogs,
            "BossScenePlayerManager" => Instance.enableBossScenePlayerManagerLogs,
            "VacuumSuctionEffect" => Instance.enableVacuumSuctionEffectLogs,
            "ArenaWallCollider" => Instance.enableArenaWallColliderLogs,
            "BossAnimationEventMediator" => Instance.enableBossAnimationEventMediatorLogs,
            "BossAnimationEventRelay" => Instance.enableBossAnimationEventRelayLogs,
            "BossAnimatorDebugger" => Instance.enableBossAnimatorDebuggerLogs,
            "BossArmHitbox" => Instance.enableBossArmHitboxLogs,
            "BossAlarmDamageReceiver" => Instance.enableBossAlarmDamageReceiverLogs,
            "NavMeshAStarPlanner" => Instance.enableNavMeshAStarPlannerLogs,
            "PathRequestManager" => Instance.enablePathRequestManagerLogs,
            "AutomatedPathTestRunner" => Instance.enableAutomatedPathTestRunnerLogs,
            "EnemyProjectile" => Instance.enableEnemyProjectileLogs,
            "ExplosiveEnemyProjectile" => Instance.enableExplosiveEnemyProjectileLogs,
            "EnemyDeathSoundConfig" => Instance.enableEnemyDeathSoundConfigLogs,
            _ => false
        };
    }

    public static void LogByName(string name, string message, Object context = null)
    {
        if (!IsEnabledByName(name)) return;
        if (context != null) Debug.Log(message, context); else Debug.Log(message);
    }

    public static void LogWarningByName(string name, string message, Object context = null)
    {
        if (!IsEnabledByName(name)) return;
        if (context != null) Debug.LogWarning(message, context); else Debug.LogWarning(message);
    }
}

public enum DebugLogCategory {
    Singleton, SceneLoading, Input, Settings, AnimatorDebug, UI, Audio,
    BaseEnemy, BaseCrawlerEnemy, BoxerEnemy, DroneEnemy, AlarmCarrierEnemy, BombCarrierEnemy, TestingEnemy,
    AttackBehavior, DeathBehavior, IdleBehavior, FleeBehavior, DroneIdleBehavior, DroneRelocateBehavior,
    SwarmManager, DroneSwarmManager, EnemyAttackQueueManager, EnemyHealthManager, CrowdController, ScenePoolManager,
    BossRoombaController, BossRoombaBrain, BossArenaManager, BossHealth, BossTopZone, BossSidePanelCollider, BossPillarCollider,
    BossPlayerEjector, BossScenePlayerManager, VacuumSuctionEffect, ArenaWallCollider, BossAnimationEventMediator,
    BossAnimationEventRelay, BossAnimatorDebugger, BossArmHitbox, BossAlarmDamageReceiver,
    NavMeshAStarPlanner, PathRequestManager, AutomatedPathTestRunner,
    EnemyProjectile, ExplosiveEnemyProjectile, EnemyDeathSoundConfig, General
}
