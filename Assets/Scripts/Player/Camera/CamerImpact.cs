using UnityEngine;

/// <summary>
/// Camera impact system for landing and jump reactions.
/// - Landing impact: Camera punch proportional to fall velocity
/// - Jump kick: Subtle upward camera movement on jump
/// - Spring physics: Realistic bounce-back behavior
/// - Grounded detection: Works with or without GroundCheck component
/// - Frame-rate independent, zero allocations
/// </summary>
public class CameraImpact : MonoBehaviour
{
    [Header("Landing Impact")]
    [SerializeField, Range(0f, 0.3f), Tooltip("Maximum downward offset on landing")]
    private float maxImpactOffset = 0.1f;
    
    [SerializeField, Range(1f, 20f), Tooltip("Minimum fall velocity to trigger impact (m/s)")]
    private float minImpactVelocity = 2f;
    
    [SerializeField, Range(5f, 30f), Tooltip("Fall velocity for maximum impact (m/s)")]
    private float maxImpactVelocity = 15f;
    
    [SerializeField, Range(0f, 1f), Tooltip("Impact strength multiplier (0 = disabled, 1 = full)")]
    private float impactStrength = 1f;

    [Header("Jump Punch")]
    [SerializeField, Range(0f, 0.1f), Tooltip("Upward offset when jumping")]
    private float jumpKickOffset = 0.02f;
    
    [SerializeField, Range(1f, 10f), Tooltip("Minimum upward velocity to trigger jump punch (m/s)")]
    private float minJumpVelocity = 4f;
    
    [SerializeField, Range(0f, 1f), Tooltip("Jump punch strength multiplier (0 = disabled, 1 = full)")]
    private float jumpKickStrength = 1f;

    [Header("Spring Physics")]
    [SerializeField, Range(10f, 200f), Tooltip("Spring stiffness (higher = snappier return)")]
    private float springStiffness = 60f;
    
    [SerializeField, Range(1f, 30f), Tooltip("Spring damping (higher = less oscillation)")]
    private float springDamping = 10f;
    
    [SerializeField, Range(0f, 2f), Tooltip("Additional gravity on spring (pulls camera down faster)")]
    private float springGravity = 0.5f;

    [Header("FOV Punch (Optional)")]
    [SerializeField, Tooltip("Apply FOV change on impact?")]
    private bool useFOVPunch = true;
    
    [SerializeField, Range(0f, 10f), Tooltip("FOV reduction on landing (degrees)")]
    private float maxFOVPunch = 3f;
    
    [SerializeField, Range(1f, 20f), Tooltip("FOV recovery speed")]
    private float fovRecoverySpeed = 8f;

    [Header("Rotation Impact (Optional)")]
    [SerializeField, Tooltip("Apply rotation impact on landing?")]
    private bool useRotationImpact = true;
    
    [SerializeField, Range(0f, 10f), Tooltip("Max pitch rotation on landing (degrees)")]
    private float maxPitchImpact = 2f;
    
    [SerializeField, Range(1f, 20f), Tooltip("Rotation recovery speed")]
    private float rotationRecoverySpeed = 10f;

    [Header("References")]
    [SerializeField, Tooltip("Player Rigidbody")]
    private Rigidbody playerRigidbody;
    
    [SerializeField, Tooltip("GroundCheck component (optional - will auto-find)")]
    private GroundCheck groundCheck;
    
    // PUBLIC property for DynamicFOV to read
    public float CurrentFOVOffset { get; private set; }

    // ===== CACHED PHYSICS DATA (updated in FixedUpdate) =====
    private bool wasGroundedLastFrame;
    private bool isGroundedThisFrame;
    private float lastAirborneVelocityY;      // Y velocity just before landing
    private Vector3 cachedVelocity;

    // ===== RUNTIME STATE: SPRING SYSTEM =====
    private float currentOffset;              // Current vertical displacement
    private float offsetVelocity;             // Velocity of spring motion
    private bool hasActiveImpact;             // Is spring currently active?

