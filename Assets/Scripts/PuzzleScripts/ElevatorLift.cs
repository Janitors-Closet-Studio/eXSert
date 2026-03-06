using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public class ElevatorLift : MonoBehaviour
{
    // FixedUpdate movement reverted; coroutine will handle movement

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
    private GameObject playerReference;



    private int currentFloor = 0;
    private bool isMoving = false;

    private void SubscribeToInputActions()
    {
        backToGameplayAction.action.performed += ReturnToGameplayAction;
        firstFloorAction.action.performed += MoveToFirstFloor;
        secondFloorAction.action.performed += MoveToSecondFloor;
        thirdFloorAction.action.performed += MoveToThirdFloor;
    }

    private void UnsubscribeFromInputActions()
    {
        backToGameplayAction.action.performed -= ReturnToGameplayAction;
        firstFloorAction.action.performed -= MoveToFirstFloor;
        secondFloorAction.action.performed -= MoveToSecondFloor;
        thirdFloorAction.action.performed -= MoveToThirdFloor;
    }

    private void Start()
    {
        // Ensure all floor buttons are initially inactive
        foreach (var button in floorButtonUI)
        {
            if (button != null)
                button.SetActive(false);
        }
    }

    public void EnterElevatorLiftMenu()
    {
        SubscribeToInputActions();

        SwapActionMaps("ElevatorLift");

        InputReader.inputBusy = true;
    
        ManageElevatorButtons(currentFloor);
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
        Debug.Log("Returning to gameplay from elevator menu.");
        UnsubscribeFromInputActions();
        SwapActionMaps("Gameplay");
        InputReader.inputBusy = false;
        InputReader.Instance.SetAllActionsEnabled(true);
    }

    private void TurnOffAllButtons()
    {
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
    
    public void CallElevatorToFloorOne()
    {
        if(currentFloor == 0 || isMoving) return;
        StartCoroutine(MoveLift(0));
    }

    private CharacterController ReturnPlayerCC()
    {
        var playerCC = playerReference.GetComponent<CharacterController>();
        if (playerCC == null)
        {
            Debug.LogWarning("Player CharacterController not found. Make sure the player has a CharacterController component.");
        }
        return playerCC;
    }

    private IEnumerator MoveLift(int targetFloor)
    {
        TurnOffAllButtons();
        isMoving = true;
        Vector3 targetPosition = desiredLiftPosition[targetFloor];
        float moveSpeed = liftSpeed; // units per second
        ReturnPlayerCC().enabled = false; // Disable CharacterController to prevent physics issues during movement

        // Parent player to elevator for smooth movement
        Transform originalParent = null;
        if (playerReference != null)
        {
            originalParent = playerReference.transform.parent;
            playerReference.transform.SetParent(elevatorLift.transform);
        }

        while (Vector3.Distance(elevatorLift.transform.position, targetPosition) > 0.001f)
        {
            elevatorLift.transform.position = Vector3.MoveTowards(
                elevatorLift.transform.position, targetPosition, moveSpeed * Time.deltaTime);
            yield return null;
        }
        elevatorLift.transform.position = targetPosition;

        // Unparent player after ride
        if (playerReference != null)
            playerReference.transform.SetParent(originalParent);

        isMoving = false;
        currentFloor = targetFloor;
        ReturnPlayerCC().enabled = true; // Re-enable CharacterController after movement
        ReturnToGameplay();
    }

}
