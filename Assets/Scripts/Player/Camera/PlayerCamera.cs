using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Range(0.1f, 10f)] private float mouseSensitivity = 2f;

    private Transform playerTransform;
    private PlayerInputReader inputReader;
    private float verticalRotation = 0f;
    
    // Horizontal rotation stored for PlayerMovement to consume
    [System.NonSerialized] public float horizontalLookInput = 0f;

    private void Awake()
    {
        // Cache component references in Awake for better performance
        // GetComponentInParent is expensive, so we cache it once
        inputReader = GetComponentInParent<PlayerInputReader>();
        
        // Get parent (Player) transform
        playerTransform = transform.parent;
        
        // Validate cached references
        if (inputReader == null)
        {
            Debug.LogError($"PlayerInputReader not found on parent of {gameObject.name}");
        }
        
        if (playerTransform == null)
        {
            Debug.LogError($"PlayerCamera must be a child of the Player GameObject");
        }
        
        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        // Early exit if required components are missing
        if (inputReader == null) return;

        // Read look input from InputReader
        Vector2 lookInput = inputReader.LookInput;

        // CORRECTED: Do NOT multiply mouse delta by Time.deltaTime
        // Mouse input is already a delta (change per frame)
        // Multiplying by deltaTime makes it framerate-dependent (bad!)
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        // Store horizontal input for PlayerMovement to apply via Rigidbody.MoveRotation
        horizontalLookInput = mouseX;

        // ONLY rotate camera vertically (X-axis) - player body rotates in FixedUpdate
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        
        transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }
}