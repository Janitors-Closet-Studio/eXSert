using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public class ElevatorLift : PuzzleInteraction
{
    [SerializeField] private GameObject elevatorLift;

    [Tooltip("Assign the desired positions for the elevator lift to move to for each floor, in order from first to third floor")]
    [SerializeField] private Vector3[] desiredLiftPosition;
    [SerializeField] private float liftSpeed = 1f;

    [Tooltip("Assign the corresponding floor button UI elements in the inspector, in order from first to third floor; exit button should last.")]
    [SerializeField] private GameObject[] floorButtonUI;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference backToGameplayAction;
    [SerializeField] private InputActionReference firstFloorAction;
    [SerializeField] private InputActionReference secondFloorAction;
    [SerializeField] private InputActionReference thirdFloorAction;

    [Header("Death Collider")]
    [Tooltip("A collider below the elevator that will kill the player if they are below it when the elevator is in the death position. Make sure to set the position of this collider to match the death position calculated by the FindDeathPosition method.")]
    [SerializeField] private BoxCollider deathCollider;
    [SerializeField] private bool killPlayerOnContact = true;
    private Vector3 positionToKillPlayer;
    private GameObject playerReference;
    private PlayerHealthBarManager healthBarManager;


    private int currentFloor = 0;
    private bool isMoving = false;
    private void OnEnable()
    {
        backToGameplayAction.action.performed += ReturnToGameplayAction;
        firstFloorAction.action.performed += MoveToFirstFloor;
        secondFloorAction.action.performed += MoveToSecondFloor;
        thirdFloorAction.action.performed += MoveToThirdFloor;
    }

    private void OnDisable()
    {
        backToGameplayAction.action.performed -= ReturnToGameplayAction;
        firstFloorAction.action.performed -= MoveToFirstFloor;
        secondFloorAction.action.performed -= MoveToSecondFloor;
        thirdFloorAction.action.performed -= MoveToThirdFloor;
    }

    // Gets point to kill player
    private Vector3 FindDeathPosition()
    {
        positionToKillPlayer = desiredLiftPosition[0] + new Vector3(0, FindPlayerHeight(), 0);

        return positionToKillPlayer;
    }

    private float FindPlayerHeight()
    {
        var playerRenderer = playerReference.GetComponentInChildren<Renderer>();
        if (playerRenderer != null)
        {
            return playerRenderer.bounds.size.y;
        }
        else
        {
            Debug.LogWarning("Player Renderer not found, size couldnt be calculated. Make sure the player has a Renderer component in its children.");
            return 0f;
        }
    }

    private void FindPlayerReference()
    {
     if (playerReference == null)
            playerReference = GameObject.FindGameObjectWithTag("Player");
    }

    private void Start()
    {
        // Ensure all floor buttons are initially inactive
        foreach (var button in floorButtonUI)
        {
            if (button != null)
                button.SetActive(false);
        }

        FindPlayerReference();
    }

    public void EnterElevatorLiftMenu()
    {
        SwapActionMaps("ElevatorLift");

        ParentPlayerToLift(true);
    
        ManageElevatorButtons(currentFloor);
    }

    private void ParentPlayerToLift(bool shouldParent)
    {
        if (playerReference != null)
        {
            playerReference.transform.SetParent(shouldParent ? elevatorLift.transform : null);
        }
    }

    private void ManageElevatorButtons(int currentFloor)
    {
        foreach (var button in floorButtonUI)
        {
            if (button != floorButtonUI[currentFloor] && button != null)
                button.SetActive(true);
            else
            {
                if (button != null)
                    button.SetActive(false);
            }
        }
    }

    private void ReturnToGameplayAction(InputAction.CallbackContext context)
    {
        ReturnToGameplay();
    }

    private void ReturnToGameplay()
    {
        SwapActionMaps("Gameplay");
        ParentPlayerToLift(false);
        // Deactivate floor button UI
        foreach (var button in floorButtonUI)
        {
            if (button != null)
                button.SetActive(false);
        }
    }

    private void MoveToFirstFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 0 || isMoving) return;
        
        StartCoroutine(MoveLift(0));
    }

    private void MoveToSecondFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 1 || isMoving) return;

        StartCoroutine(MoveLift(1));
    }

    private void MoveToThirdFloor(InputAction.CallbackContext context)
    {
        if(currentFloor == 2 || isMoving) return;

        StartCoroutine(MoveLift(2));
    }

    private void SwapActionMaps(string actionMapName)
    {
        InputReader.PlayerInput.SwitchCurrentActionMap(actionMapName);
    }

    private IEnumerator MoveLift(int targetFloor)
    {
        ReturnToGameplay();
        isMoving = true;
        Vector3 startPosition = elevatorLift.transform.position;
        Vector3 targetPosition = desiredLiftPosition[targetFloor];
        float elapsedTime = 0f;
        float distance = Vector3.Distance(startPosition, targetPosition);
        while (elapsedTime < 1f)
        {
            elapsedTime += Time.deltaTime * liftSpeed;
            elevatorLift.transform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime);
            yield return null;
        }
        isMoving = false;
        currentFloor = targetFloor;
    }

    private void TryKillPlayer()
    {
        if (healthBarManager == null)
        {
            healthBarManager = playerReference.GetComponent<PlayerHealthBarManager>();
        }

        healthBarManager.HandleDeath(true);
    }


    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);

        if (killPlayerOnContact && playerReference != null)
        {
            if (other.gameObject == playerReference && deathCollider.transform.position == FindDeathPosition())
            {
                TryKillPlayer();
            }
        }
    }
}
