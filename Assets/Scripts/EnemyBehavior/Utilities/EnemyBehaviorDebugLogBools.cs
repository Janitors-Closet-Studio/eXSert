using UnityEngine;

/// <summary>
/// Centralized debug log control for all EnemyBehavior scripts.
/// Attach to a persistent GameObject or use as a singleton.
/// Toggle individual categories on/off to reduce console noise.
/// </summary>
public class EnemyBehaviorDebugLogBools : MonoBehaviour
{
    public static EnemyBehaviorDebugLogBools Instance { get; private set; }

    [Header("=== ENEMY TYPES ===")]
    [Tooltip("BaseEnemy: health, spawn, reset, state machine, animation events")]
    public bool BaseEnemy = true;
    
    [Tooltip("BaseCrawlerEnemy: crawler-specific behaviors")]
    public bool BaseCrawlerEnemy = true;
    
    [Tooltip("BoxerEnemy: state transitions and combat")]
    public bool BoxerEnemy = true;
    
    [Tooltip("DroneEnemy: drone movement and attacks")]
    public bool DroneEnemy = true;
    
    [Tooltip("AlarmCarrierEnemy: alarm system and carrier logic")]
    public bool AlarmCarrierEnemy = true;
    
    [Tooltip("BombCarrierEnemy: bomb carrier logic")]
    public bool BombCarrierEnemy = true;
    
    [Tooltip("TestingEnemy: test enemy logs")]
    public bool TestingEnemy = true;

    [Header("=== BEHAVIORS ===")]
    [Tooltip("AttackBehavior: attack execution and damage")]
    public bool AttackBehavior = true;
    
    [Tooltip("DeathBehavior: death handling")]
    public bool DeathBehavior = true;
    
    [Tooltip("IdleBehavior: idle state")]
    public bool IdleBehavior = true;
    
    [Tooltip("FleeBehavior: flee logic")]
    public bool FleeBehavior = true;
    
    [Tooltip("DroneIdleBehavior: drone idle")]
    public bool DroneIdleBehavior = true;
    
    [Tooltip("DroneRelocateBehavior: drone relocation")]
    public bool DroneRelocateBehavior = true;

    [Header("=== MANAGERS ===")]
    [Tooltip("SwarmManager: crawler swarm coordination")]
    public bool SwarmManager = true;
    
    [Tooltip("DroneSwarmManager: drone swarm coordination")]
    public bool DroneSwarmManager = true;
    
    [Tooltip("EnemyAttackQueueManager: attack queue system")]
    public bool EnemyAttackQueueManager = true;
    
    [Tooltip("EnemyHealthManager: health events and death relay")]
    public bool EnemyHealthManager = true;
    
    [Tooltip("CrowdController: crowd simulation")]
    public bool CrowdController = true;
    
    [Tooltip("ScenePoolManager: object pooling")]
    public bool ScenePoolManager = true;

    [Header("=== BOSS ===")]
    [Tooltip("BossRoombaController: boss main controller")]
    public bool BossRoombaController = true;
    
    [Tooltip("BossRoombaBrain: boss AI decisions")]
    public bool BossRoombaBrain = true;
    
    [Tooltip("BossArenaManager: arena mechanics")]
    public bool BossArenaManager = true;
    
    [Tooltip("BossHealth: boss health system")]
    public bool BossHealth = true;
    
    [Tooltip("BossTopZone: top zone mounting")]
    public bool BossTopZone = true;
    
    [Tooltip("BossSidePanelCollider: side panel damage")]
    public bool BossSidePanelCollider = true;
    
    [Tooltip("BossPillarCollider: pillar collisions")]
    public bool BossPillarCollider = true;
    
    [Tooltip("BossPlayerEjector: player ejection")]
    public bool BossPlayerEjector = true;
    
    [Tooltip("BossScenePlayerManager: player lifecycle in boss scene")]
    public bool BossScenePlayerManager = true;
    
    [Tooltip("VacuumSuctionEffect: vacuum attack")]
    public bool VacuumSuctionEffect = true;
    
    [Tooltip("ArenaWallCollider: arena wall collisions")]
    public bool ArenaWallCollider = true;
    
    [Tooltip("BossAnimationEventMediator: animation events")]
    public bool BossAnimationEventMediator = true;
    
