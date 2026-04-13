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
        // Clear existing sub-objectives
        ClearSubobjectives();

        // Add new sub-objectives
        foreach (var subObj in subObjectives)
        {
            AddSubobjective(subObj.DisplayText);

            // Set color based on completion status
            var textComponent = activeSubobjectives[^1].GetComponentInChildren<TextMeshProUGUI>();
            textComponent.color = subObj.IsCompleted ? completedColor : activeColor;
        }
    }

    private void AddSubobjective(string subobjectiveText)
    {
        GameObject newSubobjective = Instantiate(objectivePrefab, transform);
        
        newSubobjective.GetComponentInChildren<TextMeshProUGUI>().text = subobjectiveText;
    }

    private void ClearSubobjectives()
    {
        activeSubobjectives.ForEach(Destroy);
        activeSubobjectives.Clear();
    }
}
