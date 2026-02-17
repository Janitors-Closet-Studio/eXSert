using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EnemyBehavior.Pathfinding;

public class AutomatedPathTestRunner : MonoBehaviour
{
 public float waitBeforeStart =1.0f;

 IEnumerator Start()
 {
 // small delay to allow systems to initialize
 yield return WaitForSecondsCache.Get(waitBeforeStart);
 yield return RunAllTests();
 }

 // Runtime initialize helper: if an editor trigger object was created, attach this runner automatically
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void AutoAttachIfRequested()
 {
 var trigger = GameObject.Find("AutoTestRunTrigger");
 if (trigger != null)
 {
 var runnerGO = new GameObject("AutoTestRunner");
 runnerGO.AddComponent<AutomatedPathTestRunner>();
 // remove trigger so it doesn't persist
 GameObject.Destroy(trigger);
 }
 }

 IEnumerator RunAllTests()
 {
 List<string> logs = new List<string>();
 int passed =0, failed =0;

 // Wait until PathRequestManager exists
 float timer =0f;
 while (PathRequestManager.Instance == null && timer <5f)
 {
 timer += Time.deltaTime;
 yield return null;
 }
 if (PathRequestManager.Instance == null)
 {
 EnemyBehaviorDebugLogBools.LogError("AutomatedPathTestRunner: PathRequestManager not found in scene.");
 yield break;
 }

 // Test1: Open area path
 bool r1 = TestOpenArea(out string l1);
 logs.Add(l1); if (r1) passed++; else failed++;
 yield return null;

 // Test2: Corridor test (if corridor exists)
 bool r2 = TestCorridor(out string l2);
 logs.Add(l2); if (r2) passed++; else failed++;
 yield return null;

 // Test2b: Elevated area test (ensure nav reaches elevated leg tops)
 bool r2b = TestElevatedArea(out string l2b);
 logs.Add(l2b); if (r2b) passed++; else failed++;
 yield return null;

 // Test3: Ramp test
 bool r3 = TestRamp(out string l3);
 logs.Add(l3); if (r3) passed++; else failed++;
 yield return null;

 // Test4: Platform gap test
 bool r4 = TestPlatformGap(out string l4);
 logs.Add(l4); if (r4) passed++; else failed++;
 yield return null;

 // Summary
 EnemyBehaviorDebugLogBools.Log(nameof(AutomatedPathTestRunner), $"AutomatedPathTestRunner: Tests completed. Passed {passed}, Failed {failed}.");
 foreach (var s in logs) EnemyBehaviorDebugLogBools.Log(nameof(AutomatedPathTestRunner), s);
 }

