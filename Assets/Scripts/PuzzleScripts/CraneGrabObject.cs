/*
    Written by Brandon W

    This script should be attached to the magnet extender and handles the grabbing and releasing of the desired object.
*/

using UnityEngine;

public class CraneGrabObject : MonoBehaviour
{
    [SerializeField] private CargoBayCrane cargoBayCrane;
    [SerializeField] private Transform grabAnchor;
    [SerializeField] private Vector3 grabLocalOffset = new Vector3(0f, -1.7f, 0f);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (grabLocalOffset == Vector3.zero)
            grabLocalOffset = new Vector3(0f, -1f, 0f);
    }
#endif

    private void Start()
    {
        // Verify setup
        if (cargoBayCrane == null)
        {
            return;
        }
        
        if (cargoBayCrane.magnetExtender == null)
        {
            return;
        }

        // Verify this GameObject has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            return;
        }
    }

    public void GrabObject(GameObject obj)
    {
        Transform parent = grabAnchor != null ? grabAnchor : cargoBayCrane.magnetExtender.transform;

        // Parent to the anchor, keep world scale, then snap to the desired local offset
        Vector3 worldScale = obj.transform.lossyScale;
        obj.transform.SetParent(parent, true);
        PreserveWorldScale(obj.transform, worldScale);
        obj.transform.localPosition = grabLocalOffset;
    }

    private void PreserveWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z)
        );
    }

    private float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? 0f : value / divisor;
    }

    public void ReleaseObject(GameObject obj)
    {
        // Unparent the object
        obj.transform.SetParent(null, true);;
    }

}
