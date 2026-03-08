using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class OneWayDoorCloseTrigger : MonoBehaviour
{
    [SerializeField] private OneWayDoor owner;

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void Reset()
    {
        EnsureTriggerCollider();

        if (owner == null)
            owner = GetComponentInParent<OneWayDoor>();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    public void SetOwner(OneWayDoor oneWayDoor)
    {
        owner = oneWayDoor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null)
            return;

        owner.NotifyPassedCloseTrigger(other);
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
}