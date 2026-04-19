using UnityEngine;

public class PistonAnimator : MonoBehaviour
{
    [SerializeField] private Transform[] pistons;
    [SerializeField] private float topDeadCenterY = -2f;
    [SerializeField] private float bottomDeadCenterY = -4f;
    [SerializeField] private float cyclesPerSecond = 1f;
    [SerializeField] private bool playOnStart = true;

    private Vector3[] localPositions;
    private float elapsedTime;
    private bool isPlaying;

    private void Awake()
    {
        CacheLocalPositions();
        isPlaying = playOnStart;
        ApplyPositions(0f);
    }

    private void OnEnable()
    {
        CacheLocalPositions();
        ApplyPositions(elapsedTime);
    }

    private void OnValidate()
    {
        CacheLocalPositions();

        if (!Application.isPlaying)
        {
            ApplyPositions(0f);
        }
    }

    private void Update()
    {
        if (!isPlaying || pistons == null || pistons.Length == 0)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
        ApplyPositions(elapsedTime);
    }

    public void Play()
    {
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
    }

    public void ResetCycle()
    {
        elapsedTime = 0f;
        ApplyPositions(elapsedTime);
    }

    private void CacheLocalPositions()
    {
        if (pistons == null)
        {
            localPositions = new Vector3[0];
            return;
        }

        if (localPositions == null || localPositions.Length != pistons.Length)
        {
            localPositions = new Vector3[pistons.Length];
        }

        for (int index = 0; index < pistons.Length; index++)
        {
            if (pistons[index] == null)
            {
                continue;
            }

            localPositions[index] = pistons[index].localPosition;
        }
    }

    private void ApplyPositions(float timeValue)
    {
        if (pistons == null || pistons.Length == 0)
        {
            return;
        }

        float midpointY = (topDeadCenterY + bottomDeadCenterY) * 0.5f;
        float amplitude = (topDeadCenterY - bottomDeadCenterY) * 0.5f;
        float cycleAngle = timeValue * cyclesPerSecond * Mathf.PI * 2f;
        float phaseStep = (Mathf.PI * 2f) / pistons.Length;

        for (int index = 0; index < pistons.Length; index++)
        {
            Transform piston = pistons[index];

            if (piston == null)
            {
                continue;
            }

            Vector3 localPosition = localPositions[index];
            localPosition.y = midpointY + Mathf.Cos(cycleAngle + (phaseStep * index)) * amplitude;
            piston.localPosition = localPosition;
        }
    }
}
