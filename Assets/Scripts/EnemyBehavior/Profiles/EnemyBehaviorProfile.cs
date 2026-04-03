// EnemyBehaviorProfile.cs
// Purpose: Serializable data container to tune enemy movement, avoidance, importance, and other AI parameters.
// Works with: BaseEnemy, CrowdAgent, NavMeshAgent settings, planners for hinting.

using UnityEngine;
#pragma warning disable CS0414

[CreateAssetMenu(menuName = "AI/EnemyBehaviorProfile")]
public sealed class EnemyBehaviorProfile : ScriptableObject
{
    [Header("Component Help")]
    [SerializeField, TextArea(4, 8)] private string inspectorHelp =
        "EnemyBehaviorProfile: central tuning for NavMeshAgent movement, avoidance, and planner/crowd hints.\n" +
        "Assign this asset to enemies (BaseEnemy.behaviorProfile or BossRoombaController.profile).\n" +
        "Reasonable starting values are provided below; tweak per enemy type.";

    [Tooltip("NavMeshAgent speed range in m/s. A random value within this range is applied on spawn.\nMy initial values: Crawlers 3–5, Drones 4–7, Boss 2.5–3.5.")]
    public Vector2 SpeedRange = new Vector2(2f, 6f);

    [Tooltip("NavMeshAgent acceleration in m/s² (how quickly the agent reaches speed).\nTypical: 8–20 for ground units, 12–30 for agile/flying.")]
    public float Acceleration = 12f;

    [Tooltip("NavMeshAgent angular speed in degrees/second (turn rate).\nTypical: 240–540. Higher values snap turns faster.")]
    public float AngularSpeed = 360f;

    [Tooltip("Distance in meters the agent stops before the target (prevents overshoot).\nTypical: 0.1–0.5 for small enemies, 1.0–2.0 for large bosses.")]
    public float StoppingDistance = 0.2f;

    [Tooltip("Unity avoidance priority (0 = highest priority, 99 = lowest). Lower value means others yield to this agent.\nTypical: Boss 10–20, Elites 20–40, Regulars 50, Trash 60–80.")]
    [Range(0, 99)]
    public int AvoidancePriority = 50;

    [Tooltip("Radius in meters used by crowd/density systems to stamp personal space and compute separation.\nTypical: Crawlers 0.5–0.8, Drones 0.6–1.0, Boss 1.5–2.5.")]
    public float PersonalSpaceRadius = 0.6f;

    [Tooltip("Optional NavMesh Area names this enemy prefers (e.g., 'Walkable', 'LowCost', 'Catwalk').\nPlanner may bias paths through these areas when combined with PreferredAreaCostMultiplier.")]
    public string[] PreferredAreas;

    [Tooltip("Relative cost applied to PreferredAreas during planning. < 1.0 prefers, > 1.0 avoids.\nExamples: 0.8 to prefer lanes, 1.2 to slightly avoid.\nSet to 1.0 to disable bias.")]
    public float PreferredAreaCostMultiplier = 0.8f;

    [Tooltip("Hint that many agents are moving toward the same goal (player/interest). Enables planners that handle convergence efficiently (e.g., FlowField).")]
    public bool ManyToOne;

    [Tooltip("Hint to penalize high-density regions (uses DensityGrid sampling). Set planner density multipliers > 0 to see effect.\nGood for swarms avoiding traffic jams.")]
    public bool AvoidCrowds;

    [Tooltip("Scheduling importance for crowd updates (higher = tick more often). Used by CrowdController for cadence.\nSuggested: Boss 3, Elites 2, Regulars 1.")]
    [Range(0, 5)]
    public int Importance = 1;

    public PlannerHints ToPlannerHints()
    {
        PlannerHints h = PlannerHints.None;
        if (ManyToOne) h |= PlannerHints.ManyAgentsToSameGoal;
        if (AvoidCrowds) h |= PlannerHints.AvoidCrowds;
        return h;
    }
#pragma warning restore CS0414
}