    // ===== RUNTIME STATE: FOV PUNCH =====
    private float currentFOVOffset;           // Current FOV delta

    // ===== RUNTIME STATE: ROTATION IMPACT =====
    private float currentPitchOffset;         // Current X-axis rotation delta

    // ===== CACHED OUTPUT (zero allocation) =====
    private Vector3 impactPositionOffset;     // Final position offset
    private Quaternion impactRotationOffset;  // Final rotation offset
    private Vector3 eulerAngles;              // Cached for rotation building

    // ===== EVENT FLAGS (set in FixedUpdate, consumed in LateUpdate) =====
    private bool landingEventThisFrame;
    private float landingImpactStrength;
    private bool jumpEventThisFrame;

    // ===== INITIALIZATION =====
    private void Awake()
    {
        // Validate references
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponentInParent<Rigidbody>();
            if (playerRigidbody == null)
            {
                Debug.LogError("[CameraImpact] PlayerRigidbody reference missing! Assign in Inspector.", this);
            }
        }

        // Auto-find GroundCheck if not assigned
        if (groundCheck == null)
        {
            groundCheck = GetComponentInParent<GroundCheck>();
            if (groundCheck == null)
            {
                Debug.LogWarning("[CameraImpact] GroundCheck not found. Will use basic grounded detection.", this);
            }
        }



