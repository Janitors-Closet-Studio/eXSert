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

    public bool IsOnDestinationSide(Vector3 worldPosition)
    {
        return Vector3.Dot(transform.forward, worldPosition - transform.position) > 0f;
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