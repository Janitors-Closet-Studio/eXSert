using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SubobjectiveHandler : MonoBehaviour
{
    [SerializeField] private GameObject objectivePrefab;

    private List<GameObject> activeSubobjectives = new();

    public void AddSubobjective(string subobjectiveText)
    {
        GameObject newSubobjective = Instantiate(objectivePrefab, transform);
        
        newSubobjective.GetComponentInChildren<TextMeshProUGUI>().text = subobjectiveText;
    }
}
