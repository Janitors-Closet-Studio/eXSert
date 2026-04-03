// CleanserAttackType.cs
// Purpose: Defines all attack types for the Cleanser boss and their configurations.
// Works with: CleanserBrain, CleanserComboSystem

using UnityEngine;
using UnityEngine.Serialization;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Categories of attacks for damage mitigation rules.
    /// Wing attacks can be partially guarded but not parried.
    /// Halberd attacks can be parried but guarding doesn't mitigate damage.
    /// </summary>
    public enum AttackCategory
    {
        Halberd,    // Can be parried, guard doesn't help
        Wing,       // Can be partially guarded, cannot be parried
        Mixed,      // Attack has multiple hit types (e.g., Slash into Slap)
        Special     // Ultimate attacks with unique rules
    }

    /// <summary>
    /// Possible path types for the Spare Toss projectile.
    /// Used as flags to allow multiple path types to be selected.
    /// </summary>
    [System.Flags]
    public enum SpareTossPathType
    {
        None = 0,
        Straight = 1 << 0,              // Direct path to target, no return
        StraightReturn = 1 << 1,        // Direct path, returns like boomerang
        CurvedBoomerang = 1 << 2        // Curved path, always returns
    }

    /// <summary>
    /// All basic attack types for the Cleanser boss.
    /// </summary>
    public enum CleanserBasicAttack
    {
        None = 0,
        Lunge = 1,                  // Stabbing lunge with wing block during windup
        OverheadCleave = 2,         // Overhead strike downward
        SlashIntoSlap = 3,          // Two-part: halberd slash then wing backhand
        RakeIntoSpinSlash = 4,      // Wing rake followed by 360 spin slash (or just spin slash)
        SpareToss = 5,              // Projectile throw
        // Value 6 is intentionally unused (legacy slot).
        LegSweep = 7,               // Low sweep requiring jump to avoid
        // Value 8 was Knockback and is intentionally excluded from combo-authoring list.
        // Value 9 was legacy MiniCrescentWave and is intentionally unused.

        // New/renamed moves appended to preserve existing serialized enum values.
        Cleave = 10,
        CleaveAdvance = 11,
        PommelStrike = 12,
        DiagUpwardSlash = 13,
        LungeBlock = 14,
        WingBash = 15
        // Value 16 was legacy Basic SpinDash and is intentionally unused.
    }

    /// <summary>
    /// Strong (combo finisher) attack types for the Cleanser boss.
    /// </summary>
    public enum CleanserStrongAttack
    {
        None,
        HighDive,               // Leap high then slam down
        AnimeDashSlash,         // Pentagram-style dash attacks around player
        Whirlwind,              // Spinning suction + leap slam
        SpinDash                // Finisher: dashes through lodged spare-weapon points, then player
    }

    /// <summary>
    /// Configuration for the Spin Dash attack (uses JumpSpinAttack animation clips/states).
    /// </summary>
    [System.Serializable]
    public class JumpSpinAttackConfig
    {
        [Header("Animation States")]
        [Tooltip("Wind-up animation played before movement starts.")]
        public string WindupTrigger = "JumpSpinAttackWindup";

        [Tooltip("Optional additional high-power wind-up animation.")]
        public string HPWindupTrigger = "JumpSpinAttackHPWindup";

        [Tooltip("Looped spin pose while moving toward destination.")]
        public string HoldPoseTrigger = "JumpSpinAttackHPHoldPose";

        [Tooltip("Wind-down animation played when movement phase ends.")]
        public string WindDownTrigger = "JumpSpinAttackWindDown";

        [Header("Animation Speed Multipliers")]
        [Tooltip("Playback speed multiplier for the wind-up animation clip.")]
        public float WindupAnimSpeedMultiplier = 1f;

        [Tooltip("Playback speed multiplier for the optional HP wind-up animation clip.")]
        public float HPWindupAnimSpeedMultiplier = 1f;

        [Tooltip("Playback speed multiplier while HoldPose is active.")]
        public float HoldPoseAnimSpeedMultiplier = 1f;

        [Tooltip("Playback speed multiplier for the wind-down animation clip.")]
        public float WindDownAnimSpeedMultiplier = 1f;

        [Header("Movement")]
        [Tooltip("Ground movement speed during the hold-pose loop.")]
        public float MoveSpeed = 12f;

        [Header("Range")]
        [Tooltip("Minimum distance to player for SpinDash to be eligible.")]
        public float RangeMin = 2f;

        [Tooltip("Maximum distance to player for SpinDash to be eligible.")]
        public float RangeMax = 14f;

        [Tooltip("Distance threshold: above this, dash segment travel time uses LongDistanceTravelDuration instead of MoveSpeed.")]
        public float MaxTravelDistance = 10f;

        [Tooltip("Travel time used for long dash segments (distance above MaxTravelDistance threshold).")]
        public float LongDistanceTravelDuration = 0.2f;

        [Tooltip("If true, continuously updates the final dash target toward the player's current position.")]
        public bool TrackPlayerDuringHold = false;

        [Tooltip("How far beyond the player's position the final spin dash target extends.")]
        public float FinalPlayerOvershootDistance = 1.5f;

        [Header("Damage")]
        [Tooltip("Damage per hit tick while spinning.")]
        public float DamagePerHit = 8f;

        [Tooltip("If true, SpinDash hits force-stagger the player.")]
        public bool StaggerPlayerOnHit = true;

        [Tooltip("Hitbox range while spinning.")]
        public float HitRange = 2.5f;

        [Tooltip("Time between spin hit ticks.")]
        public float HitInterval = 0.2f;

        [Tooltip("Maximum number of hit ticks that can occur in one use.")]
        public int MaxHitCount = 6;

        [Tooltip("Brief pause duration applied when SpinDash successfully hits the player.")]
        [Min(0f)] public float HitStopDurationOnPlayerHit = 0.1f;

        [Tooltip("Temporary movement speed multiplier applied after a SpinDash hit to create impact slowdown.")]
        [Range(0.1f, 1f)] public float MoveSpeedMultiplierOnPlayerHit = 0.5f;

        [Tooltip("How long the temporary SpinDash movement slowdown lasts after each successful hit.")]
        [Min(0f)] public float MoveSpeedSlowDurationOnPlayerHit = 0.2f;

        [Header("SFX/VFX")]
        [Tooltip("SFX played at attack start.")]
        public AudioClip WindupSFX;

        [Tooltip("SFX played when entering hold phase.")]
        public AudioClip HoldSFX;

        [Tooltip("SFX played at wind-down.")]
        public AudioClip WindDownSFX;

        [Tooltip("Optional VFX prefab spawned for the spinning phase.")]
        public GameObject SpinVFX;
    }

    /// <summary>
    /// Detailed configuration for the Anime Dash Slash strong finisher.
    /// </summary>
    [System.Serializable]
    public class AnimeDashSlashConfig
    {
        [Header("Animation")]
        [Tooltip("Animation trigger for Anime Dash Slash.")]
        public string AnimationTrigger = "Attack_AnimeDash";

        [Tooltip("Playback speed multiplier for Anime Dash Slash animation clip.")]
        public float AnimationSpeedMultiplier = 1f;

        [Tooltip("Delay after triggering animation before dash movement starts.")]
        public float PreDashDelay = 0.3f;

        [Tooltip("Delay after all dashes complete before returning control.")]
        public float PostDashDelay = 0f;

        [Header("Range")]
        [Tooltip("Minimum distance to player for Anime Dash Slash to be eligible.")]
        public float RangeMin = 4f;

        [Tooltip("Maximum distance to player for Anime Dash Slash to be eligible.")]
        public float RangeMax = 20f;

        [Header("Pattern Selection")]
        [Tooltip("If true, uses the rapid circling + random dash-through pattern instead of the legacy star/pentagram path.")]
        public bool UseCircularDashPattern = true;

        [Header("Circular Pattern")]
        [Tooltip("Circle radius around center while performing the new circular Anime Dash pattern.")]
        public float DashTargetRadius = 6f;

        [Tooltip("If true, captures player position once at dash start as the center point.")]
        public bool UsePlayerPositionAsCenterAtStart = true;

        [Tooltip("If true, keeps the player as a continuously updating center while circling.")]
        public bool FollowPlayerAsCenterContinuously = true;

        [Tooltip("If true (and not continuously following), re-centers on player's current position before each dash-through.")]
        public bool RecenterOnEachDashThrough = true;

        [Tooltip("Minimum number of dash-throughs during circular Anime Dash.")]
        [Range(1, 12)] public int DashThroughCountMin = 4;

        [Tooltip("Maximum number of dash-throughs during circular Anime Dash.")]
        [Range(1, 12)] public int DashThroughCountMax = 6;

        [Header("Legacy Pentagram Pattern (UseCircularDashPattern = false)")]
        [Tooltip("Number of dash target positions used by the legacy pentagram/star path.")]
        public int DashTargetCount = 5;

        [Tooltip("Angle step between legacy dash target points in degrees (144 creates star-like pattern).")]
        public float DashAngleStepDegrees = 144f;

        [Header("Dash Timing")]
        [Tooltip("Duration to move from one dash target to the next (legacy pattern).")]
        public float DashTravelDuration = 0.15f;

        [Tooltip("Minimum circle time before each abrupt dash-through.")]
        [Min(0.01f)] public float CircleDurationMin = 0.2f;

        [Tooltip("Maximum circle time before each abrupt dash-through.")]
        [Min(0.01f)] public float CircleDurationMax = 0.45f;

        [Tooltip("Minimum random time to keep circling before triggering the next dash-through.")]
        [Min(0.01f)] public float TimeBetweenDashThroughMin = 0.35f;

        [Tooltip("Maximum random time to keep circling before triggering the next dash-through.")]
        [Min(0.01f)] public float TimeBetweenDashThroughMax = 0.8f;

        [Tooltip("Angular speed used while rapidly circling around the center.")]
        public float CircleAngularSpeedDegPerSec = 720f;

        [Tooltip("Minimum circling speed as a fraction of CircleAngularSpeedDegPerSec/TurnSpeed during deceleration half (0-1).")]
        [Range(0f, 1f)] public float CircleDecelMinSpeedPercent = 0.125f;

        [Tooltip("Duration of each abrupt dash-through across the center.")]
        [Min(0.01f)] public float DashThroughDuration = 0.16f;

        [Tooltip("Short delay inserted after circling and before each dash-through begins (circular pattern only).")]
        [Min(0f)] public float PreDashThroughDelay = 0.05f;

        [Tooltip("How long to remain at each target before moving to the next (legacy pattern).")]
        public float PauseAtTargetDuration = 0.1f;

        [Tooltip("How quickly the boss rotates to face the dash direction.")]
        public float TurnSpeed = 14f;

        [Header("Damage")]
        [Tooltip("Damage applied when the dash hit window connects.")]
        public float DamagePerHit = 12f;

        [Tooltip("If true, Anime Dash keeps the hitbox active through both circling and dash-through movement (SpinDash-style multi-hit behavior).")]
        public bool UseContinuousHitboxDuringCircle = true;

        [Tooltip("Maximum number of hit ticks during one Anime Dash use when continuous hitbox is enabled.")]
        [Min(1)] public int MaxHitCount = 8;

        [Tooltip("If true, Anime Dash hits force-stagger the player.")]
        public bool StaggerPlayerOnHit = true;

        [Tooltip("Brief pause duration applied when Anime Dash successfully hits the player (continuous mode).")]
        [Min(0f)] public float HitStopDurationOnPlayerHit = 0.08f;

        [Tooltip("Temporary movement speed multiplier applied after Anime Dash hit in continuous mode.")]
        [Range(0.1f, 1f)] public float MoveSpeedMultiplierOnPlayerHit = 0.6f;

        [Tooltip("How long Anime Dash movement slowdown lasts after each successful hit in continuous mode.")]
        [Min(0f)] public float MoveSpeedSlowDurationOnPlayerHit = 0.15f;

        [Tooltip("If true, AnimeDash uses the SpinDash collider bounds for hit range. If false, uses FallbackHitRange.")]
        public bool UseSpinDashColliderForHitRange = true;

        [Tooltip("Fallback hit range used when UseSpinDashColliderForHitRange is disabled or collider is unavailable.")]
        public float HitRange = 3f;

        [Tooltip("Normalized dash progress when hit window starts (0-1).")]
        [Range(0f, 1f)] public float HitWindowStart = 0.4f;

        [Tooltip("Normalized dash progress when hit window ends (0-1).")]
        [Range(0f, 1f)] public float HitWindowEnd = 0.6f;

        [Header("SFX/VFX")]
        [Tooltip("Sound effect played when Anime Dash Slash starts.")]
        public AudioClip AttackSFX;
    }

    /// <summary>
    /// Movement-only actions for the Cleanser (no hitbox).
    /// </summary>
    public enum CleanserMovementAction
    {
        None,
        GapClosingDash          // Pure movement dash to close distance (no attack)
    }

    /// <summary>
    /// Ultimate attack for the Cleanser boss.
    /// </summary>
    public enum CleanserUltimateAttack
    {
        None,
        DoubleMaximumSweep      // Wall jump -> two sweeps -> floating platforms -> massive strike
    }

    /// <summary>
    /// Configuration for the Spare Toss projectile behavior.
    /// </summary>
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.b9ym9wldnvfx")]
    [System.Serializable]
    public class SpareTossConfig
    {
        [Header("Animation")]
        [Tooltip("Animation trigger for the spare toss attack.")]
        public string AnimationTrigger = "Attack_SpareToss";

        [Tooltip("Playback speed multiplier for the spare toss animation clip.")]
        public float AnimationSpeedMultiplier = 1f;

        [Header("Legacy Projectile Path Settings (Unused by current Spare Toss volley)")]
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public bool ThrowSequentially = true;
        
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        [Range(1, 2)] public int ProjectileCount = 1;

        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public SpareTossPathType AllowedPathTypes = SpareTossPathType.Straight | SpareTossPathType.StraightReturn | SpareTossPathType.CurvedBoomerang;

        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public bool UseStraightPath = true;
        
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public bool ReturnsOnStraightPath = false;
        
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public bool UseCurvedBoomerang = false;

        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public Vector2 CurveWidthRange = new Vector2(5f, 10f);
        
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public Vector2 OvershootDistanceRange = new Vector2(3f, 8f);
        
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public Vector2 ProjectileSpeedRange = new Vector2(15f, 25f);

        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public bool UseHoming = true;
        
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public float HomingUpdateInterval = 0.3f;
        
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        [Range(0f, 1f)] public float HomingStrength = 0.5f;

        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public LayerMask WallLayerMask;
        
        [HideInInspector]
        [Tooltip("Legacy: no longer used by current volley-based spare toss.")]
        public bool DestroyOnWallHit = true;

        [Header("SFX")]
        [Tooltip("Sound effect when throwing the weapon.")]
        public AudioClip ThrowSFX;
        
        [HideInInspector]
        [Tooltip("Legacy projectile hit SFX (unused by current volley-based spare toss).")]
        public AudioClip HitSFX;

        /// <summary>
        /// Randomly selects a path type from the allowed types and sets the corresponding bools.
        /// Call this before each throw to randomize the behavior.
        /// </summary>
        public void RandomizePathType()
        {
            // Build list of allowed path types
            var allowedTypes = new System.Collections.Generic.List<SpareTossPathType>();
            
            if ((AllowedPathTypes & SpareTossPathType.Straight) != 0)
                allowedTypes.Add(SpareTossPathType.Straight);
            if ((AllowedPathTypes & SpareTossPathType.StraightReturn) != 0)
                allowedTypes.Add(SpareTossPathType.StraightReturn);
            if ((AllowedPathTypes & SpareTossPathType.CurvedBoomerang) != 0)
                allowedTypes.Add(SpareTossPathType.CurvedBoomerang);

            // Default to straight if nothing selected
            if (allowedTypes.Count == 0)
                allowedTypes.Add(SpareTossPathType.Straight);

            // Randomly select one
            var selected = allowedTypes[Random.Range(0, allowedTypes.Count)];

            // Reset all
            UseStraightPath = false;
            ReturnsOnStraightPath = false;
            UseCurvedBoomerang = false;

            // Set based on selection
            switch (selected)
            {
                case SpareTossPathType.Straight:
                    UseStraightPath = true;
                    ReturnsOnStraightPath = false;
                    break;
                case SpareTossPathType.StraightReturn:
                    UseStraightPath = true;
                    ReturnsOnStraightPath = true;
                    break;
                case SpareTossPathType.CurvedBoomerang:
                    UseStraightPath = false;
                    UseCurvedBoomerang = true;
                    break;
            }
        }
    }

    /// <summary>
    /// Configuration for crescent arc projectile-style attacks.
    /// Used by DiagUpwardSlash and Ultimate sweep variants.
    /// </summary>
    [System.Serializable]
    public class CrescentArcProjectileConfig
    {
        [Header("Projectile")]
        [Tooltip("Projectile prefab to spawn for this attack.")]
        public GameObject ProjectilePrefab;

        [Tooltip("How many projectiles are spawned for one cast.")]
        [Range(1, 5)] public int ProjectileCount = 1;

        [Tooltip("Damage per projectile hit.")]
        public float Damage = 20f;

        [Tooltip("Projectile travel speed.")]
        public float Speed = 25f;

        [Tooltip("Maximum travel distance before despawn.")]
        public float MaxDistance = 25f;

        [Header("Spawn")]
        [Tooltip("Height offset from the caster when spawning.")]
        public float SpawnHeight = 1.2f;

        [Tooltip("Forward offset from the caster when spawning.")]
        public float SpawnForwardOffset = 1.2f;

        [Tooltip("Scale range applied per projectile. Use same min/max for fixed scale.")]
        public Vector2 ScaleRange = new Vector2(1f, 1f);

        [Tooltip("Visual tilt angle range in degrees (up/down roll style). Use same min/max for fixed tilt.")]
        public Vector2 TiltAngleRange = new Vector2(0f, 0f);

        [Tooltip("Additional spread angle between projectiles when ProjectileCount > 1.")]
        public float SpreadStep = 6f;

        [Header("Damage Rules")]
        [Tooltip("Attack category to use for guard/parry behavior on hit.")]
        public AttackCategory DamageCategory = AttackCategory.Halberd;

        [Tooltip("If true, projectile can be parried when category/rules allow it.")]
        public bool CanBeParried = true;

        [Tooltip("If true, projectile can be guarded and damage is reduced using GuardDamageMultiplier.")]
        public bool CanBeGuarded = true;

        [Tooltip("Damage multiplier applied when guarding (0.3 = 70% reduction).")]
        [Range(0f, 1f)] public float GuardDamageMultiplier = 0.35f;
    }

    /// <summary>
    /// Shared settings for leap/slam impact damage checks.
    /// </summary>
    [System.Serializable]
    public class LeapSlamDamageConfig
    {
        [Tooltip("If true, uses the Cleanser aggression collider bounds as the damage area for slam impact.")]
        public bool UseAggroCollider = false;

        [Tooltip("Range used when UseAggroCollider is false, or when the aggro collider is unavailable.")]
        [FormerlySerializedAs("FallbackRange")]
        public float Range = 4f;

        [Tooltip("Inner percentage of the effective range that deals full damage.")]
        [Range(0f, 1f)] public float FullDamageRadiusPercent = 1f;

        [Tooltip("Damage percentage dealt at the outer edge of the effective range.")]
        [Range(0f, 1f)] public float EdgeDamagePercent = 1f;
    }

    /// <summary>
    /// Configuration for the High Dive strong attack.
    /// </summary>
    [System.Serializable]
    public class HighDiveConfig
    {
        [Header("Animation")]
        [Tooltip("Playback speed multiplier for the high dive sequence.")]
        public float AnimationSpeedMultiplier = 1f;

        [Tooltip("How many seconds before upward phase ends to trigger JumpArcResolution. Use this to align slam-down animation with landing.")]
        [Min(0f)] public float JumpArcResolutionLeadTime = 0f;

        [Tooltip("Playback speed multiplier specifically for JumpArcResolution during High Dive.")]
        [Min(0.1f)] public float JumpArcResolutionAnimSpeedMultiplier = 1f;

        [Header("Range")]
        [Tooltip("Minimum distance to player for High Dive to be eligible.")]
        public float RangeMin = 4f;

        [Tooltip("Maximum distance to player for High Dive to be eligible.")]
        public float RangeMax = 18f;

        [Tooltip("Duration of the upward leap phase.")]
        public float LeapUpDuration = 0.6f;

        [Tooltip("Duration of the downward slam phase.")]
        public float SlamDownDuration = 0.4f;

        [Tooltip("Peak height reached during the leap.")]
        public float LeapHeight = 8f;

        [Tooltip("Fixed horizontal distance traveled during the leap from the takeoff point.")]
        public float HorizontalLeapDistance = 8f;

        [Header("Damage")]
        [Tooltip("Damage dealt by the slam impact.")]
        public float SlamDamage = 40f;

        [Tooltip("If true, HighDive slam hit force-staggers the player.")]
        public bool StaggerPlayerOnHit = true;

        [Tooltip("Controls how high-dive slam impact damage range and falloff are evaluated.")]
        public LeapSlamDamageConfig SlamDamageConfig = new LeapSlamDamageConfig { Range = 4f, FullDamageRadiusPercent = 1f, EdgeDamagePercent = 1f };

        [Header("SFX/VFX")]
        [Tooltip("Sound effect played when High Dive starts.")]
        public AudioClip AttackSFX;

        [Tooltip("Sound effect played on slam impact.")]
        public AudioClip ImpactSFX;

        [Tooltip("VFX prefab spawned on slam impact.")]
        public GameObject ImpactVFX;
    }

    /// <summary>
    /// Configuration for the Whirlwind strong attack.
    /// </summary>
    [System.Serializable]
    public class WhirlwindConfig
    {
        [Header("Animation")]
        [Tooltip("Animation trigger for the whirlwind attack.")]
        public string AnimationTrigger = "Attack_Whirlwind";

        [Tooltip("Playback speed multiplier for the whirlwind animation clip.")]
        public float AnimationSpeedMultiplier = 1f;

        [Tooltip("How many seconds before leap-up ends to trigger JumpArcResolution. Use this to align slam-down animation with landing.")]
        [Min(0f)] public float JumpArcResolutionLeadTime = 0f;

        [Tooltip("Playback speed multiplier specifically for JumpArcResolution during Whirlwind leap slam.")]
        [Min(0.1f)] public float JumpArcResolutionAnimSpeedMultiplier = 1f;

        [Header("Range")]
        [Tooltip("Minimum distance to player for Whirlwind to be eligible.")]
        public float RangeMin = 2f;

        [Tooltip("Maximum distance to player for Whirlwind to be eligible.")]
        public float RangeMax = 12f;

        [Header("Suction")]
        [Tooltip("Duration of the spinning suction phase.")]
        public float SuctionDuration = 4f;

        [Tooltip("Movement speed multiplier (relative to Cleanser walking speed) while spinning toward the player.")]
        [Range(0.05f, 1f)] public float ChaseSpeedMultiplier = 0.35f;

        [Tooltip("Minimum world-space movement speed while spinning toward the player. Final speed uses the higher of this value or (baseSpeed * ChaseSpeedMultiplier).")]
        [Min(0f)] public float MinimumChaseSpeed = 1.5f;

        [Tooltip("How close the Cleanser tries to stay from the player during Whirlwind chase. Prevents overshoot/rubber-banding.")]
        [Min(0f)] public float ChaseStopDistance = 1.25f;
        
        [Tooltip("Pull strength of the suction effect.")]
        public float SuctionStrength = 12f;
        
        [Tooltip("Maximum pull strength when player is close.")]
        public float MaxSuctionStrength = 20f;
        
        [Tooltip("Effective radius of the suction.")]
        public float SuctionRadius = 15f;

        [Header("Damage")]
        [Tooltip("Delay before the shared whirlwind/aggression collider is re-enabled after a successful damage tick.")]
        [FormerlySerializedAs("DamageTickInterval")]
        public float DamageColliderRearmDelay = 0.5f;
        
        [Tooltip("Damage per tick.")]
        public float DamagePerTick = 5f;

        [Tooltip("If true, Whirlwind damage hits force-stagger the player.")]
        public bool StaggerPlayerOnHit = true;

        [Tooltip("Inner percentage of the whirlwind collider radius that deals full damage.")]
        [Range(0f, 1f)] public float FullDamageRadiusPercent = 0.4f;

        [Tooltip("Damage percentage dealt at the outer edge of the whirlwind collider.")]
        [Range(0f, 1f)] public float EdgeDamagePercent = 0f;

        [Tooltip("If true, aggression value changes are paused while whirlwind is active.")]
        public bool PauseAggressionChangesDuringWhirlwind = true;

        [Header("Leap Slam")]
        [Tooltip("Fixed distance the Cleanser leaps for the slam (direction toward player).")]
        public float LeapDistance = 8f;
        
        [Tooltip("Duration of the leap.")]
        public float LeapDuration = 0.6f;
        
        [Tooltip("Damage dealt by the slam.")]
        public float SlamDamage = 30f;

        [Tooltip("Controls how whirlwind leap/slam impact damage range and falloff are evaluated.")]
        public LeapSlamDamageConfig SlamDamageConfig = new LeapSlamDamageConfig { Range = 5f, FullDamageRadiusPercent = 1f, EdgeDamagePercent = 1f };

        [Header("SFX/VFX")]
        [Tooltip("Looping sound during spin phase.")]
        public AudioClip SpinSFX;
        
        [Tooltip("Sound effect on slam impact.")]
        public AudioClip SlamSFX;
        
        [Tooltip("VFX prefab for the spinning whirlwind.")]
        public GameObject SpinVFX;
        
        [Tooltip("VFX prefab for the slam impact.")]
        public GameObject SlamVFX;
    }

    /// <summary>
    /// Configuration for the Double Maximum Sweep ultimate attack.
    /// </summary>
    [System.Serializable]
    public class DoubleMaximumSweepConfig
    {
        [Header("Animation")]
        [Tooltip("Main double-sweep animation.")]
        public string UltimateTrigger = "Ultimate";

        [Tooltip("Playback speed multiplier applied to ultimate animation clips.")]
        public float AnimationSpeedMultiplier = 1f;

        [Tooltip("Playback speed multiplier specifically for JumpArcResolution during ultimate slam-down.")]
        [Min(0.1f)] public float JumpArcResolutionAnimSpeedMultiplier = 1f;

        [Tooltip("Jump arc animation used for repositioning/float setup.")]
        public string JumpArcBaseTrigger = "JumpArcBase";

        [Tooltip("Looping hover animation played while charging in the air.")]
        public string JumpArcHoldTrigger = "JumpArcHold";

        [Tooltip("Jump arc animation used when ultimate is canceled.")]
        public string JumpArcCancelTrigger = "JumpArcCancel";

        [Tooltip("Jump arc animation used for final crash-down resolution.")]
        public string JumpArcResolutionTrigger = "JumpArcResolution";

        [Tooltip("Jump animation used when relocating to sweep/start positions.")]
        public string JumpFullTrigger = "JumpFull";

        [Tooltip("Travel duration for JumpFull repositioning hops.")]
        public float JumpFullTravelDuration = 1f;

        [Header("Sweep Projectiles")]
        [Tooltip("Projectile settings for the low sweep.")]
        public CrescentArcProjectileConfig LowSweepProjectile = new CrescentArcProjectileConfig();

        [Tooltip("Projectile settings for the mid sweep.")]
        public CrescentArcProjectileConfig MidSweepProjectile = new CrescentArcProjectileConfig();

        [Header("Floating Phase")]
        [Tooltip("Hover Y offset relative to Ultimate Arena Center Point.")]
        public float HoverHeightOffset = 6f;

        [Tooltip("Time the Cleanser charges the massive strike in the air.")]
        public float ChargeUpTime = 8f;

        [Tooltip("How long the hover timer is paused whenever Cleanser takes damage during hover phase.")]
        public float HoverTimerPauseOnDamage = 0.25f;

        [Tooltip("Yaw rotation speed (degrees/sec) while hovering. Can be negative for opposite direction.")]
        public float HoverRotationSpeed = 20f;
        
        [Tooltip("Delay added when hit by an aerial attack.")]
        public float AerialHitDelay = 2f;
        
        [Tooltip("Number of valid plunge-finisher hits required to cancel the ultimate hover phase.")]
        public int RequiredAerialHits = 2;

        [Header("Massive Strike")]
        [Tooltip("Damage dealt if massive strike is NOT canceled.")]
        public float MassiveStrikeDamage = 80f;
        
        [Tooltip("Damage mitigation cap when guarding (0.3 = 30% max reduction).")]
        [Range(0f, 0.5f)] public float GuardMitigationCap = 0.3f;
        
        [Tooltip("AoE radius of the massive strike.")]
        public float MassiveStrikeRadius = 20f;

        [Header("Camera")]
        [Tooltip("Play cutscene camera on first use of this ultimate.")]
        public bool PlayCutsceneOnFirstUse = true;
        
        [Tooltip("Duration of the cutscene (timer is paused during this).")]
        public float CutsceneDuration = 4f;

        [Header("SFX/VFX")]
        [Tooltip("Sound when ultimate is initiated (wall jump).")]
        public AudioClip InitiateSFX;
        
        [Tooltip("Sound when crescent sweep is fired.")]
        public AudioClip SweepSFX;
        
        [Tooltip("Sound during charge up phase (looping).")]
        public AudioClip ChargeSFX;
        
        [Tooltip("Sound when massive strike hits.")]
        public AudioClip MassiveStrikeSFX;
        
        [Tooltip("VFX prefab for the massive strike impact.")]
        public GameObject MassiveStrikeVFX;
    }

    /// <summary>
    /// Full attack descriptor for Cleanser attacks.
    /// Uses single animation clips with animation events for timing.
    /// </summary>
    [System.Serializable]
    public class CleanserAttackDescriptor
    {
        [Tooltip("Unique identifier for this attack.")]
        public string ID;
        
        [Tooltip("Category determines guard/parry rules.")]
        public AttackCategory Category;
        
        [Tooltip("Base damage dealt by this attack.")]
        public float BaseDamage = 10f;
        
        [Tooltip("Cooldown before this attack can be used again.")]
        public float Cooldown = 1f;
        
        [Tooltip("Minimum distance to player for this attack to be eligible.")]
        public float RangeMin = 0f;
        
        [Tooltip("Maximum distance to player for this attack to be eligible.")]
        public float RangeMax = 5f;

        [Header("Animation")]
        [Tooltip("Single animator trigger for the full attack animation. Animation events drive timing.")]
        public string AnimationTrigger;

        [Tooltip("Playback speed multiplier for this attack animation clip.")]
        public float AnimationSpeedMultiplier = 1f;
        
        [Header("Multi-Part Attack")]
        [Tooltip("If true, this attack has multiple damage phases with different rules.")]
        public bool IsMultiPart = false;
        
        [Tooltip("Categories for each part of a multi-part attack (e.g., Halberd, Wing).")]
        public AttackCategory[] PartCategories;

        [Header("Special Flags")]
        [Tooltip("If true, Cleanser has damage reduction during windup (wing block).")]
        public bool HasWindupDamageReduction = false;
        
        [Tooltip("Damage reduction multiplier during windup (0.5 = 50% damage taken).")]
        [Range(0f, 1f)] public float WindupDamageReduction = 0.5f;
        
        [Tooltip("If true, attack includes movement/dash.")]
        public bool IncludesMovement = false;
        
        [Tooltip("Distance to move during attack (if IncludesMovement is true).")]
        public float MovementDistance = 3f;
        
        [Tooltip("If true, attack can stun the player.")]
        public bool CanStunPlayer = false;

        [Tooltip("If true, this attack's hit windows force-stagger the player.")]
        public bool StaggerPlayerOnHit = true;

        [Header("Projectile (Optional)")]
        [Tooltip("Optional projectile settings for attacks that spawn projectiles (for example DiagUpwardSlash).")]
        public CrescentArcProjectileConfig ProjectileConfig = new CrescentArcProjectileConfig();

        [Header("SFX/VFX")]
        [Tooltip("Sound effect played at attack start.")]
        public AudioClip AttackSFX;
        
        [Tooltip("Sound effect played on impact.")]
        public AudioClip ImpactSFX;
        
        [Tooltip("VFX prefab spawned at attack start.")]
        public GameObject AttackVFX;
        
        [Tooltip("VFX prefab spawned on impact.")]
        public GameObject ImpactVFX;
    }

    /// <summary>
    /// Configuration for the Knockback attack.
    /// Uses external force to push the player away.
    /// </summary>
    [System.Serializable]
    public class KnockbackAttackConfig
    {
        [Header("Force Settings")]
        [Tooltip("Horizontal knockback force applied to player.")]
        public float KnockbackForce = 15f;
        
        [Tooltip("Vertical force component (lifts player slightly).")]
        public float VerticalForce = 5f;
        
        [Tooltip("Range at which knockback affects player.")]
        public float KnockbackRadius = 4f;
        
        [Tooltip("Damage dealt by the knockback attack.")]
        public float Damage = 15f;

        [Header("Animation")]
        [Tooltip("Animation trigger for the knockback attack.")]
        public string AnimationTrigger = "Knockback";

        [Tooltip("Playback speed multiplier for the knockback animation clip.")]
        public float AnimationSpeedMultiplier = 1f;

        [Header("SFX/VFX")]
        [Tooltip("Sound effect when attack starts.")]
        public AudioClip AttackSFX;
        
        [Tooltip("Sound effect on impact.")]
        public AudioClip ImpactSFX;
        
        [Tooltip("VFX prefab for the impact effect.")]
        public GameObject ImpactVFX;
    }

    /// <summary>
    /// Configuration for the Gap Closing Dash (movement only, no attack).
    /// </summary>
    [System.Serializable]
    public class GapClosingDashConfig
    {
        [Header("Dash Settings")]
        [HideInInspector]
        [Tooltip("Legacy field (unused). Dash movement speed is now derived from distance / DashDuration.")]
        public float DashSpeed = 25f;
        
        [Tooltip("Duration of the dash.")]
        public float DashDuration = 0.3f;
        
        [Tooltip("Minimum distance to player required to use dash.")]
        public float MinDistanceToUse = 8f;
        
        [Tooltip("Target distance from player to stop dashing.")]
        public float TargetStopDistance = 3f;

        [Header("Aggression Requirements")]
        [HideInInspector]
        [Tooltip("Legacy field (unused). Dash usage is now chance-based per aggression level.")]
        public int MinAggressionLevel = 4;

        [Header("Combo Dash Chance by Aggression")]
        [Tooltip("Chance to use gap-close dash during combo repositioning at aggression Level 1.")]
        [Range(0f, 1f)] public float ComboDashChanceLevel1 = 0.2f;

        [Tooltip("Chance to use gap-close dash during combo repositioning at aggression Level 2.")]
        [Range(0f, 1f)] public float ComboDashChanceLevel2 = 0.4f;

        [Tooltip("Chance to use gap-close dash during combo repositioning at aggression Level 3.")]
        [Range(0f, 1f)] public float ComboDashChanceLevel3 = 0.7f;

        [Tooltip("Chance to use gap-close dash during combo repositioning at aggression Level 4.")]
        [Range(0f, 1f)] public float ComboDashChanceLevel4 = 1f;

        [Tooltip("Chance to use gap-close dash during combo repositioning at aggression Level 5.")]
        [Range(0f, 1f)] public float ComboDashChanceLevel5 = 1f;

        public float GetComboDashChance(AggressionLevel level)
        {
            switch (level)
            {
                case AggressionLevel.Level1: return ComboDashChanceLevel1;
                case AggressionLevel.Level2: return ComboDashChanceLevel2;
                case AggressionLevel.Level3: return ComboDashChanceLevel3;
                case AggressionLevel.Level4: return ComboDashChanceLevel4;
                case AggressionLevel.Level5: return ComboDashChanceLevel5;
                default: return ComboDashChanceLevel1;
            }
        }

        [Header("Animation")]
        [Tooltip("Animation trigger for the dash.")]
        public string AnimationTrigger = "GapCloseDash";

        [Tooltip("Playback speed multiplier for the dash animation clip.")]
        public float AnimationSpeedMultiplier = 1f;

        [Header("SFX/VFX")]
        [Tooltip("Sound effect during dash.")]
        public AudioClip DashSFX;
        
        [Tooltip("VFX prefab for dash trail/afterimage.")]
        public GameObject DashVFX;
    }
}
