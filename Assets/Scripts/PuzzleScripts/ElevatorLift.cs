using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private int currentFloor = 0;
    private bool isMoving = false;

    private void OnEnable()
    {
        backToGameplayAction.action.performed += ReturnToGameplay;
        firstFloorAction.action.performed += MoveToFirstFloor;
        secondFloorAction.action.performed += MoveToSecondFloor;
        thirdFloorAction.action.performed += MoveToThirdFloor;
    }

    private void OnDisable()
    {
        backToGameplayAction.action.performed -= ReturnToGameplay;
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
        SwapActionMaps("ElevatorLift");
    
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

    private void ReturnToGameplay(InputAction.CallbackContext context)
    {
        SwapActionMaps("Gameplay");
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
}
