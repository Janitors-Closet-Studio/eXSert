using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
Written by Kyle Woo
Handles transferring between scenes when the player enters a trigger volume
*/

[RequireComponent(typeof(Collider))]
public class SceneTransferTrigger : MonoBehaviour
{
    [Header("Player filter")]
    [SerializeField] private string playerTag = "Player";

    [Header("Scenes")]
    [SerializeField] private string sceneToUnload = "DP_Elevator";
    [SerializeField] private string sceneToLoad   = "DP_Cargo";
    [SerializeField] private bool setLoadedSceneActive = true;

    private bool used;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;      // make sure this volume is a trigger
    }

    void OnTriggerEnter(Collider other)
    {
        if (used || !other.CompareTag(playerTag)) return;
        used = true;
        StartCoroutine(DoTransfer());
    }

    IEnumerator DoTransfer()
    {
        AsyncOperation loadOp   = null;
        AsyncOperation unloadOp = null;

        if (!string.IsNullOrEmpty(sceneToLoad) && !IsSceneLoaded(sceneToLoad))
            loadOp = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);

        if (!string.IsNullOrEmpty(sceneToUnload) && IsSceneLoaded(sceneToUnload))
            unloadOp = SceneManager.UnloadSceneAsync(sceneToUnload);

        if (loadOp != null)  yield return loadOp;

        if (setLoadedSceneActive && IsSceneLoaded(sceneToLoad))
        {
            var s = SceneManager.GetSceneByName(sceneToLoad);
            if (s.IsValid()) SceneManager.SetActiveScene(s);
        }

        if (unloadOp != null) yield return unloadOp;
    }

    static bool IsSceneLoaded(string name)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name == name && s.isLoaded) return true;
        }
        return false;
    }
}
