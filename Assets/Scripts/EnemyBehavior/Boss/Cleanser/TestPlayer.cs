using UnityEngine;
using UnityEngine.InputSystem;

public class TestPlayer : MonoBehaviour, IHealthSystem
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float health = 100f;
    [SerializeField] private float moveSpeed = 5f;
    
    public float currentHP => health;
    public float maxHP => maxHealth;
    
    private Vector2 moveInput;
    private PlayerMovement playerMovement;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>()
            ?? GetComponentInParent<PlayerMovement>()
            ?? GetComponentInChildren<PlayerMovement>();
    }
    
    public void LoseHP(float damage)
    {
        health -= damage;
        Debug.Log($"[TestPlayer] Took {damage} damage. Health: {health}/{maxHealth}");
        if (health <= 0) Debug.Log("[TestPlayer] DEAD!");
    }
    
    public void HealHP(float hp) => health = Mathf.Min(health + hp, maxHealth);
    
    void Update()
    {
        // If full PlayerMovement exists, let it own movement so external velocity/knockback tests are valid.
        if (playerMovement != null)
            return;

        // Read WASD/Arrow keys using the new Input System
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        moveInput = Vector2.zero;
        
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            moveInput.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            moveInput.y -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            moveInput.x += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            moveInput.x -= 1f;
        
        Vector3 move = new Vector3(moveInput.x, 0, moveInput.y).normalized * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);
    }
}
