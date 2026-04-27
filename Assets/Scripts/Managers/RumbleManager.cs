/*
    This manager controls the rumble functionality for only gamepad

    written by Brandon Wahl
*/
using UnityEngine;
using Singletons;
using UnityEngine.InputSystem;
using System.Collections;

public class RumbleManager : Singleton<RumbleManager>
{

    //current gamepad
    private Gamepad pad;
    protected override void Awake()
    {

        base.Awake();
    }
    
    //lowfreq is the low frequency motor, highfreq is the high frequency motor, duration is how long the rumble should last
    public void RumblePulse(float lowFreq, float highFreq, float duration)
    {
        if (Time.timeScale == 0)
        {
            Debug.Log("Rumble skipped due to timeScale being 0");
            return;
        }

        if (PauseManager.IsPaused)
        {
            Debug.Log("Rumble skipped because the game is currently paused");
            return;
        }

        if (CutsceneManager.IsCutscenePlaying)
        {
            Debug.Log("Rumble skipped because a cutscene is currently playing");
            return;
        }

        //checks the current control scheme and if rumble is activated
        if (InputReader.PlayerInput.currentControlScheme == "Gamepad")
        {
            // Try to get the gamepad associated with the PlayerInput
            var playerInput = InputReader.PlayerInput;
            if (playerInput != null)
            {
                foreach (var device in playerInput.devices)
                {
                    if (device is Gamepad gamepad)
                    {
                        pad = gamepad;
                        break;
                    }
                }
            }
            // Fallback to Gamepad.current if not found
            if (pad == null)
            {
                pad = Gamepad.current;
            }

            //if pad is not null then the rumble is activated with the strength assigned in the settings menu
            if (pad != null)
            {
                pad.SetMotorSpeeds(lowFreq * SettingsManager.Instance.rumbleStrength, highFreq * SettingsManager.Instance.rumbleStrength);
                StartCoroutine(StopRumble(duration, pad));
                Debug.Log("Rumble Activated with low frequency: " + lowFreq + " and high frequency: " + highFreq + " for duration: " + duration);
            }
        }
    }

    public void StopControllerRumble()
    {
        StartCoroutine(StopRumble(0, pad));
    }

    private IEnumerator StopRumble(float duration, Gamepad pad)
    {
        float elapsedTime = 0f;

        //While the current time is lower than duration rumble will play
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        pad.SetMotorSpeeds(0, 0);

    }
}
