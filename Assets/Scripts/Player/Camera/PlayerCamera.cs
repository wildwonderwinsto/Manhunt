using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Range(0.1f, 10f)] private float mouseSensitivity = 2f;

    private Transform playerTransform;
    private PlayerInputReader inputReader;
    private float verticalRotation = 0f;
    
    // Horizontal rotation stored for PlayerMovement to consume
    private float accumulatedHorizontalInput = 0f;

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
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;
        
        // Accumulate for physics to consume
        accumulatedHorizontalInput += mouseX;
        
        // Camera rotation happens immediately in LateUpdate (correct)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);
        transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    public float ConsumeHorizontalInput()
    {
        float input = accumulatedHorizontalInput;
        accumulatedHorizontalInput = 0f;
        return input;
    }

    // PlayerMovement.FixedUpdate():
    float horizontalLookInput = playerCamera.ConsumeHorizontalInput();

}