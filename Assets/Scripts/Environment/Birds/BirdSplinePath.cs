using UnityEngine;
using UnityEngine.Splines;

[DisallowMultipleComponent]
public class BirdSplinePath : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Spline Container used for bird flight.")]
    private SplineContainer splineContainer;

    [SerializeField]
    [Min(0)]
    [Tooltip("Which spline inside the container to use.")]
    private int splineIndex;

    [SerializeField]
    [Tooltip(
        "Optional perch transform for the start of the path. If left empty, the bird uses the spline start position."
    )]
    private Transform startPoint;

    [SerializeField]
    [Tooltip(
        "Optional perch transform for the end of the path. If left empty, the bird uses the spline end position."
    )]
    private Transform endPoint;

    [SerializeField]
    [Tooltip("Draw perch markers in the Scene view.")]
    private bool drawPerchGizmos = true;

    public Transform StartPoint => startPoint;
    public Transform EndPoint => endPoint;

    public bool HasValidPath
    {
        get
        {
            return splineContainer != null
                && splineContainer.Splines != null
                && splineIndex >= 0
                && splineIndex < splineContainer.Splines.Count
                && splineContainer.Splines[splineIndex] != null
                && splineContainer.Splines[splineIndex].Count > 0;
        }
    }

    public float ApproximateLength
    {
        get
        {
            return HasValidPath ? splineContainer.CalculateLength(splineIndex) : 0f;
        }
    }

    private void Reset()
    {
        if (splineContainer == null)
        {
            splineContainer = GetComponent<SplineContainer>();
        }
    }

    private void OnValidate()
    {
        if (splineContainer == null)
        {
            splineContainer = GetComponent<SplineContainer>();
        }

        splineIndex = Mathf.Max(0, splineIndex);
    }

    public Vector3 GetPoint(float normalizedDistance)
    {
        if (!HasValidPath)
        {
            return transform.position;
        }

        return splineContainer.EvaluatePosition(splineIndex, Mathf.Clamp01(normalizedDistance));
    }

    public Vector3 GetTangent(float normalizedDistance)
    {
        if (!HasValidPath)
        {
            return transform.forward;
        }

        Vector3 tangent = splineContainer.EvaluateTangent(splineIndex, Mathf.Clamp01(normalizedDistance));
        if (tangent.sqrMagnitude > 0.0001f)
        {
            return tangent.normalized;
        }

        Vector3 fallback = GetEndPosition() - GetStartPosition();
        if (fallback.sqrMagnitude > 0.0001f)
        {
            return fallback.normalized;
        }

        return transform.forward;
    }

    public Vector3 GetStartPosition()
    {
        if (startPoint != null)
        {
            return startPoint.position;
        }

        return HasValidPath ? GetPoint(0f) : transform.position;
    }

    public Vector3 GetEndPosition()
    {
        if (endPoint != null)
        {
            return endPoint.position;
        }

        return HasValidPath ? GetPoint(1f) : transform.position;
    }

    public Quaternion GetStartRotation()
    {
        if (startPoint != null)
        {
            return startPoint.rotation;
        }

        Vector3 tangent = GetTangent(0f);
        return tangent.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(tangent, Vector3.up)
            : transform.rotation;
    }

    public Quaternion GetEndRotation()
    {
        if (endPoint != null)
        {
            return endPoint.rotation;
        }

        Vector3 tangent = GetTangent(1f);
        return tangent.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(tangent, Vector3.up)
            : transform.rotation;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPerchGizmos)
        {
            return;
        }

        if (startPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(startPoint.position, 0.15f);
        }

        if (endPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(endPoint.position, 0.15f);
        }
    }
}
