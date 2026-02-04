using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    private Rigidbody rb;
    private GroundCheck groundCheck;
    private Transform cameraTransform;
    private PlayerInputReader inputReader;
    private PlayerCamera playerCamera; // Reference to camera for rotation input

    [Header("Settings")]
    [SerializeField, Range(1f, 10f)] private float walkSpeed = 7f;
    [SerializeField, Range(5f, 15f)] private float sprintSpeed = 10f;
    [SerializeField, Range(0.05f, 0.5f)] private float accelerationTime = 0.1f;

    private Vector3 targetVelocity;
    private Vector3 currentVelocity; // Cached to avoid allocations
    private Vector3 newVelocity; // Cached to avoid allocations

    // Sprint-jump momentum system
    private float momentumBoost = 0f;
    private const float BOOST_PER_JUMP = 0.5f;
    private const float MAX_MOMENTUM_STACKS = 3f;
    private float groundedTimer = 0f;

    
    
    

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        groundCheck = GetComponent<GroundCheck>();
        inputReader = GetComponent<PlayerInputReader>();
        
        // Configure Rigidbody for FPS controller
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.sleepThreshold = 0f; // Prevent sleeping
        }
        
        Transform cameraHolder = transform.Find("CameraHolder");
        if (cameraHolder != null)
        {
            cameraTransform = cameraHolder;
            playerCamera = cameraHolder.GetComponentInChildren<PlayerCamera>();
        }
    }

    private void FixedUpdate()
    {
        #if UNITY_EDITOR
        if (rb.constraints != RigidbodyConstraints.FreezeRotation)
        {
            Debug.LogError("Rigidbody constraints were modified! Resetting...");
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
        #endif
        
        HandleRotation();     
        HandleMovement();     
        HandleMomentumDecay();
    }


    private void HandleRotation()
    {
        if (playerCamera == null) return;

        float horizontalLookInput = playerCamera.ConsumeHorizontalInput();
        
        if (horizontalLookInput != 0f)
        {
            Quaternion rotationDelta = Quaternion.Euler(0f, horizontalLookInput, 0f);
            Quaternion targetRotation = rb.rotation * rotationDelta;
            rb.MoveRotation(targetRotation);
        }
    }

    private void HandleMovement()
    {
        if (cameraTransform == null || inputReader == null) return;

        // Read input from InputReader
        Vector2 moveInput = inputReader.MoveInput;
        bool isSprinting = inputReader.SprintHeld;

        // Calculate movement direction relative to camera
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;

        // Calculate target speed with momentum boost
        float targetSpeed = (isSprinting ? sprintSpeed : walkSpeed) + momentumBoost;
        targetVelocity = moveDirection * targetSpeed;

        // Preserve Y velocity, only lerp horizontal movement (NO ALLOCATIONS)
        currentVelocity.Set(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        
        // Frame-rate independent acceleration (60fps baseline)
        float lerpFactor = 1f - Mathf.Exp(-Time.fixedDeltaTime / accelerationTime);
        
        // Fix: Scale acceleration by air control
        if (!groundCheck.IsGrounded(rb))
        {
            lerpFactor *= 0.4f; // Reduce control in air to prevent infinite strafing/wall sticking
        }

        newVelocity = Vector3.Lerp(currentVelocity, targetVelocity, lerpFactor);
        
        rb.linearVelocity = new Vector3(newVelocity.x, rb.linearVelocity.y, newVelocity.z);
    }

    private void HandleMomentumDecay()
    {
        if (groundCheck.IsGrounded(rb)) // ✅ Pass rb parameter
        {
            groundedTimer += Time.fixedDeltaTime;
            if (groundedTimer >= 1f && momentumBoost > 0f)
            {
                momentumBoost = 0f;
            }
        }
        else
        {
            groundedTimer = 0f;
        }
    }

    // Called by PlayerJump when sprint-jumping
    public void AddSprintJumpMomentum()
    {
        if (inputReader != null && inputReader.SprintHeld && momentumBoost < MAX_MOMENTUM_STACKS * BOOST_PER_JUMP)
        {
            momentumBoost += BOOST_PER_JUMP;
            groundedTimer = 0f; // Reset decay timer
        }
    }
}
