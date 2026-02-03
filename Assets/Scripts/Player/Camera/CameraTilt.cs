using UnityEngine;

/// <summary>
/// Camera tilt system for first-person immersion.
/// - Strafe tilt: Lean into left/right movement
/// - Forward/backward tilt: Subtle dip when starting to move
/// - Look tilt: Slight roll when turning camera
/// - All tilts smoothly return to neutral
/// - Frame-rate independent, zero allocations
/// </summary>
public class CameraTilt : MonoBehaviour
{
    [Header("Strafe Tilt (Z-axis lean)")]
    [SerializeField, Range(0f, 15f), Tooltip("Max tilt angle when strafing left/right")]
    private float maxStrafeTilt = 4f;
    
    [SerializeField, Range(1f, 20f), Tooltip("How quickly strafe tilt responds to input")]
    private float strafeTiltSpeed = 8f;
    
    [SerializeField, Range(0f, 1f), Tooltip("Tilt reduction when moving forward/backward (0 = full tilt even when moving forward)")]
    private float forwardMovementReduction = 0.5f;

    [Header("Forward/Backward Tilt (X-axis dip)")]
    [SerializeField, Range(0f, 5f), Tooltip("Max tilt angle when accelerating forward")]
    private float maxForwardTilt = 1.5f;
    
    [SerializeField, Range(0f, 5f), Tooltip("Max tilt angle when accelerating backward")]
    private float maxBackwardTilt = 1f;
    
    [SerializeField, Range(1f, 20f), Tooltip("How quickly forward/back tilt responds")]
    private float forwardTiltSpeed = 10f;
    
    [SerializeField, Range(0.1f, 1f), Tooltip("How long the acceleration tilt lasts")]
    private float accelerationTiltDuration = 0.3f;

    [Header("Look Tilt (Z-axis roll during turns)")]
    [SerializeField, Range(0f, 10f), Tooltip("Max tilt angle when turning camera")]
    private float maxLookTilt = 2f;
    
    [SerializeField, Range(0.01f, 0.5f), Tooltip("Time window to measure look input")]
    private float lookTiltWindow = 0.1f;
    
    [SerializeField, Range(0.1f, 5f), Tooltip("Sensitivity to camera rotation")]
    private float lookTiltSensitivity = 1f;
    
    [SerializeField, Range(1f, 20f), Tooltip("How quickly look tilt responds")]
    private float lookTiltSpeed = 12f;

    [Header("Recovery")]
    [SerializeField, Range(1f, 20f), Tooltip("How quickly all tilts return to neutral")]
    private float recoverySpeed = 6f;

    [Header("References")]
    [SerializeField, Tooltip("SpeedBasedEffects component (optional but recommended)")]
    private SpeedBasedEffects speedAnalyzer;
    
    [SerializeField, Tooltip("Player Rigidbody (required if no SpeedAnalyzer)")]
    private Rigidbody playerRigidbody;
    
    [SerializeField, Tooltip("Player InputReader for movement input")]
    private PlayerInputReader inputReader;
    
    [SerializeField, Tooltip("Camera transform for look direction")]
    private Transform cameraTransform;

    // ===== CACHED PHYSICS DATA (updated in FixedUpdate) =====
    private Vector3 cachedVelocity;
    private Vector3 cachedPreviousVelocity;
    private Vector3 cachedAcceleration;
    private float cachedSpeed;
    private bool isGrounded;

    // ===== RUNTIME STATE: STRAFE TILT =====
    private float currentStrafeTilt;      // Current Z-axis tilt angle
    private float targetStrafeTilt;       // Target Z-axis tilt angle

    // ===== RUNTIME STATE: FORWARD TILT =====
    private float currentForwardTilt;     // Current X-axis tilt angle
    private float targetForwardTilt;      // Target X-axis tilt angle
    private float accelerationTiltTimer;  // Time since last acceleration impulse
    private float lastForwardSpeed;       // For acceleration detection

