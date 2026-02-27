using UnityEngine.Events;
using UnityEngine;

public class HintInteractions : InteractionManager
{
    private Hint hint;
    public UnityEvent[] collectEvents;

    protected override void Awake()
    {
        base.Awake();

        hint = GetComponent<Hint>();
        if (hint == null)
        {
            Debug.LogWarning($"HintInteractions on {gameObject.name} does not have a Hint component attached.");
        }
        else 
        {
            hint.enabled = false; // Ensure the hint is disabled at the start
        }
    }

    protected override void Interact()
    {
        if (hint != null)
        {
            hint.enabled = true; // Enable the hint component when interacted with
            if(_interactionSFX != null)
                SoundManager.Instance.sfxSource.PlayOneShot(_interactionSFX);

            foreach (UnityEvent collectEvent in collectEvents)
            {
                if (collectEvent != null)
                {
                    this.hint.enabled = true; // Enable the hint component when the item is collected
                    collectEvent?.Invoke();
                }
            }
        }
        
    }

}
