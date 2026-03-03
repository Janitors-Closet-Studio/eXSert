using UnityEngine;
using UnityEngine.InputSystem;
using Singletons;

public class NavigationMenu : Singleton<NavigationMenu>
{
    [SerializeField] private InputActionReference _navigationMenu;
    [SerializeField] internal GameObject navigationMenuGO;
}
