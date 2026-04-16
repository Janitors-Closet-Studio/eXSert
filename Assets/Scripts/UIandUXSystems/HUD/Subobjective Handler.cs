using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SubobjectiveHandler : MonoBehaviour
{
    [Header("Subobjective UI Settings")]
    [SerializeField] private GameObject objectivePrefab;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color completedColor = Color.gray;

    private readonly List<GameObject> activeSubobjectives = new();

    private void OnEnable()
    {
        ObjectiveManager.OnSubObjectivesUpdated += UpdateUI;
    }

    private void OnDisable()
    {
        ObjectiveManager.OnSubObjectivesUpdated -= UpdateUI;
    }

    private void UpdateUI(List<SubObjective> subObjectives)
    {
        Debug.Log("[Subobjective Handler] Updating Subobjective UI with " + subObjectives.Count + " subobjectives.");

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
            WritingTextUI.AddWriter_Static(textComponent, subObj.DisplayText, 0.025f, false, true);
            // Set color based on completion status
            textComponent.color = subObj.IsCompleted ? completedColor : activeColor;
        }
    }

    private void ClearSubobjectives()
    {
        activeSubobjectives.ForEach(Destroy);
        activeSubobjectives.Clear();
    }
}
