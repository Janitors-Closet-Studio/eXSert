using EnemyBehavior.Boss;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach this to a trigger-collider GameObject placed at the entrance to the roomba boss arena.
/// While the player has NOT entered the trigger:
///   - The boss NavMeshAgent stays disabled (boss idles in place).
///   - The boss HP bar stays hidden.
/// When the player enters the trigger for the first time:
///   - The NavMeshAgent is enabled.
///   - The boss HP bar is revealed.
///   - BossRoombaBrain.StartFight() is called to begin the fight sequence.
///   - This trigger collider is disabled so it can never fire again.
/// </summary>
public class BossRoombaEnabler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The root GameObject of the Roomba boss (must have BossRoombaBrain and BossHealth).")]
    [SerializeField] private BossRoombaBrain bossRoombaBrain;
    [SerializeField] private BossHealth bossHealth;

    [Header("Settings")]
    [Tooltip("Tag that identifies the player. Must match the Player GameObject's tag.")]
    [SerializeField] private string playerTag = "Player";

    private bool fightStarted;

    void Awake()
    {
        // Auto-find boss references if not set in the Inspector
        if (bossRoombaBrain == null)
            bossRoombaBrain = FindObjectOfType<BossRoombaBrain>();

        if (bossHealth == null && bossRoombaBrain != null)
            bossHealth = bossRoombaBrain.GetComponent<BossHealth>();

        // Make sure the trigger collider on this GameObject is actually set to Is Trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("[BossRoombaEnabler] The collider on this GameObject is not set to 'Is Trigger'. " +
                             "Please enable 'Is Trigger' in the Inspector.", this);
        }

        // Disable the boss NavMeshAgent until the fight begins
        if (bossRoombaBrain != null)
        {
            var agent = bossRoombaBrain.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (fightStarted) return;
        if (!other.CompareTag(playerTag)) return;

        fightStarted = true;

        // Show the boss HP bar
        bossHealth?.ShowHealthBar();

        // Start the fight (re-enables NavMeshAgent internally and begins combat coroutine)
        bossRoombaBrain?.StartFight();

        // Disable this trigger so it never fires again
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}
