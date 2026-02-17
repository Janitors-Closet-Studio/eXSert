using UnityEngine;
using System.Collections;
using Behaviors;
#region Tutorial Note
// PLEASE DO NOT DELETE THIS SCRIPT
// USE THIS AS A TEMPLATE/REFERENCE FOR CREATING NEW ENEMY TYPES
// AND/OR TO TEST OUT NEW BEHAVIORAL FEATURES
// IF AN ENEMY TYPE WILL BE USING DIFFERENT OR MORE/LESS STATES/TRIGGERS THAN THE BASE ENEMY CLASS
// THEN YOU WILL NEED TO CREATE NEW STATE AND TRIGGER ENUMS IN THAT ENEMY SCRIPT

// This is an example of how to create custom states and triggers for a specific enemy type
// You would still need to re-implement all the reused states/triggers in the new enums
/*
public enum TestingEnemyState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    SpecialMove
}

public enum TestingEnemyTrigger
{
    SeePlayer,
    LosePlayer,
    LowHealth,
    RecoveredHealth,
    PlayerInRange,
    PlayerOutOfRange,
    PlayerLowHealth
}
*/

// Example derived enemy class with custom states and triggers
// You would replace EnemyState and/or EnemyTrigger with your custom enums if needed
// This also means that you would need to implement all of the state machine configurations in this class
#endregion
public class TestingEnemy : BaseEnemy<EnemyState, EnemyTrigger>
{
    private IEnemyStateBehavior<EnemyState, EnemyTrigger> idleBehavior, relocateBehavior, chaseBehavior, attackBehavior, recoverBehavior, deathBehavior;

    // Reference to the player (set this appropriately in your game)

    protected Coroutine lookAtPlayerCoroutine;
    protected Coroutine chaseCoroutine;
    protected Coroutine attackRangeMonitorCoroutine;

    protected override void Awake()
    {
        base.Awake();
        idleBehavior = new IdleBehavior<EnemyState, EnemyTrigger>();
        relocateBehavior = new RelocateBehavior<EnemyState, EnemyTrigger>();
        recoverBehavior = new RecoverBehavior<EnemyState, EnemyTrigger>();
        chaseBehavior = new ChaseBehavior<EnemyState, EnemyTrigger>();
        attackBehavior = new AttackBehavior<EnemyState, EnemyTrigger>();
        deathBehavior = new DeathBehavior<EnemyState, EnemyTrigger>();

        //EnemyBehaviorDebugLogBools.Log(nameof(TestingEnemy), $"{gameObject.name} Awake called");

        // Find the player - use PlayerPresenceManager if available
        if (PlayerPresenceManager.IsPlayerPresent)
            PlayerTarget = PlayerPresenceManager.PlayerTransform;
        else
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                PlayerTarget = playerObj.transform;
        }
    }

    protected virtual void Start()
    {
        InitializeStateMachine(EnemyState.Idle);
        ConfigureStateMachine();
        //EnemyBehaviorDebugLogBools.Log(nameof(TestingEnemy), $"{gameObject.name} State machine initialized");
        if (enemyAI.State.Equals(EnemyState.Idle))
        {
            //EnemyBehaviorDebugLogBools.Log(nameof(TestingEnemy), $"{gameObject.name} Manually calling OnEnterIdle for initial Idle state");
            idleBehavior.OnEnter(this);
        }

        EnsureHealthBarBinding();
    }

    protected override void ConfigureStateMachine()
    {
        base.ConfigureStateMachine();

        //EnemyBehaviorDebugLogBools.Log(nameof(TestingEnemy), $"{gameObject.name} ConfigureStateMachine called");
        EnemyStateMachineConfig.ConfigureBasic(enemyAI); // ConfigureBasic is a static helper method to set up the default states and triggers
                                                         // It would not be used again in this derived class if you had custom states/triggers
                                                         // Add more transitions specific to this enemy if needed

        // --- IDLE STATE ---
        enemyAI.Configure(EnemyState.Idle)
            .OnEntry(() => {
                //EnemyBehaviorDebugLogBools.Log(nameof(TestingEnemy), $"{gameObject.name} OnEntry lambda for Idle called");
                idleBehavior.OnEnter(this);
            })
            .OnExit(() => idleBehavior.OnExit(this));

        // --- RELOCATE STATE ---
        enemyAI.Configure(EnemyState.Relocate)
            .OnEntry(() => relocateBehavior.OnEnter(this))
            .OnExit(() => relocateBehavior.OnExit(this));

        // --- RECOVER STATE ---
        enemyAI.Configure(EnemyState.Recover)
            .OnEntry(() => recoverBehavior.OnEnter(this))
            .OnExit(() => recoverBehavior.OnExit(this));

        // --- CHASE STATE ---
        enemyAI.Configure(EnemyState.Chase)
            .OnEntry(() => chaseBehavior.OnEnter(this))
            .OnExit(() => chaseBehavior.OnExit(this))
            .Ignore(EnemyTrigger.SeePlayer);

        // --- ATTACK STATE ---
        enemyAI.Configure(EnemyState.Attack)
            .OnEntry(() => attackBehavior.OnEnter(this))
            .OnExit(() => attackBehavior.OnExit(this))
            .Ignore(EnemyTrigger.SeePlayer); // Ignore SeePlayer trigger in Attack state

        // --- DEATH STATE ---
        enemyAI.Configure(EnemyState.Death)
            .OnEntry(() => deathBehavior.OnEnter(this))
            .Ignore(EnemyTrigger.SeePlayer)
            .Ignore(EnemyTrigger.LowHealth);
    }

    protected override void Update()
    {
        base.Update();
    }
}

