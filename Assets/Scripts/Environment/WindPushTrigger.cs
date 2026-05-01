using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WindPushTrigger : MonoBehaviour
{
    [SerializeField] private float pushSpeed = 6f;
    [SerializeField] private Vector3 localPushDirection = Vector3.forward;
    [SerializeField] private bool preserveVerticalVelocity;

    private Collider triggerCollider;
    private PlayerMovement activePlayerMovement;

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!TryGetPlayerMovement(other, out PlayerMovement playerMovement))
        {
            return;
        }

        Vector3 pushVelocity = transform.TransformDirection(localPushDirection);

        if (!preserveVerticalVelocity)
        {
            pushVelocity.y = 0f;
        }

        if (pushVelocity.sqrMagnitude <= 0.0001f || pushSpeed <= 0f)
        {
            playerMovement.ClearExternalVelocity();
            activePlayerMovement = null;
            return;
        }

        activePlayerMovement = playerMovement;
        playerMovement.SetExternalVelocity(pushVelocity.normalized * pushSpeed);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryGetPlayerMovement(other, out PlayerMovement playerMovement))
        {
            return;
        }

        if (playerMovement != activePlayerMovement)
        {
            return;
        }

        playerMovement.ClearExternalVelocity();
        activePlayerMovement = null;
    }

    private void OnDisable()
    {
        if (activePlayerMovement == null)
        {
            return;
        }

        activePlayerMovement.ClearExternalVelocity();
        activePlayerMovement = null;
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private static bool TryGetPlayerMovement(Collider other, out PlayerMovement playerMovement)
    {
        playerMovement = null;

        if (other == null)
        {
            return false;
        }

        Transform root = other.transform.root;

        if (root == null || !root.CompareTag("Player"))
        {
            return false;
        }

        playerMovement = other.GetComponent<PlayerMovement>()
            ?? other.GetComponentInParent<PlayerMovement>()
            ?? other.GetComponentInChildren<PlayerMovement>()
            ?? root.GetComponent<PlayerMovement>()
            ?? root.GetComponentInChildren<PlayerMovement>();

        return playerMovement != null;
    }
}