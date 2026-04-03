using UnityEngine;
using System.Collections;
using Behaviors;

public class BoxerEnemy : BaseEnemy<EnemyState, EnemyTrigger>
{
    private IEnemyStateBehavior<EnemyState, EnemyTrigger> idleBehavior, relocateBehavior, chaseBehavior, attackBehavior, recoverBehavior, deathBehavior;

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

#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name} Awake called");
#endif

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

    protected new virtual void Start()
    {
        InitializeStateMachine(EnemyState.Idle);
        ConfigureStateMachine();
#if UNITY_EDITOR
        EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name} State machine initialized");
#endif
        
        if (enemyAI.State.Equals(EnemyState.Idle))
        {
#if UNITY_EDITOR
            EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name} Manually calling OnEnterIdle for initial Idle state");
#endif
            idleBehavior.OnEnter(this);
        }

        // Initialize health system - ensure current health is set to max health
        currentHealth = maxHealth;
        
        EnsureHealthBarBinding();
    }

    protected override void ConfigureStateMachine()
    {
        base.ConfigureStateMachine();

        EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name} ConfigureStateMachine called");
        EnemyStateMachineConfig.ConfigureBasic(enemyAI);

        enemyAI.Configure(EnemyState.Idle)
            .OnEntry(() => {
                EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name} OnEntry lambda for Idle called");
                PlayIdleAnim();
                idleBehavior.OnEnter(this);
            })
            .OnExit(() => {
                idleBehavior.OnExit(this);
            });

        enemyAI.Configure(EnemyState.Relocate)
            .OnEntry(() => {
                PlayLocomotionAnim(agent != null ? agent.velocity.magnitude : 1f);
                relocateBehavior.OnEnter(this);
            })
            .OnExit(() => {
                PlayIdleAnim();
                relocateBehavior.OnExit(this);
            });

        enemyAI.Configure(EnemyState.Recover)
            .OnEntry(() => {
                PlayIdleAnim(); // or create a recover animation
                recoverBehavior.OnEnter(this);
            })
            .OnExit(() => {
                recoverBehavior.OnExit(this);
            });

        // --- CHASE STATE ---
        enemyAI.Configure(EnemyState.Chase)
            .OnEntry(() => {
                EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name}: ENTERING Chase state");
                // Force immediate transition to move animation (especially when coming from Attack state)
                PlayLocomotionAnim(agent != null ? agent.velocity.magnitude : 1f);
                chaseBehavior.OnEnter(this);
            })
            .OnExit(() => {
                EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name}: EXITING Chase state");
                PlayIdleAnim();
                chaseBehavior.OnExit(this);
            })
            .Ignore(EnemyTrigger.SeePlayer);

        // --- ATTACK STATE ---
        enemyAI.Configure(EnemyState.Attack)
            .OnEntry(() => {
                EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name}: ENTERING Attack state");
                // Play attack animation immediately when entering attack state
                PlayAttackAnim();
                attackBehavior.OnEnter(this);
            })
            .OnExit(() => {
                EnemyBehaviorDebugLogBools.Log(nameof(BoxerEnemy), $"{gameObject.name}: EXITING Attack state");
                PlayIdleAnim();
                attackBehavior.OnExit(this);
            })
            .Ignore(EnemyTrigger.SeePlayer);

        // --- DEATH STATE ---
        enemyAI.Configure(EnemyState.Death)
            .OnEntry(() => {
                PlayDieAnim();
                deathBehavior.OnEnter(this);
            })
            .Ignore(EnemyTrigger.SeePlayer)
            .Ignore(EnemyTrigger.LowHealth);
    }
}


