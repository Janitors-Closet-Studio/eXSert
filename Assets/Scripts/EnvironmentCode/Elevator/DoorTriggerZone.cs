using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/*
Written by Kyle
This script manages a trigger zone that opens and closes a two-part door when the player enters or exits the zone.
The door consists of a top and bottom part that slide apart vertically when opened and come back together when closed.
*/

[RequireComponent(typeof(BoxCollider))]
public class DoorTriggerZone : MonoBehaviour
{
    [Header("Door Pieces")]
    [SerializeField] private GameObject topDoor;      // top slab/panel
    [SerializeField] private GameObject bottomDoor;   // bottom slab/panel

    [Header("Motion")]
    [SerializeField] private float topOpenDistance = 2.0f;     // meters upward
    [SerializeField] private float bottomOpenDistance = 2.0f;  // meters downward
    [SerializeField] private float moveDuration = 0.75f;       // seconds per open/close
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
    [SerializeField] private bool stayOpened = false;

    [Header("Player Filter")]
    [SerializeField] private string playerTag = "Player";       // set your player’s tag

    [Header("Disable Zone")]
    [SerializeField] private BoxCollider disableTriggerZone;

    [Header("Events")]
    [SerializeField] private UnityEvent onEnteredTrigger;

    // cached start/end positions (local space to avoid parent movement jumps)
    private Vector3 _topClosedLocal, _bottomClosedLocal;
    private Vector3 _topOpenLocal, _bottomOpenLocal;

    private Coroutine _motion;
    private int _overlapCount = 0;   // supports multiple colliders entering (player + weapon, etc.)

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true; // make sure it’s a trigger
    }

    private void Awake()
    {
        if (topDoor == null || bottomDoor == null)
        {
            Debug.LogError($"{nameof(DoorTriggerZone)} on {name} is missing door references. Disabling to prevent crash.", this);
            enabled = false;
            return;
        }
        // Work in local space so elevator/platform movement doesn't cause snapping
        _topClosedLocal = topDoor.transform.localPosition;
        _bottomClosedLocal = bottomDoor.transform.localPosition;

        _topOpenLocal = _topClosedLocal + Vector3.up * topOpenDistance;
        _bottomOpenLocal = _bottomClosedLocal + Vector3.down * bottomOpenDistance;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        onEnteredTrigger?.Invoke();

        if (disableTriggerZone != null && IsInsideDisableZone(other))
        {
            DisableDoorTrigger();
            return;
        }

        _overlapCount++;
        if (_overlapCount == 1) StartMove(open: true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (!enabled) return;

        _overlapCount = Mathf.Max(0, _overlapCount - 1);
        if (stayOpened) return;

        if (_overlapCount == 0) StartMove(open: false);
    }

    private bool IsInsideDisableZone(Collider other)
    {
        return disableTriggerZone.bounds.Contains(other.bounds.center);
    }

    private void DisableDoorTrigger()
    {
        _overlapCount = 0;

        if (_motion != null)
        {
            StopCoroutine(_motion);
            _motion = null;
        }

        topDoor.transform.localPosition = _topClosedLocal;
        bottomDoor.transform.localPosition = _bottomClosedLocal;

        enabled = false;
    }

    private void StartMove(bool open)
    {
        if (_motion != null) StopCoroutine(_motion);
        _motion = StartCoroutine(MoveDoors(open));
    }

    private IEnumerator MoveDoors(bool open)
    {
        // Start from current local positions to avoid a visible jump when reversing or interrupting
        Vector3 topFrom = topDoor.transform.localPosition;
        Vector3 botFrom = bottomDoor.transform.localPosition;

        Vector3 topTo = open ? _topOpenLocal : _topClosedLocal;
        Vector3 botTo = open ? _bottomOpenLocal : _bottomClosedLocal;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, moveDuration);
            float k = ease.Evaluate(Mathf.Clamp01(t));
            // Lerp in local space; k is clamped so standard Lerp avoids overshoot
            topDoor.transform.localPosition    = Vector3.Lerp(topFrom, topTo, k);
            bottomDoor.transform.localPosition = Vector3.Lerp(botFrom, botTo, k);
            yield return null;
        }
        
        topDoor.transform.localPosition    = topTo;
        bottomDoor.transform.localPosition = botTo;
        _motion = null;
    }
}
