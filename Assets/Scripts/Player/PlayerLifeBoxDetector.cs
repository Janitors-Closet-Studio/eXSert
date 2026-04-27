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
    private CharacterController characterController;

    private void Awake()
    {
        healthBarManager = GetComponent<PlayerHealthBarManager>();
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        StartCoroutine(CheckIfInLifeBox());
    }

    // Continuously check if the player is inside any life boxes   
    private IEnumerator CheckIfInLifeBox()
    {
        while(true)
        {
            if (!IsInsideAnyLifeBox())
            {
                if (TryKillPlayer())
                    yield break; // Exit only after we successfully initiated death.
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

    private bool IsInsideAnyLifeBox()
    {
        // Remove stale tracked entries (destroyed/disabled or no longer containing the player).
        lifeBoxes.RemoveAll(box => box == null || !box.gameObject.activeInHierarchy || !IsPlayerInsideLifeBox(box));

        if (lifeBoxes.Count > 0)
            return true;

        // Fallback for builds where trigger enter/exit timing can be inconsistent.
        LifeBox[] allLifeBoxes = FindObjectsByType<LifeBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (allLifeBoxes == null || allLifeBoxes.Length == 0)
            return false;

        for (int i = 0; i < allLifeBoxes.Length; i++)
        {
            LifeBox candidate = allLifeBoxes[i];
            if (candidate == null || !IsPlayerInsideLifeBox(candidate))
                continue;

            lifeBoxes.Add(candidate);
            return true;
        }

        return false;
    }

    private bool IsPlayerInsideLifeBox(LifeBox box)
    {
        if (box == null)
            return false;

        Collider boxCollider = box.GetComponent<Collider>();
        if (boxCollider == null || !boxCollider.enabled)
            return false;

        Vector3 probePoint = transform.position;
        if (characterController != null)
            probePoint = transform.TransformPoint(characterController.center);

        return boxCollider.bounds.Contains(probePoint);
    }

    private bool TryKillPlayer()
    {
        if (PlayerMovement.IsTestingOrDebugMode)
        {
            Debug.Log("[PlayerLifeBoxDetector] Testing/Debug mode enabled on PlayerMovement. Skipping out-of-lifebox death handling.");
            return false;
        }

        if (!killPlayerWhenOutOfLifeBox)
            return false;

        Debug.Log("Player is out of bounds of life boxes! Killing player");

        if (healthBarManager == null)
        {
            healthBarManager = GetComponent<PlayerHealthBarManager>();
            if (healthBarManager == null)
                return false;
        }

        healthBarManager.HandleDeath(false);
        return true;
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
            if (!IsInsideAnyLifeBox())
            {
                TryKillPlayer();
            }
        }
    }
}
