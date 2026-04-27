using EnemyBehavior.Boss.Cleanser;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach this to a trigger-collider GameObject placed at the entrance to the Cleanser boss arena.
/// While the player has NOT entered the trigger:
///   - The boss NavMeshAgent stays disabled (boss idles in place).
///   - The boss HP bar stays hidden.
/// When the player enters the trigger for the first time:
///   - The NavMeshAgent is enabled.
///   - The boss HP bar is revealed.
///   - CleanserBrain.StartFight() is called to begin the main combat loop.
///   - This trigger collider is disabled so it can never fire again.
/// </summary>
public class CleanserEnabler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The root GameObject of the Cleanser boss (must have CleanserBrain).")]
    [SerializeField] private CleanserBrain cleanserBrain;

    [Header("Settings")]
    [Tooltip("Tag that identifies the player. Must match the Player GameObject's tag.")]
    [SerializeField] private string playerTag = "Player";

    private bool fightStarted;

    void Awake()
    {
        // Auto-find brain reference if not set in the Inspector
        if (cleanserBrain == null)
            cleanserBrain = FindObjectOfType<CleanserBrain>();

        // Warn if collider is not set to Is Trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("[CleanserEnabler] The collider on this GameObject is not set to 'Is Trigger'. " +
                             "Please enable 'Is Trigger' in the Inspector.", this);
        }

        // Disable the boss NavMeshAgent until the fight begins
        if (cleanserBrain != null)
        {
            var agent = cleanserBrain.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (fightStarted) return;
        if (!other.CompareTag(playerTag)) return;

        fightStarted = true;

        // Show the boss HP bar and start the fight
        cleanserBrain?.ShowHealthBar();
        cleanserBrain?.StartFight();

        // Disable this trigger so it never fires again
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}
