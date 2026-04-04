using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshTrail : MonoBehaviour
{
    public float activeTime = 2f;
    [Header("Mesh Related")]
    public float meshRefreshRate = 0.1f;
    public float meshDestroyDelay = 3f;
    public Transform positionToSpawnMesh;
    public SkinnedMeshRenderer[] sourceRenderers;
    public float spawnedMeshScaleDivisor = 25f;
    [Header("Shader Related")]
    public Material mat;
    public string shaderVarRef;
    public float shaderVarRate = 0.1f;
    public float shaderVarRefreshRate = 0.05f;
    private bool isTrailActive;
    private SkinnedMeshRenderer[] skinnedMeshRenderers;
    private Coroutine trailCoroutine;
    private Coroutine timedTrailCoroutine;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (timedTrailCoroutine != null)
            {
                StopCoroutine(timedTrailCoroutine);
            }

            timedTrailCoroutine = StartCoroutine(ActivateTrailForDuration(activeTime));
        }
    }

    [ContextMenu("Turn Trail On")]
    public void TurnTrailOn()
    {
        if (isTrailActive)
        {
            return;
        }

        isTrailActive = true;
        trailCoroutine = StartCoroutine(ActivateTrail());
    }

    [ContextMenu("Turn Trail Off")]
    public void TurnTrailOff()
    {
        isTrailActive = false;

        if (timedTrailCoroutine != null)
        {
            StopCoroutine(timedTrailCoroutine);
            timedTrailCoroutine = null;
        }

        if (trailCoroutine != null)
        {
            StopCoroutine(trailCoroutine);
            trailCoroutine = null;
        }
    }

    [ContextMenu("Collect Source Renderers")]
    public void CollectSourceRenderers()
    {
        sourceRenderers = GetComponentsInChildren<SkinnedMeshRenderer>()
            .Where(renderer => renderer != null)
            .ToArray();
    }

    IEnumerator ActivateTrailForDuration(float timeActive)
    {
        TurnTrailOn();
        yield return new WaitForSeconds(timeActive);

        timedTrailCoroutine = null;
        TurnTrailOff();
    }

    IEnumerator ActivateTrail()
    {
        CacheSourceRenderers();

        while (isTrailActive)
        {
            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = skinnedMeshRenderers[i];
                if (skinnedMeshRenderer == null)
                {
                    continue;
                }

                Mesh mesh = new Mesh();
                skinnedMeshRenderer.BakeMesh(mesh);

                GameObject gObj = new GameObject();
                gObj.transform.SetPositionAndRotation(skinnedMeshRenderer.transform.position, skinnedMeshRenderer.transform.rotation);
                gObj.transform.localScale = GetSpawnScale(skinnedMeshRenderer.transform.lossyScale);

                MeshRenderer mr = gObj.AddComponent<MeshRenderer>();
                MeshFilter mf = gObj.AddComponent<MeshFilter>();

                mf.mesh = mesh;
                mr.material = mat;

                StartCoroutine(AnimateMaterialFloat(mr.material, 0, shaderVarRate, shaderVarRefreshRate));

                Destroy(gObj, meshDestroyDelay);
                Destroy(mesh, meshDestroyDelay);
            }

            yield return new WaitForSeconds(meshRefreshRate);
        }

        trailCoroutine = null;
    }

    void CacheSourceRenderers()
    {
        if (sourceRenderers != null && sourceRenderers.Length > 0)
        {
            skinnedMeshRenderers = sourceRenderers
                .Where(renderer => renderer != null)
                .ToArray();
        }
        else
        {
            skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }
    }

    Vector3 GetSpawnScale(Vector3 sourceScale)
    {
        if (Mathf.Approximately(spawnedMeshScaleDivisor, 0f))
        {
            return sourceScale;
        }

        return sourceScale / spawnedMeshScaleDivisor;
    }

    IEnumerator AnimateMaterialFloat(Material mat, float goal, float rate, float refreshRate)
    {
        float valueToAnimate = mat.GetFloat(shaderVarRef);

        while (valueToAnimate > goal)
        {
            valueToAnimate -= rate;
            mat.SetFloat(shaderVarRef, valueToAnimate);
            yield return new WaitForSeconds(refreshRate);
        }
    }
}
