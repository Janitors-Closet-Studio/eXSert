using UnityEngine;
using Singletons;
using UnityEngine.Video;
using System.Runtime.CompilerServices;
using System;

[RequireComponent(typeof(VideoPlayer))]
public class CutsceneManager : Singleton<CutsceneManager>
{
    [SerializeField]
    private GameObject videoScreenPrefab;
    private static GameObject videoScreenInstance;
    public override string ToString() => $"Cutscene Manager";

    private VideoPlayer _videoPlayer;
    public static VideoPlayer VideoPlayer
    {
        get
        {
            if (Instance._videoPlayer == null)
            {
                Instance._videoPlayer = Instance.GetComponent<VideoPlayer>();
                if (Instance._videoPlayer == null)
                {
                    Debug.LogError("CutsceneManager requires a VideoPlayer component.");
                }
            }

            return Instance._videoPlayer;
        }
    }

    protected override void Awake()
    {
        base.Awake(); // Ensure singleton initialization

        // Ensure the video screen prefab is instantiated and set up correctly
        if (videoScreenPrefab == null)
            videoScreenPrefab = Resources.Load<GameObject>("Cutscene Canvas");
        if (videoScreenPrefab == null)
        {
            Debug.LogError("CutsceneManager requires a reference to a video screen prefab.");
            return;
        }

        videoScreenInstance = Instantiate(videoScreenPrefab);
        videoScreenInstance.transform.SetParent(transform, false); // Parent to the CutsceneManager for organization
        videoScreenInstance.SetActive(false); // Start with the video screen hidden

        _videoPlayer = GetComponent<VideoPlayer>();
        SetupVideoPlayer();
    }

    private void SetupVideoPlayer()
    {
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.targetTexture = Resources.Load<RenderTexture>("Cutscene Texture");
        _videoPlayer.targetCameraAlpha = 1.0f;
    }

    private static void OnCutsceneStarted(VideoPlayer source)
    {
        Time.timeScale = 0f; // Pause the game while the cutscene plays
        InputReader.inputBusy = true; // Prevent player input during the cutscene
        videoScreenInstance.SetActive(true); // Show the video screen when the cutscene starts
        source.started -= OnCutsceneStarted; // Unsubscribe from the event to prevent multiple triggers
    }

    private static void OnCutsceneFinished(VideoPlayer source)
    {
        Time.timeScale = 1f; // Resume the game when the cutscene finishes
        InputReader.inputBusy = false; // Allow player input again
        videoScreenInstance.SetActive(false); // Hide the video screen when the cutscene finishes
        source.Stop();
        source.loopPointReached -= OnCutsceneFinished; // Unsubscribe from the event
    }

    public static void PlayCutscene(VideoClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("[Cutscene Player] Cannot play a null VideoClip.");
            return;
        }
        VideoPlayer.clip = clip;

        VideoPlayer.started +=  OnCutsceneStarted; // Manually trigger the start event to pause the game
        VideoPlayer.loopPointReached += OnCutsceneFinished; // Subscribe to the event to know when the cutscene finishes
        
        VideoPlayer.Play();
    }
}
