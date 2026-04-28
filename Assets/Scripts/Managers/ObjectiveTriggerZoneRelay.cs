using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ObjectiveTriggerZoneRelay : MonoBehaviour
{
    public event Action<BoxCollider> Triggered;

    [SerializeField] private BoxCollider sourceCollider;

    public static ObjectiveTriggerZoneRelay GetOrAdd(BoxCollider triggerZone)
    {
        if (triggerZone == null)
            return null;

        ObjectiveTriggerZoneRelay relay = triggerZone.GetComponent<ObjectiveTriggerZoneRelay>();
        if (relay == null)
            relay = triggerZone.gameObject.AddComponent<ObjectiveTriggerZoneRelay>();

        relay.sourceCollider = triggerZone;
        if (!triggerZone.isTrigger)
            triggerZone.isTrigger = true;

        return relay;
    }

    private void Awake()
    {
        if (sourceCollider == null)
            sourceCollider = GetComponent<BoxCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (sourceCollider == null || other == null)
            return;

        if (!IsPlayerCollider(other))
            return;

        Triggered?.Invoke(sourceCollider);
    }

    private static bool IsPlayerCollider(Collider other)
    {
        return other.CompareTag("Player") || other.transform.root.CompareTag("Player");
    }
}