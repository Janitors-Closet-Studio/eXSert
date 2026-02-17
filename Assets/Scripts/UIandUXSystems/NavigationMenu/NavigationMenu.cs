using UnityEngine;
using UnityEngine.InputSystem;
using Singletons;

public class NavigationMenu : Singleton<NavigationMenu>
{
    [SerializeField] private InputActionReference _navigationMenu;
    [SerializeField] internal GameObject navigationMenuGO;

    void FixedUpdate()
    {
        if (_navigationMenu.action.triggered)
        {
            navigationMenuGO.SetActive(true);
            Debug.Log("Help");
        }
    }
}
