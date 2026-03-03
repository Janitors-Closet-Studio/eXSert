/*
    Controls the settings that involve audio

    written by Brandon Wahl
*/

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class AudioSettings : MonoBehaviour
{

    [Header("Volume Settings Container Reference")]
    [SerializeField] private GameObject volumeSettingsContainer;

    [Space(20)]
    [Header("Static Sliders")]
    [SerializeField] private Slider staticMasterVolumeSlider = null;
    [SerializeField] private Slider staticMusicVolumeSlider = null;
    [SerializeField] private Slider staticSfxVolumeSlider = null;
    [SerializeField] private Slider staticVoiceVolumeSlider = null;

    [Space(20)]
    [Header("Volume Settings")]
    [SerializeField] private float defaultVolume = 0.5f;

    [Space(20)]
    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider = null;
    [SerializeField] private Slider musicVolumeSlider = null;
    [SerializeField] private Slider sfxVolumeSlider = null;
    [SerializeField] private Slider voiceVolumeSlider = null;

    [Space(20)]

    [SerializeField] private InputActionReference _applyAction;

    // Raw slider values (0-1) used to apply master scaling consistently.
    private float _masterVolumeRaw;
    private float _musicVolumeRaw;
    private float _sfxVolumeRaw;
    private float _voiceVolumeRaw;

    void Awake()
    {
        if (HasSavedVolumes())
        {
            CacheRawVolumesFromSources();
        }
        else
        {
            _masterVolumeRaw = defaultVolume;
            _musicVolumeRaw = defaultVolume;
            _sfxVolumeRaw = defaultVolume;
            _voiceVolumeRaw = defaultVolume;
            ApplyScaledVolumes();
        }
        SetStaticSlidersToCurrentValues();
        SetCurrentValuesOnSliders();
    }

    private void OnEnable()
    {
        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed += ctx => VolumeApply();
    }

    private void OnDisable()
    {
        if (_applyAction != null && _applyAction.action != null)
            _applyAction.action.performed -= ctx => VolumeApply();
    }

    //all functions below sets the volumes for each mixer depending on the slider
    public void SetSFXVolume(float volume)
    {
        _sfxVolumeRaw = volume;
        ApplyScaledVolumes();
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolumeRaw = volume;
        ApplyScaledVolumes();
    }

    public void SetVoiceVolume(float volume)
    {
        _voiceVolumeRaw = volume;
        ApplyScaledVolumes();
    }


    public void SetMasterVolume(float volume)
    {
        _masterVolumeRaw = volume;
        ApplyScaledVolumes();
    }

    private bool HasSavedVolumes()
    {
        return PlayerPrefs.HasKey("masterVolume")
            || PlayerPrefs.HasKey("musicVolume")
            || PlayerPrefs.HasKey("sfxVolume")
            || PlayerPrefs.HasKey("voiceVolume");
    }

    // This method caches the raw volume levels from the audio sources, which are used for applying master volume scaling correctly.
    private void CacheRawVolumesFromSources()
    {
        _masterVolumeRaw = SoundManager.Instance.masterSource.volume;
        _musicVolumeRaw = GetRawVolume(SoundManager.Instance.musicSource.volume, _masterVolumeRaw);
        _sfxVolumeRaw = GetRawVolume(SoundManager.Instance.sfxSource.volume, _masterVolumeRaw);
        _voiceVolumeRaw = GetRawVolume(SoundManager.Instance.voiceSource.volume, _masterVolumeRaw);
    }

    private float GetRawVolume(float scaledVolume, float masterVolume)
    {
        if (masterVolume <= 0f)
            return 0f;

        return Mathf.Clamp01(scaledVolume / masterVolume);
    }

    private void ApplyScaledVolumes()
    {
        SoundManager.Instance.masterSource.volume = _masterVolumeRaw;
        SoundManager.Instance.musicSource.volume = _musicVolumeRaw * _masterVolumeRaw;
        SoundManager.Instance.sfxSource.volume = _sfxVolumeRaw * _masterVolumeRaw;
        SoundManager.Instance.voiceSource.volume = _voiceVolumeRaw * _masterVolumeRaw;

        if (SoundManager.Instance.ambienceSource != null)
            SoundManager.Instance.ambienceSource.volume = _musicVolumeRaw * _masterVolumeRaw * 0.2f; // Ambience is typically quieter than music

        if (SoundManager.Instance.levelMusicSource != null)
            SoundManager.Instance.levelMusicSource.volume = _musicVolumeRaw * _masterVolumeRaw;

        if (SoundManager.Instance.uiSource != null)
            SoundManager.Instance.uiSource.volume = _sfxVolumeRaw * _masterVolumeRaw;

        if (SoundManager.Instance.puzzleSource != null)
            SoundManager.Instance.puzzleSource.volume = _sfxVolumeRaw * _masterVolumeRaw;

        UpdateMusicBoxVolumes();
    }

    private void UpdateMusicBoxVolumes()
    {
        MusicBox[] musicBoxes = FindObjectsOfType<MusicBox>();
        foreach (MusicBox box in musicBoxes)
        {
            box.UpdateCachedVolumes();
        }
    }

    private void SetStaticSlidersToCurrentValues()
    {
        if (staticMasterVolumeSlider != null)
            staticMasterVolumeSlider.value = SoundManager.Instance.masterSource.volume;

        if (staticMusicVolumeSlider != null)
            staticMusicVolumeSlider.value = _musicVolumeRaw;;

        if (staticSfxVolumeSlider != null)
            staticSfxVolumeSlider.value = _sfxVolumeRaw;

        if (staticVoiceVolumeSlider != null)
            staticVoiceVolumeSlider.value = _voiceVolumeRaw;
    }

    private void SetCurrentValuesOnSliders()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.value = _masterVolumeRaw;

        if (musicVolumeSlider != null)
            musicVolumeSlider.value = _musicVolumeRaw;

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.value = _sfxVolumeRaw;

        if (voiceVolumeSlider != null)
            voiceVolumeSlider.value = _voiceVolumeRaw;
    }

    //Applies volume levels
    public void VolumeApply()
    {
        // Persist current live values
        PlayerPrefs.SetFloat("masterVolume", _masterVolumeRaw);
        PlayerPrefs.SetFloat("sfxVolume", _sfxVolumeRaw);
        PlayerPrefs.SetFloat("musicVolume", _musicVolumeRaw);
        PlayerPrefs.SetFloat("voiceVolume", _voiceVolumeRaw);

        SetStaticSlidersToCurrentValues();

        PlayerPrefs.Save();

    }

    //Resets settings
    public void ResetButton()
    {
        _masterVolumeRaw = defaultVolume;
        _musicVolumeRaw = defaultVolume;
        _sfxVolumeRaw = defaultVolume;
        _voiceVolumeRaw = defaultVolume;

        masterVolumeSlider.value = defaultVolume;
        musicVolumeSlider.value = defaultVolume;
        sfxVolumeSlider.value = defaultVolume;
        voiceVolumeSlider.value = defaultVolume;

        ApplyScaledVolumes();

        VolumeApply();
    }
}
