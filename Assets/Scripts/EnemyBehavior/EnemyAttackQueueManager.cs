// EnemyAttackQueueManager.cs
// Purpose: Global queue system that controls how many enemies can attack simultaneously.
// Works with: Any enemy implementing IQueuedAttacker interface.
// Notes: Enemies register on spawn, unregister on death/despawn. Attackers within the allowed slots can attack, then move to back.

using System.Collections.Generic;
using UnityEngine;

namespace EnemyBehavior
{
    /// <summary>
    /// Interface for enemies that participate in the attack queue system.
    /// Implement this on any enemy type that should wait its turn to attack.
    /// </summary>
    public interface IQueuedAttacker
    {
        /// <summary>
        /// Returns true if this attacker is a boss (used for filtering).
        /// </summary>
        bool IsBoss { get; }

        /// <summary>
        /// Returns true if this attacker is alive and able to attack.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Returns the GameObject for null-checking and logging.
        /// </summary>
        GameObject AttackerGameObject { get; }
    }

    /// <summary>
    /// Singleton manager that controls enemy attack order.
    /// Enemies within the first N slots (maxConcurrentAttackers) are allowed to attack.
    /// After attacking, they move to the back of the queue.
    /// </summary>
    public sealed class EnemyAttackQueueManager : MonoBehaviour
    {
        public static EnemyAttackQueueManager Instance { get; private set; }

        [Header("Queue Settings")]
        [Tooltip("How many enemies can attack at the same time. 1 = only front of queue, 3 = indices 0, 1, and 2 can all attack.")]
        [SerializeField, Min(1)] private int maxConcurrentAttackers = 1;
        
        [Tooltip("If true, boss enemies will also use the queue system. If false, bosses attack freely.")]
        [SerializeField] private bool includeBossesInQueue = false;

        [Tooltip("Maximum time (seconds) one enemy can hold an attack slot before being cycled. Prevents softlocks.")]
        [SerializeField] private float attackTimeout = 3f;

        [Tooltip("If true, logs queue operations to console for debugging.")]
        [SerializeField] private bool debugLogging = false;

        [Header("Debug Info (Read-Only)")]
        [SerializeField, ReadOnly] private int queueCount;
        [SerializeField, ReadOnly] private int activeAttackerCount;
        [SerializeField, ReadOnly] private string activeAttackerNames = "None";

        // Using List instead of Queue for easier removal of dead enemies mid-queue
        private readonly List<IQueuedAttacker> attackQueue = new List<IQueuedAttacker>(32);
        
        // Track multiple active attackers and their start times
        private readonly Dictionary<IQueuedAttacker, float> activeAttackers = new Dictionary<IQueuedAttacker, float>(8);

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyAttackQueueManager), "[EnemyAttackQueueManager] Duplicate instance detected, destroying this one.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // Clean up any null/dead entries from the queue
            CleanupDeadAttackers();

            // Update debug display
            queueCount = attackQueue.Count;
            activeAttackerCount = activeAttackers.Count;
            
            // Build active attacker names for debug
            if (activeAttackers.Count > 0)
            {
                var names = new System.Text.StringBuilder();
                foreach (var kvp in activeAttackers)
                {
                    if (names.Length > 0) names.Append(", ");
                    names.Append(kvp.Key?.AttackerGameObject?.name ?? "NULL");
                }
                activeAttackerNames = names.ToString();
            }
            else
            {
                activeAttackerNames = "None";
            }