        // Initialize state
        wasGroundedLastFrame = true;
        currentOffset = 0f;
        offsetVelocity = 0f;
        impactPositionOffset = Vector3.zero;
        impactRotationOffset = Quaternion.identity;
        eulerAngles = Vector3.zero;
    }

    // ===== PHYSICS STEP - Detect landing/jump events =====
    private void FixedUpdate()
    {
        if (playerRigidbody == null) return;

        // Cache velocity
        cachedVelocity = playerRigidbody.linearVelocity;

        // Determine grounded state
        if (groundCheck != null)
        {
            isGroundedThisFrame = groundCheck.IsGrounded(playerRigidbody);
        }
        else
        {
            // Fallback: simple ground check using velocity
            // (Not as reliable, but works without GroundCheck component)
            isGroundedThisFrame = Mathf.Abs(cachedVelocity.y) < 0.1f && Physics.Raycast(
                playerRigidbody.position, 
                Vector3.down, 
                1f
            );
        }

        // Reset event flags
        landingEventThisFrame = false;
        jumpEventThisFrame = false;

        // DETECT LANDING EVENT
        if (!wasGroundedLastFrame && isGroundedThisFrame)
        {
            // Just landed!
            float impactVelocity = Mathf.Abs(lastAirborneVelocityY);
            
            if (impactVelocity >= minImpactVelocity)
            {
                landingEventThisFrame = true;
                
                // Calculate impact strength (0 to 1)
                float normalizedImpact = Mathf.Clamp01(
                    (impactVelocity - minImpactVelocity) / (maxImpactVelocity - minImpactVelocity)
                );
                
                landingImpactStrength = normalizedImpact * impactStrength;
            }
        }

        // DETECT JUMP EVENT
        if (wasGroundedLastFrame && !isGroundedThisFrame)
        {
            // Just left ground - check if it's a jump (positive Y velocity)
            if (cachedVelocity.y >= minJumpVelocity)
            {
                jumpEventThisFrame = true;
            }
        }

        // Track airborne velocity (for landing detection)
        if (!isGroundedThisFrame)
        {
            lastAirborneVelocityY = cachedVelocity.y;
        }

        // Update grounded state for next frame
        wasGroundedLastFrame = isGroundedThisFrame;
    }

    // ===== CAMERA APPLICATION - Apply spring physics and effects =====
    private void LateUpdate()
    {
        // Process events from FixedUpdate
        ProcessLandingEvent();
        ProcessJumpEvent();

        // Update spring physics
        UpdateSpringPhysics();

        // Update FOV punch
        if (useFOVPunch)
        {
            UpdateFOVPunch();
        }

        // Update rotation impact
        if (useRotationImpact)
        {
            UpdateRotationImpact();
        }

        // Apply final transforms
        ApplyImpactTransforms();
    }

    // ===== PROCESS LANDING EVENT =====
    private void ProcessLandingEvent()
    {
        if (!landingEventThisFrame) return;

        // Calculate impact displacement
        float impactDisplacement = -landingImpactStrength * maxImpactOffset;

        // Apply impact to spring (adds to current offset and velocity)
        currentOffset += impactDisplacement;
        offsetVelocity += impactDisplacement * 10f; // Add downward velocity impulse

        // Trigger FOV punch
        if (useFOVPunch)
        {
            currentFOVOffset = -landingImpactStrength * maxFOVPunch;
        }

        // Trigger rotation impact
        if (useRotationImpact)
        {
            currentPitchOffset = -landingImpactStrength * maxPitchImpact;
        }

        hasActiveImpact = true;
    }

    // ===== PROCESS JUMP EVENT =====
    private void ProcessJumpEvent()
    {
        if (!jumpEventThisFrame) return;

        // Apply upward kick
        float kickDisplacement = jumpKickOffset * jumpKickStrength;
        
        currentOffset += kickDisplacement;
        offsetVelocity += kickDisplacement * 15f; // Upward velocity impulse

        hasActiveImpact = true;
    }

    // ===== SPRING PHYSICS UPDATE =====
    private void UpdateSpringPhysics()
    {
        if (!hasActiveImpact && Mathf.Abs(currentOffset) < 0.001f && Mathf.Abs(offsetVelocity) < 0.001f)
        {
            // Spring has settled
            currentOffset = 0f;
            offsetVelocity = 0f;
            return;
        }

        // Spring force: F = -k*x - c*v - g
        // k = spring constant (stiffness)
        // c = damping coefficient
        // x = displacement from rest
        // v = velocity
        // g = additional gravity pull

        float springForce = -springStiffness * currentOffset;
        float dampingForce = -springDamping * offsetVelocity;
        float gravityForce = -springGravity;

        float totalForce = springForce + dampingForce + gravityForce;

        // Update velocity and position (Euler integration, frame-rate independent)
        offsetVelocity += totalForce * Time.deltaTime;
        currentOffset += offsetVelocity * Time.deltaTime;

        // Clamp to prevent extreme values
        currentOffset = Mathf.Clamp(currentOffset, -maxImpactOffset * 2f, maxImpactOffset * 0.5f);

        // Check if spring has settled
        if (Mathf.Abs(currentOffset) < 0.001f && Mathf.Abs(offsetVelocity) < 0.01f)
        {
            hasActiveImpact = false;
            currentOffset = 0f;
            offsetVelocity = 0f;
        }
    }

    // ===== FOV PUNCH UPDATE =====
    private void UpdateFOVPunch()
    {
        if (!useFOVPunch) return;

        // Smoothly return FOV offset to zero
        float lerpFactor = 1f - Mathf.Exp(-fovRecoverySpeed * Time.deltaTime);
        currentFOVOffset = Mathf.Lerp(currentFOVOffset, 0f, lerpFactor);
        
        CurrentFOVOffset = currentFOVOffset; // Expose to public property
    }

    // ===== ROTATION IMPACT UPDATE =====
    private void UpdateRotationImpact()
    {
        // Smoothly return rotation to neutral
        float lerpFactor = 1f - Mathf.Exp(-rotationRecoverySpeed * Time.deltaTime);
        currentPitchOffset = Mathf.Lerp(currentPitchOffset, 0f, lerpFactor);
    }

    // ===== APPLY TRANSFORMS =====
    private void ApplyImpactTransforms()
    {
        // Position offset (vertical only)
        impactPositionOffset.Set(0f, currentOffset, 0f);
        transform.localPosition = impactPositionOffset;

        // Rotation offset (pitch only)
        if (useRotationImpact)
        {
            eulerAngles.Set(currentPitchOffset, 0f, 0f);
            impactRotationOffset = Quaternion.Euler(eulerAngles);
            transform.localRotation = impactRotationOffset;
        }
    }

    // ===== PUBLIC API =====

    /// <summary>
    /// Get current impact offset (for debugging)
    /// </summary>
    public Vector3 GetImpactOffset()
    {
        return impactPositionOffset;
    }

    /// <summary>
    /// Get current spring state (for debugging)
    /// </summary>
    public (float offset, float velocity) GetSpringState()
    {
        return (currentOffset, offsetVelocity);
    }

    /// <summary>
    /// Manually trigger a landing impact (for external events)
    /// </summary>
    /// <param name="strength">Impact strength (0-1)</param>
    public void TriggerLandingImpact(float strength)
    {
        strength = Mathf.Clamp01(strength);
        landingEventThisFrame = true;
        landingImpactStrength = strength * impactStrength;
    }

    /// <summary>
    /// Manually trigger a jump kick (for external events)
    /// </summary>
    public void TriggerJumpKick()
    {
        jumpEventThisFrame = true;
    }

    /// <summary>
    /// Reset all impact effects immediately (for teleports, cutscenes)
    /// </summary>
    public void ResetImpactImmediate()
    {
        currentOffset = 0f;
        offsetVelocity = 0f;
        currentFOVOffset = 0f;
        currentPitchOffset = 0f;
        hasActiveImpact = false;
        
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        

    }

    /// <summary>
    /// Set impact strength at runtime (0 = disabled, 1 = full)
    /// </summary>
    public void SetImpactStrength(float strength)
    {
        impactStrength = Mathf.Clamp01(strength);
    }

    /// <summary>
    /// Set jump kick strength at runtime (0 = disabled, 1 = full)
    /// </summary>
    public void SetJumpKickStrength(float strength)
    {
        jumpKickStrength = Mathf.Clamp01(strength);
    }

    /// <summary>
    /// Enable/disable FOV punch at runtime
    /// </summary>
    public void SetFOVPunchEnabled(bool enabled)
    {
        useFOVPunch = enabled;
        if (!enabled)
        {
            currentFOVOffset = 0f;
        }
    }

    // ===== DEBUG VISUALIZATION =====
    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        // Debug display (bottom-left corner)
        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(10, 10, 10, 10);

        float x = 10;
        float y = Screen.height - 150;
        float lineHeight = 20;

        GUI.Label(new Rect(x, y, 240, lineHeight), $"Impact Offset: {currentOffset:F4}", style);
        y += lineHeight;
        
        GUI.Label(new Rect(x, y, 240, lineHeight), $"Spring Velocity: {offsetVelocity:F4}", style);
        y += lineHeight;

        if (useFOVPunch)
        {
            GUI.Label(new Rect(x, y, 240, lineHeight), $"FOV Offset: {currentFOVOffset:F2}°", style);
            y += lineHeight;
        }

        if (useRotationImpact)
        {
            GUI.Label(new Rect(x, y, 240, lineHeight), $"Pitch Offset: {currentPitchOffset:F2}°", style);
            y += lineHeight;
        }

        // Status
        style.normal.textColor = hasActiveImpact ? Color.yellow : Color.green;
        GUI.Label(new Rect(x, y, 240, lineHeight), 
            hasActiveImpact ? "[Active Impact]" : "[Settled]", style);
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Vector3 origin = transform.position;

        // Draw spring displacement
        Gizmos.color = hasActiveImpact ? Color.red : Color.green;
        Gizmos.DrawRay(origin, Vector3.up * currentOffset * 10f);

        // Draw impact sphere (size = displacement)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        float sphereSize = Mathf.Abs(currentOffset) * 2f;
        Gizmos.DrawWireSphere(origin, sphereSize);

        // Draw velocity vector
        if (Mathf.Abs(offsetVelocity) > 0.01f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, Vector3.up * offsetVelocity);
        }
    }
    #endif
}