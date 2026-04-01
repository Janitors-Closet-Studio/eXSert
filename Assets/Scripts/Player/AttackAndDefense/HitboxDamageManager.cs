/*
Written by Brandon Wahl.

This script is used to determine how much damage should be dealt when collided with using the attack interface. It also counts the weapon name for debugging purposes.


*/

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities.Combat.Attacks;

[RequireComponent(typeof(BoxCollider))]
public class HitboxDamageManager : MonoBehaviour, IAttackSystem
{
    public static event System.Action<AttackType, bool> AttackHitConfirmed;

    [SerializeField] private string weaponName = "";
    [SerializeField] private float damageAmount;
    [SerializeField, Tooltip("Maximum unique enemies this hitbox may damage per activation. 0 = unlimited.")]
    private int maxTargetsPerActivation;
    [SerializeField, Tooltip("Tag treated as a boss target for damage.")]
    private string bossTag = "Boss";

    [Header("Plunge Drone Effects")]
    [SerializeField, Tooltip("When enabled, heavy aerial plunge kills on drones can trigger shared splash damage and physics-collapse behavior.")]
    private bool enablePlungeDroneEffects;
    [SerializeField, Tooltip("When enabled, drones killed by plunge are switched to simple physics collapse (single rigidbody) with no animation.")]
    private bool enablePlungeDronePhysicsCollapse = true;
    [SerializeField, Range(0f, 10f), Tooltip("Additional shared radius around a killed drone to apply plunge damage to nearby drones.")]
    private float plungeDroneSharedSplashRadius = 2.5f;
    [SerializeField, Tooltip("Layer mask used when checking for nearby drones in shared plunge splash.")]
    private LayerMask plungeDroneSplashMask = ~0;
    [SerializeField, Range(0f, 60f), Tooltip("Downward velocity applied to drones when physics collapse starts.")]
    private float plungeDroneCollapseDownwardVelocity = 16f;
    [SerializeField, Range(0f, 30f), Tooltip("Horizontal push force away from plunge impact center applied on collapse.")]
    private float plungeDroneCollapseRadialForce = 6f;

    [Header("Aerial Drone Reposition Assist")]
    [SerializeField, Tooltip("When enabled, light aerial hits on alive drones nudge them away so follow-up aerial assists can reconnect more reliably.")]
    private bool enableLightAerialDroneReposition = true;
    [SerializeField, Range(0f, 8f), Tooltip("Horizontal displacement applied to drones hit by light aerial attacks.")]
    private float lightAerialDroneRepositionDistance = 1.8f;
    [SerializeField, Range(-1f, 3f), Tooltip("Vertical offset applied when nudging drones on light aerial hit.")]
    private float lightAerialDroneRepositionVerticalOffset = 0.35f;
    [SerializeField, Range(0f, 2f), Tooltip("Only apply light-aerial drone reposition when the player is at least this much below the drone.")]
    private float lightAerialDroneRepositionMinPlayerBelow = 0.1f;
    [SerializeField, Range(0f, 1f), Tooltip("Blend between away-from-player direction (0) and attacker-forward direction (1).")]
    private float lightAerialDroneRepositionForwardBias = 0.45f;

    [Header("Enemy Hit Stagger")]
    [SerializeField, Tooltip("Master toggle: when enabled, all player attacks apply enemy stagger on hit.")]
    private bool staggerEnemiesOnAllAttacks;
    [SerializeField, Tooltip("If master toggle is off, single-target attacks can still stagger enemies when this is enabled.")]
    private bool staggerOnSingleTargetAttacks = true;
    [SerializeField, Tooltip("If master toggle is off, AoE attacks can still stagger enemies when this is enabled.")]
    private bool staggerOnAoeAttacks;
    [SerializeField, Tooltip("If master toggle is off, aerial attacks can still stagger enemies when this is enabled.")]
    private bool staggerOnAerialAttacks = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX1")]
    private bool staggerOnSx1 = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX2")]
    private bool staggerOnSx2 = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX3")]
    private bool staggerOnSx3 = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX4")]
    private bool staggerOnSx4 = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX5")]
    private bool staggerOnSx5 = true;
    [SerializeField, Tooltip("Heavy combo part toggle: AY1")]
    private bool staggerOnAy1 = true;
    [SerializeField, Tooltip("Heavy combo part toggle: AY2")]
    private bool staggerOnAy2 = true;
    [SerializeField, Tooltip("Heavy combo part toggle: AY3")]
    private bool staggerOnAy3 = true;
    [SerializeField, Tooltip("Aerial combo part toggle: AC_X1 / ACX1")]
    private bool staggerOnAcX1 = true;
    [SerializeField, Tooltip("Aerial combo part toggle: AC_X2 / ACX2")]
    private bool staggerOnAcX2 = true;
    [SerializeField, Tooltip("Aerial combo part toggle: Plunge / Heavy Aerial finisher")]
    private bool staggerOnPlunge = true;
    [SerializeField, Range(0.05f, 2f), Tooltip("Duration of enemy stagger applied from player hits.")]
    private float enemyStaggerDuration = 0.35f;

