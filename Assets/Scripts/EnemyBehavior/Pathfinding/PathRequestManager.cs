// PathRequestManager.cs
// Purpose: Central manager for path requests. Selects an appropriate planner and returns PathTask results synchronously.
// Works with: IPathPlanner implementations, CrowdAgent path requests and BaseEnemy.

using System.Collections.Generic;
using UnityEngine;
#pragma warning disable CS0414

namespace EnemyBehavior.Pathfinding
{
 public sealed class PathRequestManager : MonoBehaviour
 {
 public static PathRequestManager Instance { get; private set; }

 [Header("Component Help")]
 [SerializeField, TextArea(4, 8)] private string inspectorHelp =
     "PathRequestManager: chooses a planner (A*/FlowField/Dijkstra) per query and executes it synchronously.\n" +
     "Use Density multipliers to bias routes away from crowds.\n" +
     "Stats show planner counts and last timings for quick sanity checks.";

 [Header("Frame Budgets")]
 [SerializeField] int maxPlansPerFrame =8;
 [SerializeField] int maxReplansPerFrame =4;

 [Header("Density Cost Multipliers")]
 [Tooltip("Multiplier for density-cost added on NavMesh A* edges. 0 disables.")]
 [SerializeField, Range(0f, 5f)] private float aStarDensityCostMultiplier = 1.0f;
 [Tooltip("Multiplier for density-cost used by FlowField builder. 0 disables.")]
 [SerializeField, Range(0f, 5f)] private float flowFieldDensityCostMultiplier = 1.0f;

 [Header("Debug Stats (Read-only)")]
 [ReadOnly, SerializeField] private int lastAStarCount;
 [ReadOnly, SerializeField] private int lastDijkstraCount;
 [ReadOnly, SerializeField] private int lastFlowfieldCount;
 [ReadOnly, SerializeField] private float lastQueryMs;

 private readonly Queue<PathQuery> _queue = new Queue<PathQuery>(256);
 private readonly List<IPathPlanner> _allPlanners = new List<IPathPlanner>(8);
 private IPlannerSelector _selector;
 private WorldState _worldState = new WorldState();

 // Keep references so we can adjust knobs at runtime
 private NavMeshAStarPlanner _astar;
 private DijkstraPlanner _dijkstra;
 private FlowFieldService _flow;

 void Awake()
 {
 // Duplicate guard for additive scenes
 if (Instance != null && Instance != this)
 {
 EnemyBehaviorDebugLogBools.LogWarning(nameof(PathRequestManager), "PathRequestManager duplicate detected, destroying this instance (additive scene overlap).");
 Destroy(gameObject);
 return;
 }
#pragma warning restore CS0414
 Instance = this;
 // Register planners (simple defaults)
 _astar = new NavMeshAStarPlanner();
 _dijkstra = new DijkstraPlanner();
 _flow = new FlowFieldService();

 // Apply density multipliers
 if (_astar != null) _astar.DensityCostMultiplier = aStarDensityCostMultiplier;
 if (_flow != null) _flow.DensityCostMultiplier = flowFieldDensityCostMultiplier;

 _allPlanners.Add(_astar);
 _allPlanners.Add(_dijkstra);
 _allPlanners.Add(_flow);
 _selector = new PlannerSelector(_allPlanners);
 }

 void OnValidate()
 {
 // Keep planners in sync while tweaking in inspector (Play Mode only if objects exist)
 if (_astar != null) _astar.DensityCostMultiplier = aStarDensityCostMultiplier;
 if (_flow != null) _flow.DensityCostMultiplier = flowFieldDensityCostMultiplier;
 }

 public void SetDensityMultipliers(float aStar, float flow)
 {
 aStarDensityCostMultiplier = Mathf.Max(0f, aStar);
 flowFieldDensityCostMultiplier = Mathf.Max(0f, flow);
 if (_astar != null) _astar.DensityCostMultiplier = aStarDensityCostMultiplier;
 if (_flow != null) _flow.DensityCostMultiplier = flowFieldDensityCostMultiplier;
 }

 // Enqueue now performs an immediate synchronous path request and returns the PathTask.
 public PathTask Enqueue(PathQuery q)
 {
 if (_selector == null)
 {
 // ensure selector exists
 _selector = new PlannerSelector(_allPlanners);
 }
 var planner = _selector.Choose(q, _worldState);
 if (planner == null)
 {
 // fallback: direct NavMesh.CalculatePath
 var task = new PathTask();
 var path = new UnityEngine.AI.NavMeshPath();
 int mask = q.AreaMask == -1 ? UnityEngine.AI.NavMesh.AllAreas : q.AreaMask;
 var t0 = Time.realtimeSinceStartup;
 UnityEngine.AI.NavMesh.CalculatePath(q.Start, q.Goal, mask, path);
 lastQueryMs = (Time.realtimeSinceStartup - t0) * 1000f;
 task.Corners = path.corners;
 task.IsCompleted = true;
 task.Succeeded = path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
 // Stats: count as A* fallback
 lastAStarCount++;
 return task;
 }
 var t1 = Time.realtimeSinceStartup;
 var res = planner.RequestPath(q);
 lastQueryMs = (Time.realtimeSinceStartup - t1) * 1000f;
 if (planner is NavMeshAStarPlanner) lastAStarCount++;
 else if (planner is DijkstraPlanner) lastDijkstraCount++;
 else if (planner is FlowFieldService) lastFlowfieldCount++;
 return res;
 }

 void Update()
 {
 // keep planners updated for incremental planners
 float dt = Time.deltaTime;
 foreach (var p in _allPlanners)
 p.Update(dt);
 }
 }
}
