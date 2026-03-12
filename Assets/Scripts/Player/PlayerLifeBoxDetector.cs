using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerLifeBoxDetector : MonoBehaviour
{
    [Header("Debugging")]
    [SerializeField] private bool killPlayerWhenOutOfLifeBox = true;

    [Space(10)]

    [Header("Lifebox Settings")]
    [SerializeField] private float checkInterval = 0.5f;

    [SerializeField] private List<LifeBox> lifeBoxes = new List<LifeBox>();

    protected string lifeBoxTag = "LifeBox";

    private PlayerHealthBarManager healthBarManager;

    private void Start()
    {
        healthBarManager = GetComponent<PlayerHealthBarManager>();
        StartCoroutine(CheckIfInLifeBox());
    }

    // Continuously check if the player is inside any life boxes   
    private IEnumerator CheckIfInLifeBox()
    {
        while(true)
        {
            if (CheckIfLifeBoxesEmpty())
            {
                TryKillPlayer();
                yield break; // Exit the coroutine after death
            }
            yield return new WaitForSeconds(checkInterval); // Check every half second            
        }
    }

    private void RemoveLifeBox(LifeBox boxToRemove)
    {
        if(lifeBoxes.Contains(boxToRemove))
        {
            lifeBoxes.Remove(boxToRemove);
        }
    }

    private bool CheckIfLifeBoxesEmpty()
    {
        lifeBoxes.RemoveAll(box => box == null);
        return lifeBoxes.Count == 0;
    }

    private void TryKillPlayer()
    {
        if (!killPlayerWhenOutOfLifeBox) return;

        Debug.Log("Player is out of bounds of life boxes! Killing player");

        if (healthBarManager == null)
        {
            return;
        }

        healthBarManager.HandleDeath(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(lifeBoxTag))
        {
            if(!lifeBoxes.Contains(other.GetComponent<LifeBox>()))
                lifeBoxes.Add(other.GetComponent<LifeBox>());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(lifeBoxTag))
        { 
            RemoveLifeBox(other.GetComponent<LifeBox>());
            if (CheckIfLifeBoxesEmpty())
            {
                TryKillPlayer();
            }
        }
    }
}
