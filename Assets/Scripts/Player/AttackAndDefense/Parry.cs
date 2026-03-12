using System.Collections;
using UnityEngine;
using Utilities.Combat;
using UnityEngine.VFX;
using Managers.TimeLord;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Parry : MonoBehaviour
{
    [Header("Parry Effects")]
    [SerializeField] private AudioClip parrySoundEffect;
    [SerializeField] private VisualEffectAsset parryEffect;
    [SerializeField, Range(.5f, 2f)] private float parryEffectDuration = 1f;

    [Space(10)]
    [Header("Time Pause Settings")]
    [SerializeField] private bool pauseTimeOnParry = true;
    [SerializeField, Range(.01f, 1f)] private float parryPauseDuration = 0.05f;
    [SerializeField, Range(0f, 1f)] private float howSlowTimeScales = 0.5f;

    [Header("Animation")]
    [SerializeField] private PlayerAnimationController animationController;

    private void Awake()
    {
        if (animationController == null)
        {
            animationController = GetComponent<PlayerAnimationController>()
                ?? GetComponentInChildren<PlayerAnimationController>()
                ?? GetComponentInParent<PlayerAnimationController>();
        }
    }

    private void OnEnable()
    {
        CombatManager.OnSuccessfulParry += HandleSuccessfulParry;
    }

    private void OnDisable()
    {
        CombatManager.OnSuccessfulParry -= HandleSuccessfulParry;
    }

    private void HandleSuccessfulParry(BaseEnemy<EnemyState, EnemyTrigger> enemy)
    {
        if (animationController == null)
        {
            animationController = GetComponent<PlayerAnimationController>()
                ?? GetComponentInChildren<PlayerAnimationController>()
                ?? GetComponentInParent<PlayerAnimationController>();

            if (animationController == null)
            {
#if UNITY_2022_2_OR_NEWER
                animationController = FindAnyObjectByType<PlayerAnimationController>();
#else
                animationController = FindObjectOfType<PlayerAnimationController>();
#endif
            }
        }

        animationController?.PlayParryNonCancelable();

        if (parrySoundEffect != null)
            AudioSource.PlayClipAtPoint(parrySoundEffect, transform.position);

        if (parryEffect != null)
        {
            GameObject vfxInstance = new GameObject("ParryEffect");
            vfxInstance.transform.position = transform.position;

            VisualEffect visualEffect = vfxInstance.AddComponent<VisualEffect>();
            visualEffect.visualEffectAsset = parryEffect;
            visualEffect.Play();
            DestroyVFX(vfxInstance, parryEffectDuration);
        }

        if (pauseTimeOnParry)
        {
            StartCoroutine(PauseTimeOnParry(parryPauseDuration));
        }
    }

    private void DestroyVFX(GameObject vfxInstance, float delay)
    {
        Destroy(vfxInstance, delay);
    }

    private IEnumerator PauseTimeOnParry(float duration)
    {
        // Use PauseCoordinator rather than directly setting Time.timeScale.
        string token = PauseCoordinator.RequestTimeScale($"Parry_{GetInstanceID()}", howSlowTimeScales);

        // Wait in real time so effect length is independent of timescale.
        yield return new WaitForSecondsRealtime(duration);

        PauseCoordinator.ReleaseTimeScale(token);
    }
}
