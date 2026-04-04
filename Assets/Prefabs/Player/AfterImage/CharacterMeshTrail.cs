using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.WSA;

public class CharacterMeshTrail : MonoBehaviour
{
    public float ActiveTime = 2f;
    [Header("Mesh Related")]
    public float meshRefreshRate = 0.1f;
    public Transform positionToSpawn;
    [Header("Material")]
    public Material trailMaterial;
    private bool isTrailActive;
    private SkinnedMeshRenderer[] skinnedMeshRenderers;
    void Start()
    {
        
    }

    void Update()
    {
        if(Input.GetKeyDown (KeyCode.LeftShift) && !isTrailActive)
        {
            isTrailActive = true;
            StartCoroutine (ActivateTrail(ActiveTime));
        }
    
    }
    IEnumerator ActivateTrail (float timeActive)
    {
        while (timeActive > 0)
        {
            timeActive -= meshRefreshRate; 
            
            if(skinnedMeshRenderers == null)
                skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            
            for(int i=0; i<skinnedMeshRenderers.Length; i++)
            {
                GameObject gObj = new GameObject();
                gObj.transform.SetPositionAndRotation(positionToSpawn.position, positionToSpawn.rotation);

                MeshRenderer mr = gObj.AddComponent<MeshRenderer>();
                MeshFilter mf = gObj.AddComponent<MeshFilter>();

                Mesh mesh = new Mesh();
                skinnedMeshRenderers[i].BakeMesh(mesh);
                
                mf.mesh = mesh;
                mr.material = trailMaterial;

                Destroy(gObj, 0.5f);
            }

            yield return new WaitForSeconds (meshRefreshRate);
        }
        isTrailActive = false;
    }
}