    private BoxCollider boxCollider;
    private HashSet<int> hitThisActivation = new HashSet<int>(); // Track which enemies were hit during this activation
    private bool currentHitboxIsLightAerial;
    private Vector3 cachedAttackerPosition;
    private Vector3 cachedAttackerForward = Vector3.forward;
    private AttackType currentAttackType;
    private string currentAttackId;
    private bool hasAttackerContext;
    private int lockedSingleTargetId;
    float IAttackSystem.damageAmount => damageAmount;
    string IAttackSystem.weaponName => weaponName;
    public void Configure(string weapon, float damage, int maxTargets)
    {
        weaponName = weapon;
        damageAmount = damage;
        maxTargetsPerActivation = Mathf.Max(0, maxTargets);
    }

    public void ConfigurePlungeDroneEffects(
        bool enableEffects,
        bool enablePhysicsCollapse,
        float sharedSplashRadius,
        LayerMask splashMask,
        float collapseDownwardVelocity,
        float collapseRadialForce)
    {
        enablePlungeDroneEffects = enableEffects;
        enablePlungeDronePhysicsCollapse = enablePhysicsCollapse;
        plungeDroneSharedSplashRadius = Mathf.Max(0f, sharedSplashRadius);
        plungeDroneSplashMask = splashMask;
        plungeDroneCollapseDownwardVelocity = Mathf.Max(0f, collapseDownwardVelocity);
        plungeDroneCollapseRadialForce = Mathf.Max(0f, collapseRadialForce);
    }

    public void ConfigureAerialDroneReposition(
        bool isLightAerialHitbox,
        Vector3 attackerPosition,
        Vector3 attackerForward)
    {
        currentHitboxIsLightAerial = isLightAerialHitbox;
        cachedAttackerPosition = attackerPosition;
        cachedAttackerForward = attackerForward;
    }

    public void ConfigureAerialDroneRepositionSettings(
        bool enableReposition,
        float repositionDistance,
        float repositionVerticalOffset,
        float repositionMinPlayerBelow,
        float repositionForwardBias)
    {
        enableLightAerialDroneReposition = enableReposition;
        lightAerialDroneRepositionDistance = Mathf.Max(0f, repositionDistance);
        lightAerialDroneRepositionVerticalOffset = repositionVerticalOffset;
        lightAerialDroneRepositionMinPlayerBelow = Mathf.Max(0f, repositionMinPlayerBelow);
        lightAerialDroneRepositionForwardBias = Mathf.Clamp01(repositionForwardBias);
    }

    public void ConfigureAttackType(AttackType attackType)
    {
        currentAttackType = attackType;
    }

    public void ConfigureAttackId(string attackId)
    {
        currentAttackId = attackId;
    }

    public void ConfigureEnemyHitStaggerSettings(
        bool staggerAllAttacks,
        bool staggerSingleTargetAttacks,
        bool staggerAoeAttacks,
        bool staggerAerialAttacks,
        bool staggerSX1,
        bool staggerSX2,
        bool staggerSX3,
        bool staggerSX4,
        bool staggerSX5,
        bool staggerAY1,
        bool staggerAY2,
        bool staggerAY3,
        bool staggerACX1,
        bool staggerACX2,
        bool staggerPlunge,
        float staggerDuration)
    {
        staggerEnemiesOnAllAttacks = staggerAllAttacks;
        staggerOnSingleTargetAttacks = staggerSingleTargetAttacks;
        staggerOnAoeAttacks = staggerAoeAttacks;
        staggerOnAerialAttacks = staggerAerialAttacks;

        staggerOnSx1 = staggerSX1;
        staggerOnSx2 = staggerSX2;
        staggerOnSx3 = staggerSX3;
        staggerOnSx4 = staggerSX4;
        staggerOnSx5 = staggerSX5;

        staggerOnAy1 = staggerAY1;
        staggerOnAy2 = staggerAY2;
        staggerOnAy3 = staggerAY3;

        staggerOnAcX1 = staggerACX1;
        staggerOnAcX2 = staggerACX2;
        staggerOnPlunge = staggerPlunge;

        enemyStaggerDuration = Mathf.Max(0.05f, staggerDuration);
    }