    // ===== RUNTIME STATE: LOOK TILT =====
    private float currentLookTilt;        // Current Z-axis tilt from looking
    private float targetLookTilt;         // Target Z-axis tilt from looking
    private float[] lookDeltaHistory;     // Circular buffer for look input
    private int lookHistoryIndex;         // Current buffer position
    private int lookHistoryCount;         // Number of valid entries
    private float lookDeltaSum;           // Sum of buffer values

    // ===== CACHED OUTPUT (zero allocation) =====
    private Quaternion tiltRotation;      // Combined rotation from all tilts
    private Vector3 eulerAngles;          // Cached euler for tilt calculation

    // ===== INITIALIZATION =====
    private void Awake()
    {
        // Validate references
        if (speedAnalyzer == null && playerRigidbody == null)
        {
            Debug.LogError("[CameraTilt] Need either SpeedAnalyzer OR PlayerRigidbody! Assign one in Inspector.", this);
        }
        
        if (inputReader == null)
        {
            Debug.LogWarning("[CameraTilt] PlayerInputReader not assigned. Strafe tilt won't work.", this);
        }
        
        if (cameraTransform == null)
        {
            // CORRECT:
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cameraTransform = cam.transform;
            }
            else
            {
                Debug.LogError("[CameraTilt] CameraTransform reference missing!", this);
            }
        }

        // Initialize look tilt history buffer
        int bufferSize = Mathf.CeilToInt(lookTiltWindow / Time.fixedDeltaTime) + 1;
        lookDeltaHistory = new float[bufferSize];
        lookHistoryIndex = 0;
        lookHistoryCount = 0;
        lookDeltaSum = 0f;

