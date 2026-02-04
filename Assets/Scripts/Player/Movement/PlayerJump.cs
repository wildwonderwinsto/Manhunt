using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Tooltip("Instant jump response - sets velocity.y directly")]
    private float jumpForce = 5f;
    
    [SerializeField, Tooltip("Number of physics frames of grace after leaving ground")]
    private int coyoteFrames = 5; // ✅ This is the actual setting used

    private Rigidbody rb;
    private GroundCheck groundCheck;
    private PlayerMovement playerMovement;
    private PlayerInputReader inputReader;
    
    private Vector3 jumpVelocity;
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

        // Fix: Modern platformer gravity
        // Apply extra gravity when falling (removes floatiness)
        if (rb.linearVelocity.y < 0f)
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * 1.5f, ForceMode.Acceleration);
        }
        // Variable jump height: fall faster if jump released
        else if (!inputReader.JumpHeld && rb.linearVelocity.y > 0f)
        {
            rb.AddForce(Vector3.up * Physics.gravity.y * 2f, ForceMode.Acceleration);
        }

        if (groundCheck.IsGrounded(rb))
        {
            framesSinceGrounded = 0;
        }
        else
        {
            framesSinceGrounded++;
        }

        if (inputReader.JumpPressed)
        {
            inputReader.ConsumeJump();
            
            if (framesSinceGrounded <= coyoteFrames)
            {
                // Fix: Apply impulse, don't overwrite
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                
                playerMovement?.AddSprintJumpMomentum();
            }
        }
    }
}