    public void ConfigureAttackerContext(Vector3 attackerPosition)
    {
        cachedAttackerPosition = attackerPosition;
        hasAttackerContext = true;
    }


    void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;

        // Make sure we have a kinematic RB so trigger messages fire
        var rb = GetComponent<Rigidbody>();
        if (!rb)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }

    void OnEnable()  
    { 
        Debug.Log($"{weaponName} hitbox ENABLED - NEW ACTIVATION (HashSet had {hitThisActivation.Count} entries)");
        
        // Clear hit tracking for fresh attack activation
        hitThisActivation.Clear();
        lockedSingleTargetId = 0;
        Debug.Log($"{weaponName} HashSet cleared - now has {hitThisActivation.Count} entries");
        
        // Check for enemies already overlapping when hitbox activates
        // This handles cases where enemies are already in range when attack starts
        StartCoroutine(CheckInitialOverlaps());
    }
    
    // Public method to manually clear hit tracking (for debugging)
    public void ClearHitTracking()
    {
        Debug.Log($"{weaponName} MANUALLY clearing hit tracking (had {hitThisActivation.Count} entries)");
        hitThisActivation.Clear();
    }
    
    private System.Collections.IEnumerator CheckInitialOverlaps()
    {
        // Wait a frame to ensure the collider is properly enabled
        yield return null;
        
        if (!boxCollider.enabled) yield break;
        
        // Check what's currently overlapping this hitbox
        Collider[] overlapping = Physics.OverlapBox(
            boxCollider.bounds.center, 
            boxCollider.bounds.extents, 
            transform.rotation
        );
        
        // Debug.Log($"{weaponName} checking {overlapping.Length} overlapping colliders on activation");
        
        foreach (var collider in overlapping)
        {
            if (collider != boxCollider) // Don't hit ourselves
            {
                ProcessPotentialHit(collider);
            }
        }
    }
    
    void OnDisable() 
    { 
        // Debug.Log($"{weaponName} hitbox DISABLED - hit {hitThisActivation.Count} enemies during this activation");
        // Note: We keep hitThisActivation data until next OnEnable() clears it
    }


    private void ProcessPotentialHit(Collider other)
    {
        // Debug.Log($"{weaponName} processing potential hit on {other.gameObject.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
        // Debug.Log($"  - Root object: {other.transform.root.name} (Tag: {other.transform.root.tag})");
        // Debug.Log($"  - All components on this object: {string.Join(", ", other.GetComponents<Component>().Select(c => c.GetType().Name))}");
        
        // IMPORTANT: Only damage enemies/bosses, never the player or player's components
        if (!IsDamageableTag(other)) 
        {
            // Debug.Log($"{weaponName} hit non-enemy object: {other.gameObject.name} with tag '{other.tag}' - ignoring");
            return;
        }
        
        if (maxTargetsPerActivation > 0 && hitThisActivation.Count >= maxTargetsPerActivation)
        {
            return;
        }

        // Additional safety: Don't hit the player even if they somehow have "Enemy" tag
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
        {
            // Debug.LogWarning($"{weaponName} blocked attempt to damage player object: {other.gameObject.name}");
            return;
        }
        
        // Find IHealthSystem on the enemy (could be on root or any parent)
        var health = other.GetComponentInParent<IHealthSystem>();
        if (health == null) 
        {
            // Debug.LogWarning($"{weaponName} hit enemy {other.gameObject.name} but no IHealthSystem found!");
            return;
        }
        
        // Additional safety: Make sure this is actually an enemy component, not player
        var healthComp = health as Component;
        if (healthComp.CompareTag("Player"))
        {
            // Debug.LogWarning($"{weaponName} tried to damage player - blocked for safety");
            return;
        }
        
        // One hit per activation: Check if this enemy was already hit during current activation
        int enemyId = healthComp.GetInstanceID();
        // Debug.Log($"{weaponName} checking enemy ID {enemyId} ({healthComp.name}) - HashSet currently has {hitThisActivation.Count} entries");

        if (IsSingleTargetAttackType(currentAttackType))
        {
            if (lockedSingleTargetId == 0)
            {
                lockedSingleTargetId = ResolveClosestSingleTargetId();
                if (lockedSingleTargetId == 0)
                    lockedSingleTargetId = enemyId;
            }

            if (enemyId != lockedSingleTargetId)
                return;
        }
        
        if (hitThisActivation.Contains(enemyId))
        {
            // Debug.Log($"{weaponName} BLOCKED: already hit {healthComp.name} (ID: {enemyId}) during this activation");
            return;
        }
        
        // Mark this enemy as hit during this activation
        hitThisActivation.Add(enemyId);
        // Debug.Log($"{weaponName} ADDED enemy {healthComp.name} (ID: {enemyId}) to hit tracking - HashSet now has {hitThisActivation.Count} entries");
        
        // Apply damage via the interface
        float beforeHP = health.currentHP;
        health.LoseHP(damageAmount);
        float afterHP = health.currentHP;

        if (afterHP < beforeHP)
        {
            bool hitWasDrone = healthComp.GetComponentInParent<DroneEnemy>() != null;
            AttackHitConfirmed?.Invoke(currentAttackType, hitWasDrone);

            if (ShouldApplyEnemyStagger(currentAttackType) && afterHP > 0f)
            {
                BaseEnemyCore enemyCore = healthComp.GetComponentInParent<BaseEnemyCore>();
                enemyCore?.ApplyHitStagger(enemyStaggerDuration);
            }
        }

        if (currentHitboxIsLightAerial && afterHP > 0f)
        {
            DroneEnemy liveDrone = healthComp.GetComponentInParent<DroneEnemy>();
            if (liveDrone != null)
                TryApplyLightAerialDroneReposition(liveDrone);
        }

        if (enablePlungeDroneEffects && afterHP <= 0f)
        {
            var primaryDrone = healthComp.GetComponentInParent<DroneEnemy>();
            if (primaryDrone != null)
                ApplyPlungeDroneKillEffects(primaryDrone);
        }
        
        // Debug.Log($"SUCCESS: {weaponName} hit {healthComp.name} for {damageAmount} damage! Health: {beforeHP} -> {afterHP} (Max: {health.maxHP})");
        
        // Tell the enemy AI it was attacked (for state machine reactions)
        var enemy = other.GetComponentInParent<BaseEnemy<EnemyState, EnemyTrigger>>();
        if (enemy != null)
        {
            enemy.TryFireTriggerByName("Attacked");
            // Debug.Log($"{weaponName} fired 'Attacked' trigger on {enemy.name}");
        }
    }

    private void ApplyPlungeDroneKillEffects(DroneEnemy primaryDrone)
    {
        if (primaryDrone == null)
            return;

        Vector3 impactCenter = primaryDrone.transform.position;
        TryApplyDronePhysicsCollapse(primaryDrone, impactCenter);

        if (plungeDroneSharedSplashRadius <= 0f)
            return;

        Collider[] nearby = Physics.OverlapSphere(
            impactCenter,
            plungeDroneSharedSplashRadius,
            plungeDroneSplashMask,
            QueryTriggerInteraction.Ignore);

        foreach (Collider col in nearby)
        {
            if (col == null)
                continue;

            DroneEnemy nearbyDrone = col.GetComponentInParent<DroneEnemy>();
            if (nearbyDrone == null || ReferenceEquals(nearbyDrone, primaryDrone) || !nearbyDrone.isAlive)
                continue;

            int droneId = nearbyDrone.GetInstanceID();
            if (hitThisActivation.Contains(droneId))
                continue;

            hitThisActivation.Add(droneId);

            float hpBefore = nearbyDrone.currentHP;
            nearbyDrone.LoseHP(damageAmount);
            if (hpBefore > 0f && nearbyDrone.currentHP <= 0f)
                TryApplyDronePhysicsCollapse(nearbyDrone, impactCenter);
        }
    }

    private void TryApplyDronePhysicsCollapse(DroneEnemy drone, Vector3 impactCenter)
    {
        if (!enablePlungeDronePhysicsCollapse || drone == null)
            return;

        drone.ApplyPlungePhysicsCollapse(
            impactCenter,
            plungeDroneCollapseDownwardVelocity,
            plungeDroneCollapseRadialForce);
    }

    private void TryApplyLightAerialDroneReposition(DroneEnemy drone)
    {
        if (!enableLightAerialDroneReposition || drone == null || !drone.isAlive)
            return;

        float distance = Mathf.Max(0f, lightAerialDroneRepositionDistance);
        if (distance <= 0f)
            return;

        float yDelta = drone.transform.position.y - cachedAttackerPosition.y;
        if (yDelta < lightAerialDroneRepositionMinPlayerBelow)
            return;

        Vector3 awayFromPlayer = drone.transform.position - cachedAttackerPosition;
        awayFromPlayer.y = 0f;

        Vector3 forward = cachedAttackerForward;
        forward.y = 0f;

        if (awayFromPlayer.sqrMagnitude < 0.0001f)
            awayFromPlayer = forward.sqrMagnitude > 0.0001f ? forward : drone.transform.forward;

        if (forward.sqrMagnitude < 0.0001f)
            forward = awayFromPlayer;

        awayFromPlayer.Normalize();
        forward.Normalize();

        float bias = Mathf.Clamp01(lightAerialDroneRepositionForwardBias);
        Vector3 direction = Vector3.Slerp(awayFromPlayer, forward, bias).normalized;
        if (direction.sqrMagnitude < 0.0001f)
            direction = awayFromPlayer;

        Vector3 displacement = direction * distance;
        drone.ApplyAerialHitDisplacement(displacement, lightAerialDroneRepositionVerticalOffset);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Only process if hitbox is actually enabled
        if (!boxCollider.enabled) return;
        
        // Debug.Log($"{weaponName} OnTriggerEnter with {other.gameObject.name}");
        ProcessPotentialHit(other);
    }

    private bool IsDamageableTag(Component component)
    {
        if (component == null) return false;
        return component.CompareTag("Enemy") || (!string.IsNullOrWhiteSpace(bossTag) && component.CompareTag(bossTag));
    }

    private bool IsSingleTargetAttackType(AttackType attackType)
    {
        return attackType == AttackType.LightSingle || attackType == AttackType.HeavySingle;
    }

    private bool ShouldApplyEnemyStagger(AttackType attackType)
    {
        if (staggerEnemiesOnAllAttacks)
            return true;

        if (attackType == AttackType.LightSingle || attackType == AttackType.HeavySingle)
            return staggerOnSingleTargetAttacks && IsComboPartStaggerEnabled(currentAttackId);

        if (attackType == AttackType.LightAOE || attackType == AttackType.HeavyAOE)
            return staggerOnAoeAttacks;

        if (attackType == AttackType.LightAerial || attackType == AttackType.HeavyAerial)
            return staggerOnAerialAttacks && IsComboPartStaggerEnabled(currentAttackId);

        return false;
    }

    private bool IsComboPartStaggerEnabled(string attackId)
    {
        string key = NormalizeAttackId(attackId);
        if (string.IsNullOrEmpty(key))
            return true;

        return key switch
        {
            "SX1" => staggerOnSx1,
            "SX2" => staggerOnSx2,
            "SX3" => staggerOnSx3,
            "SX4" => staggerOnSx4,
            "SX5" => staggerOnSx5,
            "AY1" => staggerOnAy1,
            "AY2" => staggerOnAy2,
            "AY3" => staggerOnAy3,
            "ACX1" => staggerOnAcX1,
            "ACX2" => staggerOnAcX2,
            "PLUNGE" => staggerOnPlunge,
            "AERIALY1" => staggerOnPlunge,
            "HEAVYAERIAL" => staggerOnPlunge,
            _ => true,
        };
    }

    private static string NormalizeAttackId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
                buffer[count++] = char.ToUpperInvariant(c);
        }

        return count > 0 ? new string(buffer, 0, count) : string.Empty;
    }

    private int ResolveClosestSingleTargetId()
    {
        Vector3 origin = hasAttackerContext ? cachedAttackerPosition : transform.root.position;
        Collider[] overlapping = Physics.OverlapBox(
            boxCollider.bounds.center,
            boxCollider.bounds.extents,
            transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        int closestId = 0;
        float closestSqr = float.MaxValue;

        foreach (Collider candidate in overlapping)
        {
            if (candidate == null || candidate == boxCollider)
                continue;

            if (!IsDamageableTag(candidate))
                continue;

            if (candidate.CompareTag("Player") || candidate.transform.root.CompareTag("Player"))
                continue;

            IHealthSystem health = candidate.GetComponentInParent<IHealthSystem>();
            Component healthComp = health as Component;
            if (healthComp == null || healthComp.CompareTag("Player"))
                continue;

            BaseEnemyCore enemyCore = healthComp.GetComponentInParent<BaseEnemyCore>();
            if (enemyCore != null && !enemyCore.isAlive)
                continue;

            float sqr = (healthComp.transform.position - origin).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                closestId = healthComp.GetInstanceID();
            }
        }

        return closestId;
    }
    

    private void OnDrawGizmos()
    {
        if (!boxCollider) return;
        
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        
        // Set color based on whether hitbox is active
        if (boxCollider.enabled)
        {
            // Active hitbox - VERY BRIGHT RED with strong fill
            Gizmos.color = new Color(1f, 0f, 0f, 0.7f); // Bright red with 70% opacity
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            
            // Thick bright outline
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            
            // Draw a yellow outline to make it REALLY stand out
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size * 1.02f);
        }
        else
        {
            // Inactive hitbox - subtle gray
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
    }
}
