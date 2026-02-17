// NavMeshAStarPlanner.cs
// Purpose: Triangle-graph A* planner built from NavMesh triangulation. Extracts portals between triangles and runs funnel smoothing for any-angle paths.
// Works with: PathRequestManager, FlowFieldService, DensityGrid for density-aware costs.
// Notes: Has a fallback to NavMesh.CalculatePath and an expansion budget to avoid worst-case stalls.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public sealed class NavMeshAStarPlanner : IPathPlanner
{
 public bool SupportsDynamicUpdates => false;
 public bool SupportsManyToOne => false;
 public bool SupportsNavMesh => true;

 // If >0, multiply sampled density by this value and add to edge cost
 public float DensityCostMultiplier { get; set; } =0f;

 // Max triangle expansions before giving up and using NavMesh fallback
 public int MaxExpansions =20000;

 // Cached triangulation graph
 private Vector3[] _vertices;
 private int[] _indices;
 private int _triangleCount;
 private Vector3[] _centers;
 private List<int>[] _neighbors;
 private bool _graphBuilt = false;

 public NavMeshAStarPlanner()
 {
 TryBuildGraph();
 }

 private void TryBuildGraph()
 {
 try
 {
 var tri = NavMesh.CalculateTriangulation();
 _vertices = tri.vertices;
 _indices = tri.indices;
 _triangleCount = _indices.Length /3;
 _centers = new Vector3[_triangleCount];
 _neighbors = new List<int>[_triangleCount];
 for (int t =0; t < _triangleCount; t++)
 {
 int i0 = _indices[t *3 +0];
 int i1 = _indices[t *3 +1];
 int i2 = _indices[t *3 +2];
 _centers[t] = (_vertices[i0] + _vertices[i1] + _vertices[i2]) /3f;
 _neighbors[t] = new List<int>();
 }

 var edgeToTri = new Dictionary<Edge, int>(new EdgeComparer());
 for (int t =0; t < _triangleCount; t++)
 {
 int a = _indices[t *3 +0];
 int b = _indices[t *3 +1];
 int c = _indices[t *3 +2];
 var edges = new Edge[3] { new Edge(a, b), new Edge(b, c), new Edge(c, a) };
 foreach (var e in edges)
 {
 if (edgeToTri.TryGetValue(e, out int other))
 {
 _neighbors[t].Add(other);
 _neighbors[other].Add(t);
 }
 else edgeToTri[e] = t;
 }
 }

 _graphBuilt = true;
 }
 catch (Exception ex)
 {
 EnemyBehaviorDebugLogBools.LogWarning(nameof(NavMeshAStarPlanner), "NavMeshAStarPlanner: failed to build triangle graph: " + ex.Message);
 _graphBuilt = false;
 }
 }

 public PathTask RequestPath(PathQuery query)
 {
 var task = new PathTask();
 if (!_graphBuilt)
 {
 return FallbackCalculate(query);
 }

 // Sample positions onto navmesh
 if (!NavMesh.SamplePosition(query.Start, out var startHit,1.0f, NavMesh.AllAreas) ||
 !NavMesh.SamplePosition(query.Goal, out var goalHit,1.0f, NavMesh.AllAreas))
 {
 return FallbackCalculate(query);
 }

 int startTri = FindNearestTriangle(startHit.position);
 int goalTri = FindNearestTriangle(goalHit.position);
 if (startTri == -1 || goalTri == -1)
 return FallbackCalculate(query);

 // A* on triangle graph
 var cameFrom = new int[_triangleCount];
 var gScore = new float[_triangleCount];
 for (int i =0; i < _triangleCount; i++) { cameFrom[i] = -1; gScore[i] = float.PositiveInfinity; }

 var open = new BinaryHeap(Math.Max(256, _triangleCount /8));
 gScore[startTri] =0f;
 open.Push(startTri, Heuristic(startTri, goalTri));

 int expansions =0;
 bool found = false;
 var visited = new BitArray(_triangleCount);

 while (open.Count >0)
 {
 int current = open.Pop();
 if (visited[current]) continue; // skip duplicates
 visited[current] = true;

 if (current == goalTri) { found = true; break; }
 expansions++;
 if (expansions > MaxExpansions)
 {
 EnemyBehaviorDebugLogBools.LogWarning(nameof(NavMeshAStarPlanner), $"NavMeshAStarPlanner: expansion budget exceeded ({MaxExpansions}), falling back to NavMesh.CalculatePath");
 return FallbackCalculate(query);
 }

 foreach (var nb in _neighbors[current])
 {
 if (visited[nb]) continue;
 float moveCost = Vector3.Distance(_centers[current], _centers[nb]);
 // sample density at edge midpoint if enabled
 if (DensityCostMultiplier >0f && EnemyBehavior.Density.DensityGrid.Instance != null)
 {
 var edgeMid = (_centers[current] + _centers[nb]) *0.5f;
 float density = EnemyBehavior.Density.DensityGrid.Instance.SampleCost(edgeMid);
 moveCost += DensityCostMultiplier * density;
 }
 float tentative = gScore[current] + moveCost;
 if (tentative < gScore[nb])
 {
 gScore[nb] = tentative;
 cameFrom[nb] = current;
 float f = tentative + Heuristic(nb, goalTri);
 open.Push(nb, f);
 }
 }
 }

 if (!found)
 return FallbackCalculate(query);

 // reconstruct triangle path
 var triPath = new List<int>();
 int cur = goalTri;
 while (cur != -1)
 {
 triPath.Add(cur);
 cur = cameFrom[cur];
 }
 triPath.Reverse();

 // extract portals between consecutive triangles
 var portals = new List<Tuple<Vector3, Vector3>>();
 for (int i =0; i < triPath.Count -1; i++)
 {
 int a = triPath[i];
 int b = triPath[i +1];
 if (TryGetSharedEdge(a, b, out Vector3 p0, out Vector3 p1))
 {
 portals.Add(Tuple.Create(p0, p1));
 }
 else
 {
 // fallback use centers as degenerate portal
 portals.Add(Tuple.Create(_centers[a], _centers[b]));
 }
 }

 var corners = RunFunnel(startHit.position, goalHit.position, portals);
 task.Corners = corners.ToArray();
 task.IsCompleted = true;
 task.Succeeded = true;
 return task;
 }

 private PathTask FallbackCalculate(PathQuery q)
 {
 var task = new PathTask();
 var path = new NavMeshPath();
 int mask = q.AreaMask == -1 ? NavMesh.AllAreas : q.AreaMask;
 NavMesh.CalculatePath(q.Start, q.Goal, mask, path);
 task.Corners = path.corners;
 task.IsCompleted = true;
 task.Succeeded = path.status == NavMeshPathStatus.PathComplete;
 return task;
 }

 private int FindNearestTriangle(Vector3 pos)
 {
 int best = -1;
 float bestDist = float.MaxValue;
 for (int i =0; i < _triangleCount; i++)
 {
 float d = (pos - _centers[i]).sqrMagnitude;
 if (d < bestDist) { bestDist = d; best = i; }
 }
 return best;
 }

 private float Heuristic(int tri, int goalTri)
 {
 return Vector3.Distance(_centers[tri], _centers[goalTri]);
 }

 private bool TryGetSharedEdge(int ta, int tb, out Vector3 p0, out Vector3 p1)
 {
 p0 = p1 = Vector3.zero;
 int a0 = _indices[ta *3 +0];
 int a1 = _indices[ta *3 +1];
 int a2 = _indices[ta *3 +2];
 int[] asIdx = new[] { a0, a1, a2 };
 int b0 = _indices[tb *3 +0];
 int b1 = _indices[tb *3 +1];
 int b2 = _indices[tb *3 +2];
 int[] bsIdx = new[] { b0, b1, b2 };
 var shared = new List<int>();
 foreach (var ai in asIdx)
 foreach (var bi in bsIdx)
 if (ai == bi) shared.Add(ai);
 if (shared.Count ==2)
 {
 p0 = _vertices[shared[0]];
 p1 = _vertices[shared[1]];
 return true;
 }
 return false;
 }

 private List<Vector3> RunFunnel(Vector3 start, Vector3 goal, List<Tuple<Vector3, Vector3>> portals)
 {
 var pts = new List<Vector3>();
 if (portals == null || portals.Count ==0)
 {
 pts.Add(start);
 pts.Add(goal);
 return pts;
 }

 Vector3 portalApex = start;
 Vector3 portalLeft = portals[0].Item1;
 Vector3 portalRight = portals[0].Item2;
 int apexIndex =0, leftIndex =0, rightIndex =0;
 pts.Add(portalApex);

 for (int i =0; i < portals.Count; i++)
 {
 Vector3 left = portals[i].Item1;
 Vector3 right = portals[i].Item2;
 // ensure ordering
 if (Vector3.SignedAngle((left - portalApex), (right - portalApex), Vector3.up) <0)
 {
 var tmp = left; left = right; right = tmp;
 }

 // Update right
 if (TriangleArea2(portalApex, portalRight, right) <=0f)
 {
 if (Vector3.Equals(portalApex, portalRight) || TriangleArea2(portalApex, portalLeft, right) >0f)
 {
 portalRight = right; rightIndex = i;
 }
 else
 {
 portalApex = portalLeft;
 apexIndex = leftIndex;
 pts.Add(portalApex);
 portalLeft = portalApex; portalRight = portalApex;
 leftIndex = apexIndex; rightIndex = apexIndex;
 i = apexIndex;
 continue;
 }
 }

 // Update left
 if (TriangleArea2(portalApex, portalLeft, left) >=0f)
 {
 if (Vector3.Equals(portalApex, portalLeft) || TriangleArea2(portalApex, portalRight, left) <0f)
 {
 portalLeft = left; leftIndex = i;
 }
 else
 {
 portalApex = portalRight;
 apexIndex = rightIndex;
 pts.Add(portalApex);
 portalLeft = portalApex; portalRight = portalApex;
 leftIndex = apexIndex; rightIndex = apexIndex;
 i = apexIndex;
 continue;
 }
 }
 }

 pts.Add(goal);
 return pts;
 }

 private static float TriangleArea2(Vector3 a, Vector3 b, Vector3 c)
 {
 return (b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z);
 }

 // Implement IPathPlanner.Update(float) to satisfy interface; do a lazy graph rebuild if needed.
 public void Update(float dt)
 {
 if (!_graphBuilt)
 {
 TryBuildGraph();
 }
 }

 // Supporting types
 private struct Edge { public int a, b; public Edge(int a, int b) { if (a < b) { this.a = a; this.b = b; } else { this.a = b; this.b = a; } } }
 private class EdgeComparer : IEqualityComparer<Edge> { public bool Equals(Edge x, Edge y) => x.a == y.a && x.b == y.b; public int GetHashCode(Edge obj) => obj.a *73856093 ^ obj.b *19349663; }

 // Compact bit array
 private class BitArray
 {
 private readonly int[] _data;
 public BitArray(int size) { _data = new int[(size +31) >>5]; }
 public bool this[int i]
 {
 get { return (_data[i >>5] & (1 << (i &31))) !=0; }
 set { if (value) _data[i >>5] |= (1 << (i &31)); else _data[i >>5] &= ~(1 << (i &31)); }
 }
 }

 // Array-backed binary heap (min-heap) for ints with float keys
 private class BinaryHeap
 {
 private int[] items;
 private float[] scores;
 private int count;
 public BinaryHeap(int capacity) { items = new int[Math.Max(8, capacity)]; scores = new float[items.Length]; count =0; }
 public int Count => count;
 public void Push(int item, float score)
 {
 if (count >= items.Length)
 {
 int nc = Math.Max(items.Length *2,8);
 Array.Resize(ref items, nc);
 Array.Resize(ref scores, nc);
 }
 int i = count++; items[i] = item; scores[i] = score;
 while (i >0)
 {
 int p = (i -1) >>1;
 if (scores[p] <= scores[i]) break;
 Swap(i, p); i = p;
 }
 }
 public int Pop()
 {
 int r = items[0]; count--;
 if (count >0)
 {
 items[0] = items[count]; scores[0] = scores[count]; SiftDown(0);
 }
 return r;
 }
 private void SiftDown(int i)
 {
 while (true)
 {
 int l = (i <<1) +1; if (l >= count) break; int r = l +1; int s = l; if (r < count && scores[r] < scores[l]) s = r; if (scores[i] <= scores[s]) break; Swap(i, s); i = s;
 }
 }
 private void Swap(int a, int b) { int t = items[a]; items[a] = items[b]; items[b] = t; float f = scores[a]; scores[a] = scores[b]; scores[b] = f; }
 }
}
