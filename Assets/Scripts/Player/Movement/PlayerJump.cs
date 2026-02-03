using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Tooltip("Instant jump response - sets velocity.y directly")]
    private float jumpForce = 5f;
    
    [SerializeField, Tooltip("Grace period to jump after leaving ground")]
    private float coyoteTime = 0.1f;

    private Rigidbody rb;
    private GroundCheck groundCheck;
    private PlayerMovement playerMovement;
    private PlayerInputReader inputReader;
    
    
    private Vector3 jumpVelocity; // Cached to avoid allocation

    private int coyoteFrames = 5; // Explicit: "5 physics frames of grace"
    private int framesSinceGrounded = 999;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        groundCheck = GetComponent<GroundCheck>();
        playerMovement = GetComponent<PlayerMovement>();
        inputReader = GetComponent<PlayerInputReader>();
    }

    private void FixedUpdate()
    {
        if (inputReader == null) return;

        // Track frames since grounded
        if (groundCheck.IsGrounded(rb))
        {
            framesSinceGrounded = 0;
        }
        else
        {
            framesSinceGrounded++;
        }

        // Process jump when button is pressed
        if (inputReader.JumpPressed)
        {
            inputReader.ConsumeJump();
            
            // Allow jump if within coyote frames
            if (framesSinceGrounded <= coyoteFrames)
            {
                jumpVelocity = rb.linearVelocity;
                jumpVelocity.y = jumpForce;
                rb.linearVelocity = jumpVelocity;
                
                playerMovement?.AddSprintJumpMomentum();
            }
        }
    }
}
