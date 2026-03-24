/*
    Manages the settings that are only available in game; acts as a middle man between the main menu settings and the functionality

    written by Brandon Wahl
*/

using UnityEngine;
using Singletons;
using System.Collections.Generic;
using Unity.Cinemachine;

public class SettingsManager : Singleton<SettingsManager>
{
    [SerializeField] internal bool invertY;
    internal float sensitivity;
    [SerializeField] internal bool comboProgression;
    [SerializeField] internal float rumbleStrength;

    private GameObject player;
    private List<CinemachineInputAxisController> playerCameraController = new List<CinemachineInputAxisController>();
    private bool pendingCameraInputApply;

    [SerializeField] private float defaultSens = 1.5f;
    [SerializeField] private float defaultRumble = 0.5f;

    private void Start()
    {
        sensitivity = PlayerPrefs.GetFloat("masterSens", defaultSens);
        invertY = PlayerPrefs.GetInt("masterInvertY", 0) == 1;
        comboProgression = PlayerPrefs.GetInt("masterCombo", 1) == 1;
        rumbleStrength = PlayerPrefs.GetFloat("masterVibrateStrength", defaultRumble);
        pendingCameraInputApply = true;

        // Apply settings on start
        UpdatePlayerCameraSens(sensitivity);
        UpdatePlayerInvertY(invertY);
        UpdateComboProgressionDisplay(comboProgression);
    }

    private void LateUpdate()
    {
        if (!pendingCameraInputApply)
            return;

        FindPlayer();

        if (playerCameraController == null || playerCameraController.Count == 0)
            return;

        UpdatePlayerCameraSens(sensitivity);
        UpdatePlayerInvertY(invertY);
        pendingCameraInputApply = false;
    }

    internal void UpdatePlayerCameraSens(float newSensitivity)
    {
        FindPlayer();
        float ySign = invertY ? 1f : -1f;

        if(player == null)
        {
            Debug.LogWarning("Player not found. Cannot update camera sensitivity.");
            pendingCameraInputApply = true;
            return;
        }

        if (playerCameraController != null && playerCameraController.Count > 0)
        {
            Debug.Log("Updating player camera sensitivity to: " + newSensitivity);
            foreach (var axisController in playerCameraController)
            {
                if (axisController == null)
                    continue;

                foreach (var c in axisController.Controllers)
                {
                    if (c.Name == "Look Orbit X")
                    {
                        c.Input.Gain = newSensitivity;
                    }

                    if (c.Name == "Look Orbit Y")
                    {
                        c.Input.Gain = Mathf.Abs(newSensitivity) * ySign;
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("Player Camera Controller not found. Cannot update sensitivity.");
            pendingCameraInputApply = true;
        }

        sensitivity = newSensitivity;
    }

    internal void UpdateComboProgressionDisplay(bool isComboProgressionOn)
    {
        ComboProgressionUIController controller = FindComboProgressionUIController();
        if (controller != null)
        {
            controller.gameObject.SetActive(isComboProgressionOn); 
        }

        comboProgression = isComboProgressionOn;
    }

    private ComboProgressionUIController FindComboProgressionUIController()
    {
        ComboProgressionUIController controller = FindObjectOfType<ComboProgressionUIController>();
        if (controller == null)
        {
            Debug.LogWarning("No ComboProgressionUIController found in the scene.");
        }
        return controller;
    }

    internal void UpdatePlayerInvertY(bool newInvertY)
    {
        FindPlayer();
        float sensitivityMagnitude = Mathf.Abs(sensitivity);

        if (playerCameraController != null && playerCameraController.Count > 0)
        {
            foreach (var axisController in playerCameraController)
            {
                if (axisController == null)
                    continue;

                foreach (var c in axisController.Controllers)
                {
                    if (c.Name == "Look Orbit Y")
                    {
                        c.Input.Gain = sensitivityMagnitude * (newInvertY ? 1f : -1f);
                    }
                }
            }
        }
        else
        {
            pendingCameraInputApply = true;
        }

        invertY = newInvertY;
    }

    private GameObject FindPlayer()
    {
        if (player == null)
        {
            var taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                player = taggedPlayer.transform.root.gameObject;
        }

        if (player == null)
        {
            Debug.LogWarning("Player not found in the scene.");
            return null;
        }

        Debug.Log("Resolved player root: " + player.name);

        CameraManager cameraHolder = player.GetComponentInChildren<CameraManager>();

        if (cameraHolder == null){
            Debug.LogWarning("Camera holder not found on player. Cannot find camera controllers.");
            return player;
        }
        foreach (var c in cameraHolder.GetComponentsInChildren<CinemachineInputAxisController>())
        {
            if (!playerCameraController.Contains(c))
                playerCameraController.Add(c);
        }

        return player;
    }

}