    [Tooltip("BossAnimationEventRelay: animation event relay")]
    public bool BossAnimationEventRelay = true;
    
    [Tooltip("BossAnimatorDebugger: animator state debugging")]
    public bool BossAnimatorDebugger = true;
    
    [Tooltip("BossArmHitbox: arm hitbox damage")]
    public bool BossArmHitbox = true;
    
    [Tooltip("BossAlarmDamageReceiver: boss alarm damage")]
    public bool BossAlarmDamageReceiver = true;

    [Header("=== PATHFINDING ===")]
    [Tooltip("NavMeshAStarPlanner: A* pathfinding")]
    public bool NavMeshAStarPlanner = true;
    
    [Tooltip("PathRequestManager: path requests")]
    public bool PathRequestManager = true;
    
    [Tooltip("AutomatedPathTestRunner: pathfinding tests")]
    public bool AutomatedPathTestRunner = true;

    [Header("=== MISC ===")]
    [Tooltip("EnemyProjectile: projectile behavior")]
    public bool EnemyProjectile = true;
    
    [Tooltip("ExplosiveEnemyProjectile: explosive projectiles")]
    public bool ExplosiveEnemyProjectile = true;
    
    [Tooltip("EnemyDeathSoundConfig: death sounds")]
    public bool EnemyDeathSoundConfig = true;

    [Header("=== MASTER CONTROLS ===")]
    [Tooltip("Enable/disable ALL enemy behavior debug logs at once")]
    public bool EnableAllLogs = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Check if logging is enabled for a specific category.
    /// Returns false if Instance is null (logs disabled by default if no manager exists).
    /// </summary>
    public static bool IsEnabled(string category)
    {
        if (Instance == null) return true; // Default to enabled if no manager
        if (!Instance.EnableAllLogs) return false;

        return category switch
        {
            // Enemy Types
            nameof(BaseEnemy) => Instance.BaseEnemy,
            nameof(BaseCrawlerEnemy) => Instance.BaseCrawlerEnemy,
            nameof(BoxerEnemy) => Instance.BoxerEnemy,
            nameof(DroneEnemy) => Instance.DroneEnemy,
            nameof(AlarmCarrierEnemy) => Instance.AlarmCarrierEnemy,
            nameof(BombCarrierEnemy) => Instance.BombCarrierEnemy,
            nameof(TestingEnemy) => Instance.TestingEnemy,
            
            // Behaviors
            nameof(AttackBehavior) => Instance.AttackBehavior,
            nameof(DeathBehavior) => Instance.DeathBehavior,
            nameof(IdleBehavior) => Instance.IdleBehavior,
            nameof(FleeBehavior) => Instance.FleeBehavior,
            nameof(DroneIdleBehavior) => Instance.DroneIdleBehavior,
            nameof(DroneRelocateBehavior) => Instance.DroneRelocateBehavior,
            
            // Managers
            nameof(SwarmManager) => Instance.SwarmManager,
            nameof(DroneSwarmManager) => Instance.DroneSwarmManager,
            nameof(EnemyAttackQueueManager) => Instance.EnemyAttackQueueManager,
            nameof(EnemyHealthManager) => Instance.EnemyHealthManager,
            nameof(CrowdController) => Instance.CrowdController,
            nameof(ScenePoolManager) => Instance.ScenePoolManager,
            
            // Boss
            nameof(BossRoombaController) => Instance.BossRoombaController,
            nameof(BossRoombaBrain) => Instance.BossRoombaBrain,
            nameof(BossArenaManager) => Instance.BossArenaManager,
            nameof(BossHealth) => Instance.BossHealth,
            nameof(BossTopZone) => Instance.BossTopZone,
            nameof(BossSidePanelCollider) => Instance.BossSidePanelCollider,
            nameof(BossPillarCollider) => Instance.BossPillarCollider,
            nameof(BossPlayerEjector) => Instance.BossPlayerEjector,
            nameof(BossScenePlayerManager) => Instance.BossScenePlayerManager,
            nameof(VacuumSuctionEffect) => Instance.VacuumSuctionEffect,
            nameof(ArenaWallCollider) => Instance.ArenaWallCollider,
            nameof(BossAnimationEventMediator) => Instance.BossAnimationEventMediator,
            nameof(BossAnimationEventRelay) => Instance.BossAnimationEventRelay,
            nameof(BossAnimatorDebugger) => Instance.BossAnimatorDebugger,
            nameof(BossArmHitbox) => Instance.BossArmHitbox,
            nameof(BossAlarmDamageReceiver) => Instance.BossAlarmDamageReceiver,
            
            // Pathfinding
            nameof(NavMeshAStarPlanner) => Instance.NavMeshAStarPlanner,
            nameof(PathRequestManager) => Instance.PathRequestManager,
            nameof(AutomatedPathTestRunner) => Instance.AutomatedPathTestRunner,
            
            // Misc
            nameof(EnemyProjectile) => Instance.EnemyProjectile,
            nameof(ExplosiveEnemyProjectile) => Instance.ExplosiveEnemyProjectile,
            nameof(EnemyDeathSoundConfig) => Instance.EnemyDeathSoundConfig,
            
            _ => true // Unknown category defaults to enabled
        };
    }

