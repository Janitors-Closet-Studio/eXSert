using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Interface for pillars to implement their own visual on/off behavior.
    /// Attach a component implementing this interface to your pillar GameObjects.
    /// </summary>
    public interface IPillarVisual
    {
        /// <summary>
        /// Called when the pillar should turn off (boss collided with it).
        /// Implement visual feedback like turning off lights, changing materials, etc.
        /// </summary>
        void TurnOff();
        
        /// <summary>
        /// Called when the pillar should turn back on (arena reset).
        /// Implement visual feedback to restore the pillar's powered state.
        /// </summary>
        void TurnOn();
        
        /// <summary>
        /// Returns true if the pillar is currently powered on.
        /// </summary>
        bool IsPoweredOn { get; }
    }

    /// <summary>
    /// Represents a single charge segment (from one point to another).
    /// </summary>
    [System.Serializable]
    public class ChargeSegment
    {
        [Tooltip("Starting position for this charge segment")]
        public Transform Start;
        [Tooltip("Ending position for this charge segment")]
        public Transform End;
        
        /// <summary>
        /// If Start and End are the same as another segment's End and Start,
        /// the boss can chain directly without repositioning.
        /// </summary>
        public bool IsValid => Start != null && End != null;
    }

    /// <summary>
    /// A combo is a sequence of charge segments executed in order.
    /// The boss will charge through each segment, then do a targeted charge at the player.
    /// </summary>
    [System.Serializable]
    public class LaneCombo
    {
        [Tooltip("Name for this combo (for debugging/identification)")]
        public string ComboName = "Combo";
        
        [Tooltip("The sequence of charge segments in this combo")]
        public List<ChargeSegment> Segments = new List<ChargeSegment>();
        
        /// <summary>
        /// Returns true if this combo has at least one valid segment.
        /// </summary>
        public bool IsValid => Segments != null && Segments.Count > 0 && Segments[0].IsValid;
        
        /// <summary>
        /// Number of charge segments in this combo.
        /// </summary>
        public int SegmentCount => Segments?.Count ?? 0;
    }

    public sealed class BossArenaManager : MonoBehaviour
    {
        [Header("Arena Elements")]
        public List<GameObject> Walls;
        public List<GameObject> Pillars;

        [Header("Lane Combos")]
        [Tooltip("Predefined charge combos - each combo is a sequence of charge segments")]
        public List<LaneCombo> LaneCombos = new List<LaneCombo>();

        [Header("Cage Bounds")]
        [Tooltip("Collider defining the inside of the cage area")]
        public Collider CageBounds;

        [Header("Wall Animation (Optional)")]
        [Tooltip("If true, walls animate up/down instead of instant enable/disable")]
        public bool AnimateWalls = false;
        [Tooltip("How fast walls raise/lower (units per second)")]
        public float WallAnimationSpeed = 8f;
        [Tooltip("Height walls raise to when up")]
        public float WallRaisedHeight = 5f;
        [Tooltip("Height walls lower to when down (usually below ground)")]
        public float WallLoweredHeight = -3f;

        /// <summary>
        /// Fired when the boss collides with a pillar during a charge.
        /// The boss should stun and transition back to Duelist form.
        /// </summary>
        public event Action<int> OnPillarHit;

        /// <summary>
        /// Fired when walls state changes. True = raised (cage match active).
        /// </summary>
        public event Action<bool> OnWallsStateChanged;

        public bool WallsAreRaised { get; private set; }

        private List<NavMeshAgent> disabledAgentsOutsideCage = new List<NavMeshAgent>();
        private HashSet<int> destroyedPillars = new HashSet<int>();

        /// <summary>
        /// Returns the number of active (non-destroyed) pillars.
        /// </summary>
        public int ActivePillarCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Pillars.Count; i++)
                {
                    if (Pillars[i] != null && Pillars[i].activeInHierarchy && !destroyedPillars.Contains(i))
                        count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Returns the total number of pillars (including destroyed).
        /// </summary>
        public int TotalPillarCount => Pillars?.Count ?? 0;

        /// <summary>
        /// Returns the number of lane combos configured.
        /// </summary>
        public int ComboCount => LaneCombos?.Count ?? 0;



        public void RaiseWalls(bool up)
        {
            WallsAreRaised = up;

            for (int i = 0; i < Walls.Count; i++)
            {
                if (Walls[i] != null)
                {
                    // Manage NavMeshObstacle carving - this blocks the boss from passing through
                    SetWallNavMeshObstacle(Walls[i], up);
                    
                    if (AnimateWalls)
                    {
                        // Get or add the animator component
                        var animator = Walls[i].GetComponent<WallAnimator>();
                        if (animator == null)
                            animator = Walls[i].AddComponent<WallAnimator>();
                        
                        // Walls stay active the entire time - they're just below the floor when lowered
                        // No need to SetActive, just animate position
                        animator.AnimateTo(
                            up ? WallRaisedHeight : WallLoweredHeight,
                            WallAnimationSpeed);
                    }
                    // If not using animation, walls are always active at their scene positions
                    // (No SetActive calls - walls stay active regardless)
                }
            }

            if (up)
            {
                DisableAgentsOutsideCage();
            }
            else
            {
                ReenableDisabledAgents();
            }

            OnWallsStateChanged?.Invoke(up);
            EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Walls {(up ? "RAISED" : "LOWERED")}");
        }

        private void DisableAgentsOutsideCage()
        {
            if (CageBounds == null) return;

            disabledAgentsOutsideCage.Clear();

            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (var enemy in enemies)
            {
                // Skip the boss - don't disable its agent!
                if (enemy.GetComponent<BossRoombaBrain>() != null)
                    continue;
                
                var agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null && agent.enabled)
                {
                    if (!CageBounds.bounds.Contains(enemy.transform.position))
                    {
                        agent.enabled = false;
                        disabledAgentsOutsideCage.Add(agent);
                        EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Disabled agent outside cage: {enemy.name}");
                    }
                }
            }
        }

        private void ReenableDisabledAgents()
        {
            foreach (var agent in disabledAgentsOutsideCage)
            {
                if (agent != null)
                {
                    agent.enabled = true;
                    EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Re-enabled agent: {agent.name}");
                }
            }
            disabledAgentsOutsideCage.Clear();
        }
        
        /// <summary>
        /// Manages NavMeshObstacle on a wall to block/allow NavMeshAgent passage.
        /// When walls are raised, carving is enabled to block the boss.
        /// When walls are lowered, carving is disabled to allow passage.
        /// </summary>
        private void SetWallNavMeshObstacle(GameObject wall, bool enableCarving)
        {
            // Get or add NavMeshObstacle component
            var obstacle = wall.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = wall.AddComponent<NavMeshObstacle>();
                
                // Configure the obstacle based on the wall's collider
                var collider = wall.GetComponent<Collider>();
                if (collider != null)
                {
                    // Set size slightly larger than collider to prevent clipping
                    // Add buffer on X and Z axes (horizontal) to keep boss model from visually clipping
                    Vector3 size = collider.bounds.size;
                    size.x += 2f; // Add 1 unit buffer on each side
                    size.z += 2f;
                    obstacle.size = size;
                    obstacle.center = wall.transform.InverseTransformPoint(collider.bounds.center);
                }
                else
                {
                    // Default size if no collider
                    obstacle.size = new Vector3(3f, 3f, 3f);
                }
                
                EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Added NavMeshObstacle to wall: {wall.name} (size: {obstacle.size})");
            }
            
            // Enable/disable carving based on wall state
            obstacle.carving = enableCarving;
            obstacle.carveOnlyStationary = false; // Carve even when animating
            
            if (enableCarving)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Wall '{wall.name}' NavMesh carving ENABLED - boss cannot pass");
            }
        }

        #region Lane Combo System

        /// <summary>
        /// Gets a specific lane combo by index.
        /// </summary>
        public LaneCombo GetCombo(int comboIndex)
        {
            if (LaneCombos == null || comboIndex < 0 || comboIndex >= LaneCombos.Count)
                return null;
            
            return LaneCombos[comboIndex];
        }

        /// <summary>
        /// Gets a random valid lane combo.
        /// </summary>
        public LaneCombo GetRandomCombo()
        {
            if (LaneCombos == null || LaneCombos.Count == 0)
                return null;
            
            // Filter to only valid combos - preallocate to avoid resizing
            var validCombos = new List<int>(LaneCombos.Count);
            for (int i = 0; i < LaneCombos.Count; i++)
            {
                if (LaneCombos[i] != null && LaneCombos[i].IsValid)
                    validCombos.Add(i);
            }
            
            if (validCombos.Count == 0)
                return null;
            
            int randomIndex = validCombos[UnityEngine.Random.Range(0, validCombos.Count)];
            return LaneCombos[randomIndex];
        }

        /// <summary>
        /// Gets all charge segments for a combo as an array of (start, end) positions.
        /// Returns null if combo is invalid.
        /// </summary>
        public (Vector3 start, Vector3 end)[] GetComboSegments(int comboIndex)
        {
            var combo = GetCombo(comboIndex);
            if (combo == null || !combo.IsValid)
                return null;
            
            var segments = new (Vector3, Vector3)[combo.SegmentCount];
            for (int i = 0; i < combo.SegmentCount; i++)
            {
                var seg = combo.Segments[i];
                if (seg.IsValid)
                {
                    segments[i] = (seg.Start.position, seg.End.position);
                }
                else
                {
                    // Invalid segment - use arena center as fallback
                    Vector3 center = GetArenaCenter();
                    segments[i] = (center, center + Vector3.forward * 3f);
                }
            }
            
            return segments;
        }

        /// <summary>
        /// Gets all charge segments for a combo.
        /// Returns null if combo is invalid.
        /// </summary>
        public (Vector3 start, Vector3 end)[] GetComboSegments(LaneCombo combo)
        {
            if (combo == null || !combo.IsValid)
                return null;
            
            var segments = new (Vector3, Vector3)[combo.SegmentCount];
            for (int i = 0; i < combo.SegmentCount; i++)
            {
                var seg = combo.Segments[i];
                if (seg.IsValid)
                {
                    segments[i] = (seg.Start.position, seg.End.position);
                }
                else
                {
                    Vector3 center = GetArenaCenter();
                    segments[i] = (center, center + Vector3.forward * 3f);
                }
            }
            
            return segments;
        }

        /// <summary>
        /// Returns true if valid combos are configured.
        /// </summary>
        public bool HasValidCombos => LaneCombos != null && LaneCombos.Count > 0;

        #endregion

        /// <summary>
        /// Checks if a pillar is still active (powered on, not deactivated).
        /// </summary>
        public bool IsPillarActive(int pillarIndex)
        {
            if (pillarIndex < 0 || pillarIndex >= Pillars.Count)
                return false;
            
            return Pillars[pillarIndex] != null 
                   && Pillars[pillarIndex].activeInHierarchy 
                   && !destroyedPillars.Contains(pillarIndex);
        }
        
        /// <summary>
        /// Checks if a pillar is powered on (same as active, for clarity).
        /// </summary>
        public bool IsPillarPoweredOn(int pillarIndex) => IsPillarActive(pillarIndex);

        /// <summary>
        /// Called when the boss collides with a pillar during a charge.
        /// Turns off the pillar (visually) and triggers the cage match end sequence.
        /// </summary>
        public void OnPillarCollision(int pillarIndex)
        {
            if (pillarIndex < 0 || pillarIndex >= Pillars.Count)
                return;

            if (destroyedPillars.Contains(pillarIndex))
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Pillar {pillarIndex} already deactivated, ignoring collision");
                return;
            }

            if (Pillars[pillarIndex] != null)
            {
                // Mark pillar as deactivated (but don't hide it!)
                destroyedPillars.Add(pillarIndex);
                
                // Try to notify the pillar to turn off visually (lights, effects, etc.)
                var pillarVisual = Pillars[pillarIndex].GetComponent<IPillarVisual>();
                if (pillarVisual != null)
                {
                    pillarVisual.TurnOff();
                }
                else
                {
                    // Fallback: Try to find a child with "Light" or disable emissive materials
                    TurnOffPillarVisuals(Pillars[pillarIndex]);
                }
                
                EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Pillar {pillarIndex} DEACTIVATED by boss collision! Cage match ending...");
                
                // Lower the walls - the pillar collision breaks the power to the walls
                RaiseWalls(false);
                
                // Fire the event so the boss gets stunned and transitions to Duelist form
                OnPillarHit?.Invoke(pillarIndex);
            }
        }
        
        /// <summary>
        /// Fallback method to visually turn off a pillar when no IPillarVisual is found.
        /// </summary>
        private void TurnOffPillarVisuals(GameObject pillar)
        {
            // Try to disable any lights on the pillar
            var lights = pillar.GetComponentsInChildren<Light>();
            foreach (var light in lights)
            {
                light.enabled = false;
            }
            
            // Try to find and disable any particle systems
            var particles = pillar.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Stop();
            }
            
            // Try to change material color to indicate "off" state
            var renderers = pillar.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.materials)
                {
                    // Disable emission if present
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", Color.black);
                        mat.DisableKeyword("_EMISSION");
                    }
                }
            }
            
            EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Applied fallback visual turn-off for pillar: {pillar.name}");
        }
        
        /// <summary>
        /// Fallback method to visually turn a pillar back on.
        /// </summary>
        private void TurnOnPillarVisuals(GameObject pillar)
        {
            // Re-enable any lights on the pillar
            var lights = pillar.GetComponentsInChildren<Light>();
            foreach (var light in lights)
            {
                light.enabled = true;
            }
            
            // Restart particle systems
            var particles = pillar.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Play();
            }
            
            // Note: We can't easily restore original emission colors without caching them first
            // For now, just log that the pillar visual should implement IPillarVisual for proper reset
            EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Applied fallback visual turn-on for pillar: {pillar.name} (implement IPillarVisual for full control)");
        }

        /// <summary>
        /// Resets all pillars to active/powered state (for testing or new fight).
        /// </summary>
        public void ResetAllPillars()
        {
            destroyedPillars.Clear();
            for (int i = 0; i < Pillars.Count; i++)
            {
                if (Pillars[i] != null)
                {
                    // Make sure the pillar is active in the hierarchy
                    Pillars[i].SetActive(true);
                    
                    // Turn the pillar back on visually
                    var pillarVisual = Pillars[i].GetComponent<IPillarVisual>();
                    if (pillarVisual != null)
                    {
                        pillarVisual.TurnOn();
                    }
                    else
                    {
                        TurnOnPillarVisuals(Pillars[i]);
                    }
                }
            }
            EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] All {Pillars.Count} pillars reset and powered on");
        }

        /// <summary>
        /// Gets the position of the arena center (for charge targeting).
        /// </summary>
        public Vector3 GetArenaCenter()
        {
            if (CageBounds != null)
                return CageBounds.bounds.center;
            return transform.position;
        }

    [ContextMenu("Debug: Raise Walls")]
    public void DebugRaiseWalls() => RaiseWalls(true);

    [ContextMenu("Debug: Lower Walls")]
    public void DebugLowerWalls() => RaiseWalls(false);

    [ContextMenu("Debug: Reset Pillars")]
    public void DebugResetPillars() => ResetAllPillars();
    
    [ContextMenu("Debug: Start Cage Match")]
    public void DebugStartCageMatch()
    {
        ResetAllPillars();
        RaiseWalls(true);
        EnemyBehaviorDebugLogBools.Log(nameof(BossArenaManager), $"[BossArenaManager] Cage match started - walls raised, pillars powered on");
    }
}

    /// <summary>
    /// Simple component to animate wall position vertically.
    /// Smoothly moves from current position to target position.
    /// </summary>
    internal sealed class WallAnimator : MonoBehaviour
    {
        private float startY;
        private float targetY;
        private float speed;
        private bool animating;
        private bool initialized;

        public void AnimateTo(float targetHeight, float animSpeed)
        {
            // Capture the CURRENT position as start point (prevents teleporting)
            startY = transform.position.y;
            targetY = targetHeight;
            speed = animSpeed;
            animating = true;
            initialized = true;
        }

        private void Update()
        {
            if (!animating || !initialized) return;

            Vector3 pos = transform.position;
            float diff = targetY - pos.y;
            
            if (Mathf.Abs(diff) < 0.01f)
            {
                pos.y = targetY;
                transform.position = pos;
                animating = false;
            }
            else
            {
                // Smoothly move towards target from wherever we currently are
                pos.y = Mathf.MoveTowards(pos.y, targetY, speed * Time.deltaTime);
                transform.position = pos;
            }
        }
    }
}