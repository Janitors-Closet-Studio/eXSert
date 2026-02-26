using UnityEngine;

/*
    Written by Brandon Wahl

    This script will handle the functionality of the act buttons in the navigation menu
    In the future, it will send players back to previous completed acts
*/

public class ActButton : MonoBehaviour
{
    [SerializeField] private int actNumber = 0; //0-4 

    [SerializeField] private GameObject sceneTriggerBox = null;

    public void OnActButtonClick()
    {
        bool matchKey = false;

        foreach(int i in ActsManager.Instance.actCompletionMap.Keys)
        {
            
            if(i == actNumber)
            {
                matchKey = true;
            }
        }

        if(matchKey)
        {
            bool isCompleted = ActsManager.Instance.actCompletionMap[actNumber];
            if(isCompleted)
                TeleportPlayerToAct();
        }
    }


    private void TeleportPlayerToAct()
    {
        if(sceneTriggerBox != null)
        {
            var player = GameObject.FindGameObjectWithTag("Player"); // Finds player
            if (player != null)
                player.transform.position = sceneTriggerBox.transform.position;
            
        }
    }
    
}
