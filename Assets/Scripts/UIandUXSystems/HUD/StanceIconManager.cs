/*
Written by Kyle Woo

Detects the player's current stance and toggles UI icons accordingly.
Shows PileDriver icon for Single Attack stance and AOE icon for Area of Effect stance.
Active icons fade/wave between two colors.
*/

using UnityEngine;
using UnityEngine.UI;

using Utilities.Combat.Attacks;

public class StanceIconManager : MonoBehaviour
{
    [Header("UI Icon References")]
    [SerializeField] private GameObject pileDriverIcon;
    [SerializeField] private GameObject aoeIcon;

    [Header("Combat State")]
    [SerializeField] private PlayerCombatIdleController combatIdleController;
    
    [Header("Color Animation")]
    [SerializeField] private Color activeColor1 = Color.white;
    [SerializeField] private Color activeColor2 = Color.grey;
    [SerializeField] private Color inactiveColor = Color.grey;

    [Header("Blink Settings")]
    [SerializeField, Range(0.05f, 1f)] private float blinkDuration = 0.2f;
    [SerializeField, Range(1f, 30f)] private float blinkSpeed = 16f;

    [Header("Visibility")]
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0f;
    [SerializeField, Range(0.5f, 20f)] private float visibilityFadeSpeed = 8f;

    private Image activeStance => lastAttackWasAoe ? aoeImage : pileDriverImage;
    private Image inactiveStance => lastAttackWasAoe ? pileDriverImage : aoeImage;

    // Component references for color animation
    private Image pileDriverImage;
    private Image aoeImage;
    private float colorTimer = 0f;
    private float blinkTimer = 0f;
    private bool lastAttackWasAoe = false;

    private void OnEnable()
    {
        PlayerAttackManager.OnAttack += HandleAttackEvent;
    }

    private void OnDisable()
    {
        PlayerAttackManager.OnAttack -= HandleAttackEvent;
    }

    void Start()
    {
        // Get Image components for color animation
        if (pileDriverIcon != null)
        {
            pileDriverImage = pileDriverIcon.GetComponent<Image>();
            if (pileDriverImage == null)
                Debug.LogWarning($"{gameObject.name}: PileDriver icon doesn't have an Image component!");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: PileDriver icon GameObject not assigned!");
        }

        if (aoeIcon != null)
        {
            aoeImage = aoeIcon.GetComponent<Image>();
            if (aoeImage == null)
                Debug.LogWarning($"{gameObject.name}: AOE icon doesn't have an Image component!");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: AOE icon GameObject not assigned!");
        }

        EnsureCombatIdleController();
        InitializeIconAlpha();
    }

    void Update()
    {
        EnsureCombatIdleController();
        AnimateActiveIcon();
    }
    
    private void AnimateActiveIcon()
    {
        bool inCombat = combatIdleController != null && combatIdleController.IsInCombat;
        UpdateIconVisibility(inCombat);

        if (!inCombat)
        {
            blinkTimer = 0f;
            colorTimer = 0f;
            return;
        }

        Image active = activeStance;
        Image inactive = inactiveStance;

        if (blinkTimer > 0f)
        {
            blinkTimer -= Time.deltaTime;
            colorTimer += Time.deltaTime * blinkSpeed;
        }
        else
        {
            colorTimer = 0f;
        }

        if (active != null)
        {
            float fadeValue = blinkTimer > 0f
                ? (Mathf.Sin(colorTimer) + 1f) / 2f
                : 0f;

            Color animatedColor = Color.Lerp(activeColor1, activeColor2, fadeValue);
            animatedColor.a = active.color.a;
            active.color = animatedColor;
        }

        if (inactive != null)
        {
            Color inactiveTint = inactiveColor;
            inactiveTint.a = inactive.color.a;
            inactive.color = inactiveTint;
        }
    }

    private void HandleAttackEvent(PlayerAttack attack)
    {
        if (attack == null)
            return;

        if (IsSingleAttack(attack.attackType))
            lastAttackWasAoe = false;
        else if (IsAoeAttack(attack.attackType))
            lastAttackWasAoe = true;

        blinkTimer = blinkDuration;
        colorTimer = 0f;
    }

    private bool IsSingleAttack(AttackType attackType)
    {
        return attackType == AttackType.LightSingle
            || attackType == AttackType.HeavySingle
            || attackType == AttackType.LightAerial
            || attackType == AttackType.HeavyAerial;
    }

    private bool IsAoeAttack(AttackType attackType)
    {
        return attackType == AttackType.LightAOE
            || attackType == AttackType.HeavyAOE;
    }

    private void UpdateIconVisibility(bool inCombat)
    {
        float targetActiveAlpha = inCombat ? visibleAlpha : 0f;
        float targetInactiveAlpha = inCombat ? inactiveAlpha : 0f;

        if (activeStance != null)
            activeStance.color = ApplyAlpha(activeStance.color, targetActiveAlpha);
        if (inactiveStance != null)
            inactiveStance.color = ApplyAlpha(inactiveStance.color, targetInactiveAlpha);
    }

    private Color ApplyAlpha(Color color, float targetAlpha)
    {
        float nextAlpha = Mathf.MoveTowards(color.a, targetAlpha, Time.deltaTime * visibilityFadeSpeed);
        color.a = nextAlpha;
        return color;
    }

    private void InitializeIconAlpha()
    {
        if (pileDriverImage != null)
            pileDriverImage.color = SetAlphaImmediate(pileDriverImage.color, 0f);
        if (aoeImage != null)
            aoeImage.color = SetAlphaImmediate(aoeImage.color, 0f);
    }

    private Color SetAlphaImmediate(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void EnsureCombatIdleController()
    {
        if (combatIdleController != null)
            return;

        combatIdleController = FindFirstObjectByType<PlayerCombatIdleController>(FindObjectsInactive.Include);
    }
}