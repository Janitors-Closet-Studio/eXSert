using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Singletons;
public class InteractionUI : Singleton<InteractionUI>
{
    [Header("Global Interaction UI")]
    public TMP_Text _interactText;
    public Image _interactIcon;
    public TMP_Text _collectText;
    public TMP_Text _collectBottomText;

    protected override void Awake()
    {
        base.Awake();
    }

}
