
// Utility script to force certain GameObjects to be turned on at the start of the scene. 

using System.Collections;
using UnityEngine;


public class ForceTurnOn : MonoBehaviour
{
    [SerializeField] private GameObject[] objectsToTurnOn;

    private void Start()
    {
        StartCoroutine(AttemptTurnOnUntilSuccess());
    }

    private IEnumerator AttemptTurnOnUntilSuccess()
    {
        while (true)
        {
            bool allObjectsTurnedOn = true;

            foreach (var obj in objectsToTurnOn)
            {
                if (obj != null && !obj.activeInHierarchy)
                {
                    obj.SetActive(true);
                    Debug.LogWarning($"ForceTurnOn: {obj.name} was turned on at the start of the scene.");
                }

                if (obj != null && !obj.activeInHierarchy)
                {
                    allObjectsTurnedOn = false;
                }
            }

            if (allObjectsTurnedOn)
                yield break;

            yield return new WaitForSeconds(0.5f); // Check every 0.5 seconds
        }
    }   
}