    /// <summary>
    /// Logs a message if the category is enabled.
    /// </summary>
    public static void Log(string category, string message, Object context = null)
    {
        if (!IsEnabled(category)) return;
        
        if (context != null)
            Debug.Log(message, context);
        else
            Debug.Log(message);
    }

    /// <summary>
    /// Logs a warning if the category is enabled.
    /// </summary>
    public static void LogWarning(string category, string message, Object context = null)
    {
        if (!IsEnabled(category)) return;
        
        if (context != null)
            Debug.LogWarning(message, context);
        else
            Debug.LogWarning(message);
    }

    /// <summary>
    /// Logs an error (always logs regardless of category setting - errors should not be suppressed).
    /// </summary>
    public static void LogError(string message, Object context = null)
    {
        if (context != null)
            Debug.LogError(message, context);
        else
            Debug.LogError(message);
    }

    [ContextMenu("Enable All Categories")]
    private void EnableAllCategories()
    {
        EnableAllLogs = true;
        BaseEnemy = true;
        BaseCrawlerEnemy = true;
        BoxerEnemy = true;
        DroneEnemy = true;
        AlarmCarrierEnemy = true;
        BombCarrierEnemy = true;
        TestingEnemy = true;
        AttackBehavior = true;
        DeathBehavior = true;
        IdleBehavior = true;
        FleeBehavior = true;
        DroneIdleBehavior = true;
        DroneRelocateBehavior = true;
        SwarmManager = true;
        DroneSwarmManager = true;
        EnemyAttackQueueManager = true;
        EnemyHealthManager = true;
        CrowdController = true;
        ScenePoolManager = true;
        BossRoombaController = true;
        BossRoombaBrain = true;
        BossArenaManager = true;
        BossHealth = true;
        BossTopZone = true;
        BossSidePanelCollider = true;
        BossPillarCollider = true;
        BossPlayerEjector = true;
        BossScenePlayerManager = true;
        VacuumSuctionEffect = true;
        ArenaWallCollider = true;
        BossAnimationEventMediator = true;
        BossAnimationEventRelay = true;
        BossAnimatorDebugger = true;
        BossArmHitbox = true;
        BossAlarmDamageReceiver = true;
        NavMeshAStarPlanner = true;
        PathRequestManager = true;
        AutomatedPathTestRunner = true;
        EnemyProjectile = true;
        ExplosiveEnemyProjectile = true;
        EnemyDeathSoundConfig = true;
    }

    [ContextMenu("Disable All Categories")]
    private void DisableAllCategories()
    {
        EnableAllLogs = false;
    }

    [ContextMenu("Disable Boss Logs Only")]
    private void DisableBossLogsOnly()
    {
        BossRoombaController = false;
        BossRoombaBrain = false;
        BossArenaManager = false;
        BossHealth = false;
        BossTopZone = false;
        BossSidePanelCollider = false;
        BossPillarCollider = false;
        BossPlayerEjector = false;
        BossScenePlayerManager = false;
        VacuumSuctionEffect = false;
        ArenaWallCollider = false;
        BossAnimationEventMediator = false;
        BossAnimationEventRelay = false;
        BossAnimatorDebugger = false;
        BossArmHitbox = false;
        BossAlarmDamageReceiver = false;
    }

    [ContextMenu("Disable Swarm Logs Only")]
    private void DisableSwarmLogsOnly()
    {
        SwarmManager = false;
        DroneSwarmManager = false;
        BaseCrawlerEnemy = false;
    }
}
