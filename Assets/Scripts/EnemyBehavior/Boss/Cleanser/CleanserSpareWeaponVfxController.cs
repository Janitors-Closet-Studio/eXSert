using System;
using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace EnemyBehavior.Boss.Cleanser
{
    public class CleanserSpareWeaponVfxController : MonoBehaviour
    {
        public GameObject controlledVfxRoot;

        private ParticleSystem[] controlledParticles = Array.Empty<ParticleSystem>();
        private VisualEffect[] controlledVisualEffects = Array.Empty<VisualEffect>();
        private Coroutine replayParticlesRoutine;
        private bool controlledStateActive;

        private void Awake()
        {
            CacheEffects();
            SetControlledState(false);
        }

        public void SetControlledState(bool isControlled)
        {
            controlledStateActive = isControlled;

            GameObject root = controlledVfxRoot != null ? controlledVfxRoot : gameObject;
            if (root == null)
                return;

            if (isControlled)
            {
                if (!root.activeSelf)
                    root.SetActive(true);

                if (replayParticlesRoutine != null)
                {
                    StopCoroutine(replayParticlesRoutine);
                    replayParticlesRoutine = null;
                }

                for (int index = 0; index < controlledParticles.Length; index++)
                {
                    ParticleSystem particle = controlledParticles[index];
                    if (particle == null)
                        continue;

                    if (!particle.gameObject.activeSelf)
                        particle.gameObject.SetActive(true);

                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Clear(true);
                    particle.Simulate(0f, true, true, true);
                    particle.Play(true);
                }

                if (controlledParticles.Length > 0)
                    replayParticlesRoutine = StartCoroutine(ReplayParticlesNextFrame());

                for (int index = 0; index < controlledVisualEffects.Length; index++)
                {
                    VisualEffect visualEffect = controlledVisualEffects[index];
                    if (visualEffect == null)
                        continue;

                    if (!visualEffect.gameObject.activeSelf)
                        visualEffect.gameObject.SetActive(true);

                    visualEffect.Reinit();
                    visualEffect.Play();
                }

                return;
            }

            if (replayParticlesRoutine != null)
            {
                StopCoroutine(replayParticlesRoutine);
                replayParticlesRoutine = null;
            }

            for (int index = 0; index < controlledParticles.Length; index++)
            {
                ParticleSystem particle = controlledParticles[index];
                if (particle == null)
                    continue;

                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
            }

            for (int index = 0; index < controlledVisualEffects.Length; index++)
            {
                VisualEffect visualEffect = controlledVisualEffects[index];
                if (visualEffect == null)
                    continue;

                visualEffect.Stop();
                visualEffect.Reinit();
            }

            if (controlledVfxRoot != null)
                controlledVfxRoot.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!controlledStateActive)
                return;

            for (int index = 0; index < controlledParticles.Length; index++)
            {
                ParticleSystem particle = controlledParticles[index];
                if (particle == null || !particle.gameObject.activeInHierarchy)
                    continue;

                if (!particle.isPlaying && !particle.IsAlive(true))
                    particle.Play(true);
            }

            for (int index = 0; index < controlledVisualEffects.Length; index++)
            {
                VisualEffect visualEffect = controlledVisualEffects[index];
                if (visualEffect == null || !visualEffect.gameObject.activeInHierarchy)
                    continue;

                if (!visualEffect.HasAnySystemAwake())
                    visualEffect.Play();
            }
        }

        private void CacheEffects()
        {
            GameObject root = controlledVfxRoot != null ? controlledVfxRoot : gameObject;
            if (root == null)
            {
                controlledParticles = Array.Empty<ParticleSystem>();
                controlledVisualEffects = Array.Empty<VisualEffect>();
                return;
            }

            controlledParticles =
                root.GetComponentsInChildren<ParticleSystem>(true) ?? Array.Empty<ParticleSystem>();
            controlledVisualEffects =
                root.GetComponentsInChildren<VisualEffect>(true) ?? Array.Empty<VisualEffect>();
        }

        private IEnumerator ReplayParticlesNextFrame()
        {
            yield return null;

            for (int index = 0; index < controlledParticles.Length; index++)
            {
                ParticleSystem particle = controlledParticles[index];
                if (particle == null || !particle.gameObject.activeInHierarchy)
                    continue;

                if (!particle.isPlaying)
                    particle.Play(true);
            }

            replayParticlesRoutine = null;
        }
    }
}
