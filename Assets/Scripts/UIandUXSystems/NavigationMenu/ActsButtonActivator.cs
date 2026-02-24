using UnityEngine;
using UnityEngine.UI;

public class ActsButtonActivator : MonoBehaviour
{
    [SerializeField] private Button[] actsButton;

    private void Awake()
    {
        for(int i = 0; i < actsButton.Length; i++)
        {
            if(ActsManager.Instance.actCompletionMap.TryGetValue(i, out bool isCompleted))
            {
                actsButton[i].interactable = isCompleted;
            }
            else
            {
                actsButton[i].interactable = false;
            }
        }
    }


}
