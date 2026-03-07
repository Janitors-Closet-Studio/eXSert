using UnityEngine;
using System.Collections.Generic;

public class UISceneManager : MonoBehaviour
{
    [Header("Menus")]
    public List<GameObject> Menus = new List<GameObject>();
    [SerializeField] private GameObject _initialMenu;

    private void Awake()
    {
        foreach (var menu in Menus)
        {
            menu.SetActive(false);
        }

        // Activate the initial menu
        if (_initialMenu != null && Menus.Contains(_initialMenu))
            _initialMenu.SetActive(true);


        // Fallbacks
        else
        {
            if (_initialMenu == null)
                Debug.LogWarning("Initial Menu is not set in the inspector. Assigning Initial Menu");
            else
                Debug.LogWarning("Initial Menu is not in the Menus list. Reassigning Initial Menu");

            if (Menus.Count > 0)
            {
                _initialMenu = Menus[0];
                _initialMenu.SetActive(true);
            }
            else
                Debug.LogError("No menus are set in the Menus list.");

        }
    }

    public void SwitchMenu(GameObject menuToSwitchTo)
    {
        if (menuToSwitchTo == null || !Menus.Contains(menuToSwitchTo))
        {
            Debug.LogError("Menu to switch to is null or doesn't exist in list, aborting function.");
            return;
        }

        foreach (var menu in Menus)
        {
            if (menu == menuToSwitchTo)
                menu.SetActive(true);
            else
                menu.SetActive(false);
        }
    }
}