            // Check for attack timeouts
            CheckAttackTimeouts();
        }

        private void CheckAttackTimeouts()
        {
            // Need to collect timed-out attackers first to avoid modifying collection during iteration
            var timedOut = new List<IQueuedAttacker>();
            
            foreach (var kvp in activeAttackers)
            {
                if (Time.time - kvp.Value > attackTimeout)
                {
                    timedOut.Add(kvp.Key);
                }
            }
            
            foreach (var attacker in timedOut)
            {
                if (debugLogging)
                {
                    EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Attack timeout! Cycling attacker: {attacker?.AttackerGameObject?.name}");
                }
                CycleAttacker(attacker);
            }
        }

        /// <summary>
        /// Register an enemy to participate in the attack queue.
        /// Call this when an enemy spawns or becomes active.
        /// </summary>
        public void Register(IQueuedAttacker attacker)
        {
            if (attacker == null) return;

            // Check if bosses should be excluded
            if (attacker.IsBoss && !includeBossesInQueue)
            {
                if (debugLogging)
                {
                    EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Boss '{attacker.AttackerGameObject?.name}' excluded from queue (includeBossesInQueue=false)");
                }
                return;
            }

            // Prevent duplicate registration
            if (attackQueue.Contains(attacker))
            {
                if (debugLogging)
                {
                    EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyAttackQueueManager), $"[AttackQueue] '{attacker.AttackerGameObject?.name}' already registered!");
                }
                return;
            }

            attackQueue.Add(attacker);

            if (debugLogging)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Registered: {attacker.AttackerGameObject?.name} (Queue size: {attackQueue.Count})");
            }
        }

        /// <summary>
        /// Unregister an enemy from the attack queue.
        /// Call this when an enemy dies, despawns, or is destroyed.
        /// </summary>
        public void Unregister(IQueuedAttacker attacker)
        {
            if (attacker == null) return;

            bool wasRemoved = attackQueue.Remove(attacker);
            
            // Also remove from active attackers if present
            activeAttackers.Remove(attacker);

            if (debugLogging && wasRemoved)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Unregistered: {attacker.AttackerGameObject?.name} (Queue size: {attackQueue.Count})");
            }
        }

        /// <summary>
        /// Check if this attacker is allowed to attack right now.
        /// Returns true if the attacker is within the first N slots (maxConcurrentAttackers),
        /// OR if they are a boss and bosses are excluded from the queue.
        /// </summary>
        public bool CanAttack(IQueuedAttacker attacker)
        {
            if (attacker == null) return false;

            // Bosses attack freely if not included in queue
            if (attacker.IsBoss && !includeBossesInQueue)
            {
                return true;
            }

            // Not in queue? They shouldn't be attacking - but register them as a failsafe
            if (!attackQueue.Contains(attacker))
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyAttackQueueManager), $"[AttackQueue] '{attacker.AttackerGameObject?.name}' tried to attack but wasn't registered! Auto-registering...");
                Register(attacker);
                return false; // They go to back of queue
            }

            // Clean up dead attackers first
            CleanupDeadAttackers();

            // Check if queue is empty (shouldn't happen if attacker is registered, but safety check)
            if (attackQueue.Count == 0) return false;

            // Get attacker's position in queue
            int position = attackQueue.IndexOf(attacker);
            
            // Can attack if within the allowed slots
            if (position >= 0 && position < maxConcurrentAttackers)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Call this when an enemy begins their attack.
        /// Marks the attack as in-progress and starts the timeout timer.
        /// </summary>
        public void BeginAttack(IQueuedAttacker attacker)
        {
            if (attacker == null) return;

            // Bosses don't need to track if excluded
            if (attacker.IsBoss && !includeBossesInQueue) return;

            if (!CanAttack(attacker))
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(EnemyAttackQueueManager), $"[AttackQueue] '{attacker.AttackerGameObject?.name}' called BeginAttack but CanAttack returned false!");
                return;
            }

            // Track this attacker as active
            activeAttackers[attacker] = Time.time;

            if (debugLogging)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Attack started: {attacker.AttackerGameObject?.name} (Active: {activeAttackers.Count})");
            }
        }

        /// <summary>
        /// Call this when an enemy finishes their attack (hit or miss).
        /// Moves them to the back of the queue and allows the next enemy to attack.
        /// </summary>
        public void FinishAttack(IQueuedAttacker attacker)
        {
            if (attacker == null) return;

            // Bosses don't need to cycle if excluded
            if (attacker.IsBoss && !includeBossesInQueue) return;

            CycleAttacker(attacker);
        }

        /// <summary>
        /// Force the current attack to finish and cycle all active attackers.
        /// Used for timeout or emergency situations.
        /// </summary>
        public void ForceFinishCurrentAttack()
        {
            // Cycle all active attackers
            var toFinish = new List<IQueuedAttacker>(activeAttackers.Keys);
            foreach (var attacker in toFinish)
            {
                CycleAttacker(attacker);
            }
        }

        /// <summary>
        /// Gets the current position in queue for an attacker (0 = front).
        /// Returns -1 if not in queue.
        /// </summary>
        public int GetQueuePosition(IQueuedAttacker attacker)
        {
            if (attacker == null) return -1;

            // Bosses are always "position 0" if excluded from queue
            if (attacker.IsBoss && !includeBossesInQueue) return 0;

            return attackQueue.IndexOf(attacker);
        }

        /// <summary>
        /// Returns true if the given attacker is within the allowed attack slots.
        /// </summary>
        public bool IsInAttackSlot(IQueuedAttacker attacker)
        {
            if (attacker == null) return false;
            if (attacker.IsBoss && !includeBossesInQueue) return true;

            CleanupDeadAttackers();
            int position = attackQueue.IndexOf(attacker);
            return position >= 0 && position < maxConcurrentAttackers;
        }

        /// <summary>
        /// Returns true if the given attacker is at the front of the queue (index 0).
        /// </summary>
        public bool IsAtFrontOfQueue(IQueuedAttacker attacker)
        {
            if (attacker == null) return false;
            if (attacker.IsBoss && !includeBossesInQueue) return true;

            CleanupDeadAttackers();
            return attackQueue.Count > 0 && attackQueue[0] == attacker;
        }

        /// <summary>
        /// Returns the current queue count (for debugging/UI).
        /// </summary>
        public int QueueCount => attackQueue.Count;

        /// <summary>
        /// Returns how many attackers are currently in an active attack.
        /// </summary>
        public int ActiveAttackerCount => activeAttackers.Count;
        
        /// <summary>
        /// Returns the maximum number of concurrent attackers allowed.
        /// </summary>
        public int MaxConcurrentAttackers => maxConcurrentAttackers;

        // Move attacker from current position to back of queue
        private void CycleAttacker(IQueuedAttacker attacker)
        {
            if (attacker == null) return;

            // Remove from active attackers
            activeAttackers.Remove(attacker);
            
            // Remove from current position in queue
            bool removed = attackQueue.Remove(attacker);

            // Add to back if still alive
            if (removed && attacker.IsAlive && attacker.AttackerGameObject != null)
            {
                attackQueue.Add(attacker);

                if (debugLogging)
                {
                    EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Cycled to back: {attacker.AttackerGameObject?.name} (Queue size: {attackQueue.Count})");
                }
            }
        }

        // Remove any null or dead attackers from the queue
        private void CleanupDeadAttackers()
        {
            for (int i = attackQueue.Count - 1; i >= 0; i--)
            {
                var attacker = attackQueue[i];
                if (attacker == null || !attacker.IsAlive || attacker.AttackerGameObject == null)
                {
                if (debugLogging && attacker?.AttackerGameObject != null)
                    {
                        EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Cleaned up dead attacker at index {i}");
                    }
                    attackQueue.RemoveAt(i);
                    activeAttackers.Remove(attacker);
                }
            }
        }

        [ContextMenu("Debug: Log Queue State")]
        public void DebugLogQueueState()
        {
            EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] === Queue State ===");
            EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Count: {attackQueue.Count}");
            EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Max Concurrent Attackers: {maxConcurrentAttackers}");
            EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Active Attackers: {activeAttackers.Count}");
            EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue] Include Bosses: {includeBossesInQueue}");

            for (int i = 0; i < attackQueue.Count; i++)
            {
                var attacker = attackQueue[i];
                string name = attacker?.AttackerGameObject?.name ?? "NULL";
                string status = attacker?.IsAlive == true ? "Alive" : "Dead";
                string boss = attacker?.IsBoss == true ? " [BOSS]" : "";
                string canAttack = i < maxConcurrentAttackers ? " [CAN ATTACK]" : "";
                string isActive = activeAttackers.ContainsKey(attacker) ? " [ATTACKING]" : "";
                EnemyBehaviorDebugLogBools.Log(nameof(EnemyAttackQueueManager), $"[AttackQueue]   [{i}] {name} ({status}){boss}{canAttack}{isActive}");
            }
        }

        [ContextMenu("Debug: Force Cycle All")]
        public void DebugForceCycleAll()
        {
            ForceFinishCurrentAttack();
        }
    }
}