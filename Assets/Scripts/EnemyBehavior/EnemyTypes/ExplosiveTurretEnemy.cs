// ExplosiveTurretEnemy.cs
// Purpose: Turret enemy that fires explosive projectiles and manages firing behavior.
// Works with: ExplosiveEnemyProjectile, BaseTurretEnemy, Pathfinding optional.

using System.Collections;
using UnityEngine;
#pragma warning disable CS0414

// [RequireComponent(typeof(MeshRenderer))]
public class ExplosiveTurretEnemy : BaseTurretEnemy
{
	[Header("Animation")]
	[Tooltip("Optional secondary animator (box body) to drive alongside the main animator.")]
	[SerializeField] private Animator boxAnimator;
	[Tooltip("Animator state name used for the popup animation.")]
	[SerializeField] private string popupStateName = "Popup";
	[Tooltip("Popup animation clip used to determine how long to wait before attacking.")]
	[SerializeField] private AnimationClip popupAnimation;
	[Tooltip("Fallback duration if popup animation clip is not assigned.")]
	[SerializeField] private float popupFallbackDuration = 0.75f;

	private bool popupStarted;
	private bool popupComplete;
	private Coroutine popupRoutine;
	private bool playerEnteredDetection;

	public override bool TryFireTriggerByName(string triggerName)
	{
		if (!popupComplete && (triggerName == "SeePlayer" || triggerName == "InAttackRange"))
		{
			return false;
		}
#pragma warning restore CS0414

		return base.TryFireTriggerByName(triggerName);
	}

	protected override void PlayIdleAnim()
	{
		if (!popupComplete)
			return;

		base.PlayIdleAnim();
		PlayIdleAnimOn(boxAnimator);
	}

	protected override void PlayAttackAnim()
	{
		base.PlayAttackAnim();
		PlayAttackAnimOn(boxAnimator);
	}

	protected override void PlayHitAnim()
	{
		base.PlayHitAnim();
		PlayHitAnimOn(boxAnimator);
	}

	protected override void PlayDieAnim()
	{
		base.PlayDieAnim();
		PlayDieAnimOn(boxAnimator);
	}

	protected override void OnTriggerEnter(Collider other)
	{
		if (other != null && other.CompareTag("Player"))
		{
			playerEnteredDetection = true;
			StartPopupIfNeeded();
		}

		base.OnTriggerEnter(other);
	}

	protected void OnTriggerExit(Collider other)
	{
		if (other != null && other.CompareTag("Player"))
		{
			playerEnteredDetection = false;
			if (popupComplete)
				PlayIdleAnim();
		}
	}

	private void StartPopupIfNeeded()
	{
		if (popupStarted)
			return;

		popupStarted = true;
		PlayPopupAnim();

		if (popupRoutine != null)
			StopCoroutine(popupRoutine);

		popupRoutine = StartCoroutine(PopupRoutine());
	}

	private IEnumerator PopupRoutine()
	{
		float duration = popupAnimation != null ? popupAnimation.length : Mathf.Max(0f, popupFallbackDuration);
		if (duration > 0f)
			yield return WaitForSecondsCache.Get(duration);

		popupComplete = true;

		if (player != null)
		{
			float sqrDist = (player.position - transform.position).sqrMagnitude;
			float range = GetEffectiveDetectionRange();
			if (sqrDist <= range * range)
				base.TryFireTriggerByName("SeePlayer");
		}

		popupRoutine = null;
	}

	private void PlayPopupAnim()
	{
		if (!string.IsNullOrEmpty(popupStateName))
		{
			animator?.Play(popupStateName, 0, 0f);
			boxAnimator?.Play(popupStateName, 0, 0f);
		}
	}
}