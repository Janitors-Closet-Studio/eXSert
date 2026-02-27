using UnityEngine;

public class PlayCraneSFX : MonoBehaviour
{
    [SerializeField] private AudioClip craneSFXClip;
    [SerializeField] private AudioClip craneStopClip;
    [SerializeField, Range(0f, 1f)] private float clipVolume = .25f;
    [SerializeField] private bool loop = true;
    [SerializeField] CranePuzzle crane;

    private AudioSource puzzleSource;
    private float originalSourceVolume;
    private bool wasMoving;
    private bool wasMagnetMoving;
    private bool stopClipPlayed;

    void Awake()
    {
        puzzleSource = SoundManager.Instance.puzzleSource;
        
        if (puzzleSource == null)
        {
            enabled = false;
            return;
        }
        
        originalSourceVolume = puzzleSource.volume;
    }

    private void Start()
    {
        if(craneSFXClip != null)
        {
            puzzleSource.clip = craneSFXClip;
            puzzleSource.volume = originalSourceVolume * clipVolume;
            puzzleSource.loop = loop;
        }
    }

    private void Update()
    {
        PlaySoundWhenMoving();
        PreventPlayingForever();
    }

    private void PlaySoundWhenMoving()
    {
        bool isMoving = crane.IsMoving();
        bool isExtending = crane.isExtending;
        bool isRetracting = crane.IsRetracting();
        bool isMagnetMoving = isExtending || isRetracting;
        if (isMoving || isMagnetMoving)
        {
            if (!wasMoving && !wasMagnetMoving)
            {
                puzzleSource.Stop();
                puzzleSource.Play();
                stopClipPlayed = false;
            }
            else if (!puzzleSource.isPlaying)
            {
                puzzleSource.Play();
            }
        }       
        else
        {
            if (wasMoving || wasMagnetMoving)
            {
                puzzleSource.Stop();
                if (!stopClipPlayed)
                {
                    PlayStopClip();
                    stopClipPlayed = true;
                }
            }
        }

        wasMoving = isMoving;
        wasMagnetMoving = isMagnetMoving;
    }

    private void PreventPlayingForever()
    {
        if(craneSFXClip.length == 0f && (crane.IsMoving() || crane.isExtending || crane.IsRetracting()))
        {
            puzzleSource.Stop();
            if (!stopClipPlayed)
            {
                PlayStopClip();
                stopClipPlayed = true;
            }
        }

        if(crane.isCompleted)
        {
            puzzleSource.Stop();
            if (!stopClipPlayed)
            {
                PlayStopClip();
                stopClipPlayed = true;
            }
        }
    }

    private void PlayStopClip()
    {
        if (craneStopClip != null)
        {
            puzzleSource.PlayOneShot(craneStopClip, clipVolume);
        }
    }

}
