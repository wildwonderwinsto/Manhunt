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
                jumpVelocity = rb.linearVelocity;
                jumpVelocity.y = jumpForce;
                rb.linearVelocity = jumpVelocity;
                
                playerMovement?.AddSprintJumpMomentum();
            }
        }
    }
}