using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class SubobjectiveHandler : MonoBehaviour
{
    [Header("Subobjective UI Settings")]
    [SerializeField] private GameObject objectivePrefab;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color completedColor = Color.gray;

    private readonly List<GameObject> activeSubobjectives = new();
    private List<SubObjective> currentSubObjectives = new();
    private bool isSubscribed;

    private void OnEnable()
    {
        ObjectiveManager.OnSubObjectivesUpdated += UpdateUI;
        SubscribeToPlayerInput();
        InputSystem.onActionChange += HandleActionChange;
    }

    private void OnDisable()
    {
        ObjectiveManager.OnSubObjectivesUpdated -= UpdateUI;
        UnsubscribeFromPlayerInput();
        InputSystem.onActionChange -= HandleActionChange;
    }

    private void UpdateUI(List<SubObjective> subObjectives)
    {
        Debug.Log("[Subobjective Handler] Updating Subobjective UI with " + subObjectives.Count + " subobjectives.");
        currentSubObjectives = new List<SubObjective>(subObjectives);

        // Clear existing sub-objectives
        ClearSubobjectives();

        // Animate typing for each subobjective as it's added
        foreach (var subObj in subObjectives)
        {
            GameObject newSubobjective = Instantiate(objectivePrefab, transform);
            var textComponent = newSubobjective.GetComponentInChildren<TextMeshProUGUI>();
            textComponent.text = "";
            activeSubobjectives.Add(newSubobjective);
            // Animate typing in
            WritingTextUI.AddWriter_Static(textComponent, KeybindRichTextFormatter.Format(subObj.DisplayText), 0.025f, false, true);
            // Set color based on completion status
            textComponent.color = subObj.IsCompleted ? completedColor : activeColor;
        }
    }

    private void HandleActionChange(object obj, InputActionChange change)
    {
        if (change != InputActionChange.BoundControlsChanged || currentSubObjectives == null)
            return;

        UpdateUI(currentSubObjectives);
    }

    private void HandleControlsChanged(PlayerInput _)
    {
        if (currentSubObjectives == null)
            return;

        UpdateUI(currentSubObjectives);
    }

    private void SubscribeToPlayerInput()
    {
        if (isSubscribed || InputReader.PlayerInput == null)
            return;

        InputReader.PlayerInput.onControlsChanged += HandleControlsChanged;
        isSubscribed = true;
    }

    private void UnsubscribeFromPlayerInput()
    {
        if (!isSubscribed)
            return;

        if (InputReader.PlayerInput != null)
            InputReader.PlayerInput.onControlsChanged -= HandleControlsChanged;

        isSubscribed = false;
    }

    private void ClearSubobjectives()
    {
        activeSubobjectives.ForEach(Destroy);
        activeSubobjectives.Clear();
    }
}
