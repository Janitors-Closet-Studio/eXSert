/*
    Written by Brandon Wahl

    This script manages the platform rotation puzzle in the hangar level. Here once the designated interact point is click,
    the platform will rotate 180 degrees. The player will also move with the platform to perserve realism.
*/


using UnityEngine;

public class HangarPlatformRotationPuzzle : PuzzlePart
{
    [SerializeField] private float lerpSpeed;

    private GameObject player;
    private CharacterController playerController;
    private Vector3 playerStartOffset; // Offset from platform at start of rotation
    private Vector3 lastPlayerPosition; // Track player position from last frame
    private Quaternion lastPlatformRotation; // Track platform rotation from last frame

    private bool isRotating = false;
    private Quaternion startPos;
    private Quaternion targetPos;

    protected Quaternion origin;


    private void Awake()
    {
        origin = this.transform.rotation;
        
        // Find player by CharacterController component instead of tag
        playerController = FindFirstObjectByType<CharacterController>();
        if (playerController != null)
        {
            player = playerController.gameObject;
        }
    }

    public override void ConsoleInteracted()
    {
        throw new System.NotImplementedException();
    }

    public override void StartPuzzle()
    {
        if(!isCompleted)
        {
            startPos = origin;
            targetPos = Quaternion.Euler(startPos.eulerAngles.x, startPos.eulerAngles.y - 180, startPos.eulerAngles.z);
            isRotating = true;
            isCompleted = true;
            
            // Store player's offset from platform center at start
            if (player != null)
            {
                playerStartOffset = player.transform.position - this.transform.position;
                lastPlayerPosition = player.transform.position;
                lastPlatformRotation = this.transform.rotation;
            }
        }
    }

    public override void EndPuzzle()
    {
        if(isCompleted)
        {
            startPos = this.transform.rotation;
            targetPos = origin;
            isRotating = true;
            isCompleted = false;
            
            // Store player's offset from platform center at start
            if (player != null)
            {
                playerStartOffset = player.transform.position - this.transform.position;
                lastPlayerPosition = player.transform.position;
                lastPlatformRotation = this.transform.rotation;
            }
        }
    }

    // Calculate player's target position by rotating their offset around platform
    private void RotatePlayer()
    {
        if (player != null && playerController != null)
            {
                
                // Gets rotation delta this frame
                Quaternion rotationDelta = this.transform.rotation * Quaternion.Inverse(lastPlatformRotation);
                
                // Get current offset from platform
                Vector3 currentOffset = player.transform.position - this.transform.position;
                
                // Rotate the offset by the rotation delta
                Vector3 rotatedOffset = rotationDelta * currentOffset;
                
                // Calculate where player should be after platform rotation
                Vector3 targetPosition = this.transform.position + rotatedOffset;
                
                // This will allow for the player to be correctly rotated until they free move
                Vector3 rotationMovement = targetPosition - player.transform.position;
                
                playerController.Move(rotationMovement);
                
                // Update variables
                lastPlayerPosition = player.transform.position;
                lastPlatformRotation = this.transform.rotation;
            }
    }

    private void Update()
    {
        if (isRotating)
        {
            float t = Mathf.Clamp01(lerpSpeed * Time.deltaTime);

            //Rotates platform as needed
            this.transform.rotation = Quaternion.Lerp(this.transform.rotation, targetPos, t);

            RotatePlayer();
            
            // Once it is at its destination this will be called
            if (Quaternion.Angle(this.transform.rotation, targetPos) < 0.01f)
            {
                this.transform.rotation = targetPos;
                isRotating = false;
            }
        }
    }
}
