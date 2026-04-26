using System.Collections.Generic;
using UnityEngine;

public class DestroySelectSingletons : MonoBehaviour
{
    [SerializeField] private List<GameObject> singletonsToDestroy = new List<GameObject>();

    public void DestroySingletons()
    {
        HashSet<GameObject> seen = new HashSet<GameObject>();

        foreach (GameObject singleton in singletonsToDestroy)
        {
            if (singleton == null || !seen.Add(singleton))
            {
                continue;
            }

            Destroy(singleton);
        }
    }
}
