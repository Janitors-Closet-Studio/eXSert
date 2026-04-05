using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [SerializeField] float speed = 2f;    // How fast it moves
    [SerializeField] float height = 0.5f; // How far up and down it goes
    
    Vector3 startPos;

    void Start()
    {
        // Store the initial position of the object
        startPos = transform.position;
    }

    void Update()
    {
        // Calculate the new Y position using a sine wave
        float newY = startPos.y + Mathf.Sin(Time.time * speed) * height;
        
        // Apply the new position while keeping X and Z the same
        transform.position = new Vector3(startPos.x, newY, startPos.z);
    }
}