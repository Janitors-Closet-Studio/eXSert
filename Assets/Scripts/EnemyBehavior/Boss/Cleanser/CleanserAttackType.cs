// CleanserAttackType.cs
// Purpose: Defines all attack types for the Cleanser boss and their configurations.
// Works with: CleanserBrain, CleanserComboSystem

using UnityEngine;

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
        None,
        Lunge,                  // Stabbing lunge with wing block during windup
        OverheadCleave,         // Overhead strike downward
        SlashIntoSlap,          // Two-part: halberd slash then wing backhand
        RakeIntoSpinSlash,      // Wing rake followed by 360 spin slash (or just spin slash)
        SpareToss,              // Projectile throw
        SpinDash,               // Meta Knight style spinning dash (1 or 3 times)
        LegSweep,               // Low sweep requiring jump to avoid
        Knockback,              // Pushes player away using external force
        MiniCrescentWave        // Quick ranged crescent wave slash
    }

    /// <summary>
    /// Strong (combo finisher) attack types for the Cleanser boss.
    /// </summary>
    public enum CleanserStrongAttack
    {
        None,
        HighDive,               // Leap high then slam down
        AnimeDashSlash,         // Pentagram-style dash attacks around player
        Whirlwind               // Spinning suction + leap slam
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
        public string AnimationTrigger = "SpareToss";

        [Header("Throw Type")]
        [Tooltip("If true, throws sequentially (one after another). If false, throws simultaneously.")]
        public bool ThrowSequentially = true;
        
        [Tooltip("Number of weapons to throw (1 or 2).")]
        [Range(1, 2)] public int ProjectileCount = 1;

        [Header("Path Type Selection")]
        [Tooltip("Which path types are allowed. One will be randomly selected at runtime.")]
        public SpareTossPathType AllowedPathTypes = SpareTossPathType.Straight | SpareTossPathType.StraightReturn | SpareTossPathType.CurvedBoomerang;

        [Header("Path Type (Runtime - Read Only)")]
        [Tooltip("Straight path to target. Set at runtime based on AllowedPathTypes.")]
        public bool UseStraightPath = true;
        
        [Tooltip("If true, projectile returns like a boomerang even on straight path. Set at runtime.")]
        public bool ReturnsOnStraightPath = false;
        
        [Tooltip("Curved boomerang path (goes past player, curves back). Set at runtime.")]
        public bool UseCurvedBoomerang = false;

        [Header("Boomerang Settings")]
        [Tooltip("How far the boomerang curves outward from the throw direction (min, max).")]
        public Vector2 CurveWidthRange = new Vector2(5f, 10f);
        
        [Tooltip("How far past the player the boomerang travels before returning (min, max).")]
        public Vector2 OvershootDistanceRange = new Vector2(3f, 8f);
        
        [Tooltip("Speed of the projectile (min, max).")]
        public Vector2 ProjectileSpeedRange = new Vector2(15f, 25f);

        [Header("Homing")]
        [Tooltip("If true, boomerang updates target position at intervals.")]
        public bool UseHoming = true;
        
        [Tooltip("How often the homing target updates (seconds).")]
        public float HomingUpdateInterval = 0.3f;
        
        [Tooltip("How strongly the projectile tracks the target (higher = sharper turns).")]
        [Range(0f, 1f)] public float HomingStrength = 0.5f;

        [Header("Collision")]
        [Tooltip("Layer mask for wall collision checks.")]
        public LayerMask WallLayerMask;
        
        [Tooltip("If true, projectile is destroyed on wall impact. If false, bounces/stops.")]
        public bool DestroyOnWallHit = true;

        [Header("SFX")]
        [Tooltip("Sound effect when throwing the weapon.")]
        public AudioClip ThrowSFX;
        
        [Tooltip("Sound effect when projectile hits something.")]
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
    /// Configuration for the Spin Dash attack.
    /// </summary>
    [System.Serializable]
    public class SpinDashConfig
    {
        [Header("Animation")]
        [Tooltip("Animation trigger for the spin dash attack.")]
        public string AnimationTrigger = "SpinDash";

        [Header("Dash Count")]
        [Tooltip("If true, randomly chooses between 1 or 3 dashes. If false, uses TripleDash setting.")]
        public bool RandomDashCount = true;
        
        [Tooltip("If RandomDashCount is false, this determines if it's always 3 dashes (true) or 1 dash (false).")]
        public bool AlwaysTripleDash = false;
        
        [Tooltip("Speed of the spin dash.")]
        public float DashSpeed = 20f;
        
        [Tooltip("Duration of each dash in seconds.")]
        public float DashDuration = 0.4f;
        
        [Tooltip("Delay between consecutive dashes when doing triple dash.")]
        public float DelayBetweenDashes = 0.3f;

        [Header("SFX/VFX")]
        [Tooltip("Sound effect during the dash.")]
        public AudioClip DashSFX;
        
        [Tooltip("VFX prefab spawned during dash (e.g., afterimage trail).")]
        public GameObject DashVFX;
    }

    /// <summary>
    /// Configuration for the Whirlwind strong attack.
    /// </summary>
    [System.Serializable]
    public class WhirlwindConfig
    {
        [Header("Animation")]
        [Tooltip("Animation trigger for the whirlwind attack.")]
        public string AnimationTrigger = "Whirlwind";

        [Header("Suction")]
        [Tooltip("Duration of the spinning suction phase.")]
        public float SuctionDuration = 4f;
        
        [Tooltip("Pull strength of the suction effect.")]
        public float SuctionStrength = 12f;
        
        [Tooltip("Maximum pull strength when player is close.")]
        public float MaxSuctionStrength = 20f;
        
        [Tooltip("Effective radius of the suction.")]
        public float SuctionRadius = 15f;

        [Header("Damage")]
        [Tooltip("Delay between damage ticks when player is inside whirlwind.")]
        public float DamageTickInterval = 0.5f;
        
        [Tooltip("Damage per tick.")]
        public float DamagePerTick = 5f;

        [Header("Leap Slam")]
        [Tooltip("Fixed distance the Cleanser leaps for the slam (direction toward player).")]
        public float LeapDistance = 8f;
        
        [Tooltip("Duration of the leap.")]
        public float LeapDuration = 0.6f;
        
        [Tooltip("AoE radius of the slam.")]
        public float SlamAoERadius = 5f;
        
        [Tooltip("Damage dealt by the slam.")]
        public float SlamDamage = 30f;

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
        [Header("Sweep Projectiles")]
        [Tooltip("Speed of the crescent wave projectiles.")]
        public float WaveSpeed = 25f;
        
        [Tooltip("Height of the low sweep relative to Cleanser position.")]
        public float LowSweepHeight = 0.5f;
        
        [Tooltip("Height of the mid sweep relative to Cleanser position.")]
        public float MidSweepHeight = 3f;
        
        [Tooltip("Width of the crescent wave hitbox.")]
        public float WaveWidth = 20f;
        
        [Tooltip("Damage dealt by each sweep.")]
        public float SweepDamage = 25f;

        [Header("Floating Phase")]
        [Tooltip("Time the Cleanser charges the massive strike in the air.")]
        public float ChargeUpTime = 8f;
        
        [Tooltip("Delay added when hit by an aerial attack.")]
        public float AerialHitDelay = 2f;
        
        [Tooltip("Number of aerial hits required before plunge finisher can cancel.")]
        public int RequiredAerialHits = 2;

        [Header("Platforms")]
        [Tooltip("Height platforms rise to (relative to arena floor).")]
        public float PlatformRiseHeight = 6f;
        
        [Tooltip("Radius of platform orbit around Cleanser.")]
        public float PlatformOrbitRadius = 4f;
        
        [Tooltip("Speed of platform orbit (degrees per second).")]
        public float PlatformOrbitSpeed = 30f;
        
        [Tooltip("Time for platforms to rise.")]
        public float PlatformRiseTime = 1.5f;

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

        [Header("SFX/VFX")]
        [Tooltip("Sound effect when attack starts.")]
        public AudioClip AttackSFX;
        
        [Tooltip("Sound effect on impact.")]
        public AudioClip ImpactSFX;
        
        [Tooltip("VFX prefab for the impact effect.")]
        public GameObject ImpactVFX;
    }

    /// <summary>
    /// Configuration for the Mini Crescent Wave ranged attack.
    /// A quick one-off crescent slash projectile.
    /// </summary>
    [System.Serializable]
    public class MiniCrescentWaveConfig
    {
        [Header("Projectile Settings")]
        [Tooltip("Speed of the crescent wave projectile.")]
        public float WaveSpeed = 20f;
        
        [Tooltip("Damage dealt by the wave.")]
        public float Damage = 20f;
        
        [Tooltip("Width of the wave hitbox.")]
        public float WaveWidth = 8f;
        
        [Tooltip("Height of the wave (relative to Cleanser position).")]
        public float WaveHeight = 1.5f;
        
        [Tooltip("Maximum travel distance before wave dissipates.")]
        public float MaxDistance = 25f;

        [Header("Animation")]
        [Tooltip("Animation trigger for the crescent wave slash.")]
        public string AnimationTrigger = "MiniCrescent";

        [Header("SFX/VFX")]
        [Tooltip("Sound effect when slashing.")]
        public AudioClip SlashSFX;
        
        [Tooltip("Prefab for the crescent wave projectile.")]
        public GameObject WavePrefab;
    }

    /// <summary>
    /// Configuration for the Gap Closing Dash (movement only, no attack).
    /// </summary>
    [System.Serializable]
    public class GapClosingDashConfig
    {
        [Header("Dash Settings")]
        [Tooltip("Speed of the dash.")]
        public float DashSpeed = 25f;
        
        [Tooltip("Duration of the dash.")]
        public float DashDuration = 0.3f;
        
        [Tooltip("Minimum distance to player required to use dash.")]
        public float MinDistanceToUse = 8f;
        
        [Tooltip("Target distance from player to stop dashing.")]
        public float TargetStopDistance = 3f;

        [Header("Aggression Requirements")]
        [Tooltip("Minimum aggression level required to use this dash.")]
        public int MinAggressionLevel = 4;

        [Header("Animation")]
        [Tooltip("Animation trigger for the dash.")]
        public string AnimationTrigger = "GapClose";

        [Header("SFX/VFX")]
        [Tooltip("Sound effect during dash.")]
        public AudioClip DashSFX;
        
        [Tooltip("VFX prefab for dash trail/afterimage.")]
        public GameObject DashVFX;
    }
}
