using UnityEngine;
using UnityEngine.InputSystem;
using Singletons;

public class NavigationMenu : Singleton<NavigationMenu>
{
    [SerializeField] private InputActionReference _navigationMenu;
    [SerializeField] internal GameObject navigationMenuGO;

    [SerializeField] private GameObject IndividualLogGO;
    [SerializeField] private GameObject IndividualDiaryGO;

    public void Start()
    {
        base.Awake();
        IndividualDiaryGO.SetActive(true);
        IndividualLogGO.SetActive(true);
    }
}