        // Initialize cached values
        cachedPreviousVelocity = Vector3.zero;
        tiltRotation = Quaternion.identity;
        eulerAngles = Vector3.zero;
    }

    // ===== PHYSICS STEP - Cache velocity and acceleration =====
    private void FixedUpdate()
    {
        // Get velocity from SpeedAnalyzer (preferred) or directly from Rigidbody
        if (speedAnalyzer != null)
        {
            cachedSpeed = speedAnalyzer.CurrentSpeed;
            // Reconstruct velocity vector from speed and direction (approximation)
            // Note: SpeedAnalyzer doesn't expose full velocity, so we use Rigidbody if available
            if (playerRigidbody != null)
            {
                cachedVelocity = playerRigidbody.linearVelocity;
            }
        }
        else if (playerRigidbody != null)
        {
            cachedVelocity = playerRigidbody.linearVelocity;
            Vector3 horizontalVelocity = new Vector3(cachedVelocity.x, 0f, cachedVelocity.z);
            cachedSpeed = horizontalVelocity.magnitude;
        }

        // Calculate acceleration (for forward/backward tilt)
        cachedAcceleration = (cachedVelocity - cachedPreviousVelocity) / Time.fixedDeltaTime;
        cachedPreviousVelocity = cachedVelocity;

        // Detect if grounded (if GroundCheck component exists)
        GroundCheck groundCheck = GetComponentInParent<GroundCheck>();
        if (groundCheck != null && playerRigidbody != null)
        {
            isGrounded = groundCheck.IsGrounded(playerRigidbody);
        }
        else
        {
            isGrounded = true; // Assume grounded if no GroundCheck
        }
    }

    // ===== CAMERA APPLICATION - Calculate and apply tilt =====
    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        // Calculate individual tilt components
        UpdateStrafeTilt();
        UpdateForwardTilt();
        UpdateLookTilt();

        // Combine all tilts into final rotation
        CombineTilts();

        // Apply to camera
        transform.localRotation = tiltRotation;
    }

    // ===== STRAFE TILT CALCULATION =====
    private void UpdateStrafeTilt()
    {
        if (inputReader == null)
        {
            targetStrafeTilt = 0f;
        }
        else
        {
            // Read horizontal input (-1 = left, +1 = right)
            float horizontalInput = inputReader.MoveInput.x;

            // Calculate base tilt (negative because tilting left means negative Z rotation)
            float baseTilt = -horizontalInput * maxStrafeTilt;

            // Reduce tilt when moving forward/backward (optional realism)
            float forwardInput = Mathf.Abs(inputReader.MoveInput.y);
            float reductionFactor = 1f - (forwardInput * forwardMovementReduction);
            
            targetStrafeTilt = baseTilt * reductionFactor;
        }

        // Smoothly interpolate to target (exponential decay for frame-rate independence)
        float lerpSpeed = targetStrafeTilt == 0f ? recoverySpeed : strafeTiltSpeed;
        float lerpFactor = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        currentStrafeTilt = Mathf.Lerp(currentStrafeTilt, targetStrafeTilt, lerpFactor);
    }

    // ===== FORWARD/BACKWARD TILT CALCULATION =====
    private void UpdateForwardTilt()
    {
        if (!isGrounded)
        {
            // No acceleration tilt while airborne
            accelerationTiltTimer = 0f;
            targetForwardTilt = 0f;
        }
        else
        {
            // Calculate forward speed component (project velocity onto camera forward)
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            Vector3 horizontalVelocity = new Vector3(cachedVelocity.x, 0f, cachedVelocity.z);
            float forwardSpeed = Vector3.Dot(horizontalVelocity, cameraForward);

            // Detect acceleration (change in forward speed)
            float acceleration = (forwardSpeed - lastForwardSpeed) / Time.deltaTime;
            lastForwardSpeed = forwardSpeed;

            // Trigger tilt impulse on significant acceleration
            if (Mathf.Abs(acceleration) > 3f) // Threshold for "significant" acceleration
            {
                accelerationTiltTimer = accelerationTiltDuration;
                
                // Positive acceleration (speeding up forward) → tilt down (negative X)
                // Negative acceleration (slowing down or going backward) → tilt up (positive X)
                if (acceleration > 0f)
                {
                    targetForwardTilt = -maxForwardTilt;
                }
                else
                {
                    targetForwardTilt = maxBackwardTilt;
                }
            }

            // Decay tilt over time
            if (accelerationTiltTimer > 0f)
            {
                accelerationTiltTimer -= Time.deltaTime;
                
                // Exponential decay back to neutral
                float decayFactor = accelerationTiltTimer / accelerationTiltDuration;
                targetForwardTilt *= decayFactor;
            }
            else
            {
                targetForwardTilt = 0f;
            }
        }

        // Smooth interpolation
        float lerpSpeed = targetForwardTilt == 0f ? recoverySpeed : forwardTiltSpeed;
        float lerpFactor = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        currentForwardTilt = Mathf.Lerp(currentForwardTilt, targetForwardTilt, lerpFactor);
    }

    // ===== LOOK TILT CALCULATION =====
    private void UpdateLookTilt()
    {
        if (inputReader == null)
        {
            targetLookTilt = 0f;
        }
        else
        {
            // Read horizontal look input (mouse delta X)
            float lookDelta = inputReader.LookInput.x;

            // Add to circular buffer
            if (lookHistoryCount < lookDeltaHistory.Length)
            {
                // Buffer not full yet
                lookDeltaSum += lookDelta;
                lookDeltaHistory[lookHistoryIndex] = lookDelta;
                lookHistoryCount++;
            }
            else
            {
                // Buffer full, replace oldest value
                lookDeltaSum -= lookDeltaHistory[lookHistoryIndex];
                lookDeltaHistory[lookHistoryIndex] = lookDelta;
                lookDeltaSum += lookDelta;
            }

            // Advance circular buffer index
            lookHistoryIndex = (lookHistoryIndex + 1) % lookDeltaHistory.Length;

            // Calculate average look delta over time window
            float averageLookDelta = lookDeltaSum / lookHistoryCount;

            // Convert to tilt angle (negative because turning right should tilt right = negative Z)
            targetLookTilt = -averageLookDelta * lookTiltSensitivity;
            
            // Clamp to max
            targetLookTilt = Mathf.Clamp(targetLookTilt, -maxLookTilt, maxLookTilt);
        }

        // Smooth interpolation
        float lerpSpeed = targetLookTilt == 0f ? recoverySpeed : lookTiltSpeed;
        float lerpFactor = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
        currentLookTilt = Mathf.Lerp(currentLookTilt, targetLookTilt, lerpFactor);
    }

    // ===== COMBINE ALL TILTS =====
    private void CombineTilts()
    {
        // Combine Z-axis tilts (strafe + look)
        float combinedZTilt = currentStrafeTilt + currentLookTilt;

        // X-axis tilt (forward/backward)
        float xTilt = currentForwardTilt;

        // Build final rotation (NO ALLOCATION - uses cached eulerAngles)
        eulerAngles.Set(xTilt, 0f, combinedZTilt);
        tiltRotation = Quaternion.Euler(eulerAngles);
    }

    // ===== PUBLIC API =====
    
    /// <summary>
    /// Get current combined tilt rotation
    /// </summary>
    public Quaternion GetTiltRotation()
    {
        return tiltRotation;
    }

    /// <summary>
    /// Get individual tilt values for debugging
    /// </summary>
    public Vector3 GetTiltAngles()
    {
        return new Vector3(currentForwardTilt, 0f, currentStrafeTilt + currentLookTilt);
    }

    /// <summary>
    /// Reset all tilts immediately (useful for teleports, cutscenes)
    /// </summary>
    public void ResetTiltImmediate()
    {
        currentStrafeTilt = 0f;
        targetStrafeTilt = 0f;
        currentForwardTilt = 0f;
        targetForwardTilt = 0f;
        currentLookTilt = 0f;
        targetLookTilt = 0f;
        accelerationTiltTimer = 0f;
        
        // Clear look history
        lookDeltaSum = 0f;
        lookHistoryCount = 0;
        lookHistoryIndex = 0;
        
        tiltRotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Set strafe tilt enabled/disabled at runtime
    /// </summary>
    public void SetStrafeTiltEnabled(bool enabled)
    {
        if (!enabled)
        {
            targetStrafeTilt = 0f;
        }
    }

    /// <summary>
    /// Set look tilt enabled/disabled at runtime
    /// </summary>
    public void SetLookTiltEnabled(bool enabled)
    {
        if (!enabled)
        {
            targetLookTilt = 0f;
            lookDeltaSum = 0f;
            lookHistoryCount = 0;
        }
    }

    // ===== DEBUG VISUALIZATION =====
    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        // Debug display (bottom-right corner)
        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(10, 10, 10, 10);

        float x = Screen.width - 250;
        float y = Screen.height - 150;
        float lineHeight = 20;

        GUI.Label(new Rect(x, y, 240, lineHeight), $"Strafe Tilt: {currentStrafeTilt:F2}°", style);
        y += lineHeight;
        
        GUI.Label(new Rect(x, y, 240, lineHeight), $"Forward Tilt: {currentForwardTilt:F2}°", style);
        y += lineHeight;
        
        GUI.Label(new Rect(x, y, 240, lineHeight), $"Look Tilt: {currentLookTilt:F2}°", style);
        y += lineHeight;
        
        Vector3 combined = GetTiltAngles();
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(x, y, 240, lineHeight), $"Combined: X={combined.x:F1}° Z={combined.z:F1}°", style);
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || cameraTransform == null) return;

        Vector3 origin = transform.position;

        // Draw tilt axes
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, transform.right * 0.5f); // X-axis (forward tilt)
        
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(origin, transform.forward * 0.5f); // Z-axis (strafe/look tilt)

        // Draw tilt magnitude as sphere
        float totalTilt = Mathf.Abs(currentStrafeTilt) + Mathf.Abs(currentForwardTilt) + Mathf.Abs(currentLookTilt);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin + Vector3.up * 0.3f, totalTilt * 0.02f);
    }
    #endif
}