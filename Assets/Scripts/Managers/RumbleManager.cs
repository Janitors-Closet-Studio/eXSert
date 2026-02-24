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

    //current control scheme
    private string currentControlScheme;
    protected override void Awake()
    {

        base.Awake();
    }

    void OnEnable()
    {
        //Subscribes onControlsChanged to SwitchControls function
        InputReader.PlayerInput.onControlsChanged += SwitchControls;
    }

    public void RumblePulse(float lowFreq, float highFreq, float duration)
    {
        //checks the current control scheme and if rumble is activated
        if (currentControlScheme == "Gamepad")
        {
            pad = Gamepad.current;

            //if pad is not null then the rumble is activated with the strength assigned in the settings menu
            if (pad != null)
            {
                pad.SetMotorSpeeds(lowFreq * SettingsManager.Instance.rumbleStrength, highFreq * SettingsManager.Instance.rumbleStrength);
                
                StartCoroutine(StopRumble(duration, pad));
            }
        }

    }
    
    private void SwitchControls(PlayerInput input)
    {
        //Gets current control scheme
        currentControlScheme = input.currentControlScheme;
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

    private void OnDisable()
    {
        //If the script is disabled then onControlsChanged is unsubscribed
        if(InputReader.PlayerInput != null)
            InputReader.PlayerInput.onControlsChanged -= SwitchControls;
    }
}