 bool TestOpenArea(out string log)
 {
 // pick two random points on the ground area
 var ground = GameObject.Find("Ground");
 if (ground == null)
 {
 log = "OpenArea: Ground not found";
 return false;
 }
 Vector3 center = ground.transform.position;
 Vector3 a = center + new Vector3(-5f,0f, -5f);
 Vector3 b = center + new Vector3(5f,0f,5f);
 if (!NavMesh.SamplePosition(a, out NavMeshHit ha,2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(b, out NavMeshHit hb,2f, NavMesh.AllAreas))
 {
 log = $"OpenArea: Could not sample navmesh at test points";
 return false;
 }
 var q = new PathQuery() { Start = ha.position, Goal = hb.position, AreaMask = -1 };
 var res = PathRequestManager.Instance.Enqueue(q);
 bool ok = res != null && res.Succeeded && res.Corners != null && res.Corners.Length >0;
 log = $"OpenArea: {(ok?"PASS":"FAIL")}. Path found={res?.Succeeded.ToString() ?? "null"}, corners={res?.Corners?.Length ??0}";
 return ok;
 }

 bool TestCorridor(out string log)
 {
 var wallA = GameObject.Find("Corridor_0_A");
 var wallB = GameObject.Find("Corridor_0_B");
 if (wallA == null || wallB == null)
 {
 log = "Corridor: corridor objects not found; skipping";
 return false;
 }
 Vector3 left = wallA.transform.position + new Vector3(0,0, -2f);
 Vector3 right = wallB.transform.position + new Vector3(0,0,2f);
 if (!NavMesh.SamplePosition(left, out NavMeshHit hl,2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(right, out NavMeshHit hr,2f, NavMesh.AllAreas))
 {
 log = "Corridor: could not sample navmesh near corridor sides";
 return false;
 }
 var q = new PathQuery() { Start = hl.position, Goal = hr.position, AreaMask = -1 };
 var res = PathRequestManager.Instance.Enqueue(q);
 bool ok = res != null && res.Succeeded;
 log = $"Corridor: {(ok?"PASS":"FAIL")}. Path found={res?.Succeeded.ToString() ?? "null"}, corners={res?.Corners?.Length ??0}";
 return ok;
 }

 bool TestElevatedArea(out string log)
 {
 var leg = GameObject.Find("Leg1");
 if (leg == null)
 {
 log = "Elevated: Leg1 not found";
 return false;
 }
 Vector3 sampleGround = leg.transform.position + new Vector3(-6f,0f,0f);
 Vector3 start;
 if (!NavMesh.SamplePosition(sampleGround, out NavMeshHit hs,2f, NavMesh.AllAreas))
 {
 log = "Elevated: could not sample ground start";
 return false;
 }
 Vector3 elevatedPoint = leg.transform.position + Vector3.up *0.5f;
 if (!NavMesh.SamplePosition(elevatedPoint, out NavMeshHit he,2f, NavMesh.AllAreas))
 {
 log = "Elevated: could not sample elevated area (navmesh missing)";
 return false;
 }
 var q = new PathQuery() { Start = hs.position, Goal = he.position, AreaMask = -1 };
 var res = PathRequestManager.Instance.Enqueue(q);
 bool ok = res != null && res.Succeeded;
 log = $"Elevated: {(ok?"PASS":"FAIL")}. Path found={res?.Succeeded.ToString() ?? "null"}, corners={res?.Corners?.Length ??0}";
 return ok;
 }

 bool TestRamp(out string log)
 {
 var ramp = GameObject.Find("RampToLeg1");
 var leg = GameObject.Find("Leg1");
 if (ramp == null || leg == null)
 {
 log = "Ramp: ramp or leg not found";
 return false;
 }
 Vector3 start = ramp.transform.position + ramp.transform.forward * -2f + Vector3.up *0.1f;
 Vector3 goal = leg.transform.position + Vector3.up *0.5f;
 if (!NavMesh.SamplePosition(start, out NavMeshHit hs,2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(goal, out NavMeshHit hg,2f, NavMesh.AllAreas))
 {
 log = "Ramp: could not sample navmesh on ramp/leg";
 return false;
 }
 var q = new PathQuery() { Start = hs.position, Goal = hg.position, AreaMask = -1 };
 var res = PathRequestManager.Instance.Enqueue(q);
 bool ok = res != null && res.Succeeded;
 log = $"Ramp: {(ok?"PASS":"FAIL")}. Path found={res?.Succeeded.ToString() ?? "null"}, corners={res?.Corners?.Length ??0}";
 return ok;
 }

 bool TestPlatformGap(out string log)
 {
 var pit = GameObject.Find("MovingPlatform_0_Pit");
 var platform = GameObject.Find("MovingPlatform_0");
 if (pit == null || platform == null)
 {
 log = "PlatformGap: required objects not found";
 return false;
 }
 // pick points on opposite sides of pit
 Vector3 left = pit.transform.position + new Vector3(-6f,2f,0);
 Vector3 right = pit.transform.position + new Vector3(6f,2f,0);
 if (!NavMesh.SamplePosition(left, out NavMeshHit hl,2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(right, out NavMeshHit hr,2f, NavMesh.AllAreas))
 {
 log = "PlatformGap: could not sample navmesh on either side of pit";
 return false;
 }
 var q = new PathQuery() { Start = hl.position, Goal = hr.position, AreaMask = -1 };
 var res = PathRequestManager.Instance.Enqueue(q);
 bool ok = res != null && res.Succeeded;
 log = $"PlatformGap: {(ok?"PASS":"FAIL")}. Path found={res?.Succeeded.ToString() ?? "null"}, corners={res?.Corners?.Length ??0}";
 return ok;
 }
}
