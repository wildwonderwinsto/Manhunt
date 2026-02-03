using UnityEngine;

/// <summary>
/// Procedural head bobbing system for first-person camera.
/// - Frequency and amplitude scale with movement speed
/// - Intensity boost when moving straight (momentum reward)
/// - Dampens during sharp turns for realistic feel
/// - Frame-rate independent, zero allocations
/// </summary>
public class ViewBobbing : MonoBehaviour
{
    [Header("Speed Analyzer (Optional)")]
[SerializeField] private SpeedBasedEffects speedAnalyzer;
    [Header("Bob Settings")]
    [SerializeField, Range(1f, 4f), Tooltip("Base bob frequency in Hz (cycles per second)")]
    private float baseFrequency = 2.0f;
    
    [SerializeField, Range(0.01f, 0.15f), Tooltip("Base vertical bob amplitude in world units")]
    private float baseAmplitude = 0.04f;
    
    [SerializeField, Range(0.3f, 0.7f), Tooltip("Horizontal bob as ratio of vertical (0.5 = half)")]
    private float horizontalRatio = 0.5f;
    
    [SerializeField, Range(1.0f, 2.5f), Tooltip("Frequency multiplier when sprinting")]
    private float sprintFrequencyMultiplier = 1.4f;
    
    [SerializeField, Range(1.0f, 2.5f), Tooltip("Amplitude multiplier when sprinting")]
    private float sprintAmplitudeMultiplier = 1.3f;

    [Header("Intensity Modulation")]
    [SerializeField, Range(1.0f, 2.0f), Tooltip("Intensity boost when moving straight")]
    private float straightLineBonus = 1.3f;
    
    [SerializeField, Range(0.3f, 0.9f), Tooltip("Intensity reduction during sharp turns")]
    private float turnDampening = 0.7f;
    
    [SerializeField, Range(0.1f, 1.0f), Tooltip("Time to ramp up straight-line bonus")]
    private float bonusRampTime = 0.5f;
    
    [SerializeField, Range(10f, 180f), Tooltip("Turn rate threshold (degrees/sec) to trigger dampening")]
    private float sharpTurnThreshold = 45f;

    [Header("Speed Thresholds")]
    [SerializeField, Range(0.5f, 5f), Tooltip("Minimum speed to start bobbing")]
    private float minSpeedThreshold = 1.0f;
    
    [SerializeField, Range(5f, 12f), Tooltip("Speed considered 'walking' (reference for scaling)")]
    private float walkSpeed = 7f;
    
    [SerializeField, Range(8f, 20f), Tooltip("Speed considered 'sprinting' (max scaling)")]
    private float sprintSpeed = 10f;

    [Header("Smoothing")]
    [SerializeField, Range(1f, 20f), Tooltip("How quickly bob intensity changes (higher = snappier)")]
    private float intensityTransitionSpeed = 8f;

    [Header("References")]
    [SerializeField, Tooltip("Assign the player's Rigidbody")]
    private Rigidbody playerRigidbody;
    
    [SerializeField, Tooltip("Assign the camera transform (for forward direction)")]
    private Transform cameraTransform;

    // ===== CACHED PHYSICS DATA (updated in FixedUpdate) =====
    private float cachedSpeed;
    private Vector3 cachedVelocity;
    private Vector3 cachedHorizontalVelocity;

    // ===== RUNTIME STATE =====
    private float bobTimer;                    // Accumulated time for sine wave
    private float currentIntensity = 1f;       // Current intensity multiplier (0-2+)
    private float targetIntensity = 1f;        // Target intensity we're lerping toward
    private float straightLineTimer;           // Time spent moving straight
    private float lastYRotation;               // Previous frame's camera Y rotation
    private bool wasMovingStraight;            // Previous frame state for hysteresis

    // ===== CACHED OUTPUT (zero allocation) =====
    private Vector3 bobOffset;                 // Final position offset
    private Vector3 cachedForward;             // Camera forward (Y=0)
    private Vector3 cachedVelocityDirection;   // Velocity direction (Y=0)

    // ===== INITIALIZATION =====
    private void Awake()
    {
        // Validate references
        if (playerRigidbody == null)
        {
            Debug.LogError("[ViewBobbing] PlayerRigidbody reference missing! Assign in Inspector.", this);
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
                Debug.LogError("[ViewBobbing] CameraTransform reference missing! Assign in Inspector.", this);
            }
        }

        // Initialize state
        lastYRotation = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
    }

    // ===== PHYSICS STEP - Cache velocity data =====
    private void FixedUpdate()
    {
        if (playerRigidbody == null) return;

        // Cache velocity (read once per physics step)
        cachedVelocity = playerRigidbody.linearVelocity;
        
        // Calculate horizontal speed (ignore Y component)
        cachedHorizontalVelocity.Set(cachedVelocity.x, 0f, cachedVelocity.z);
        cachedSpeed = cachedHorizontalVelocity.magnitude;
    }

    // ===== CAMERA APPLICATION - Calculate and apply bob =====
    private void LateUpdate()
    {
        if (playerRigidbody == null || cameraTransform == null)
        {
            bobOffset = Vector3.zero;
            return;
        }

        // Early exit if moving too slowly
        if (cachedSpeed < minSpeedThreshold)
        {
            ResetBob();
            return;
        }

        // Update intensity based on movement conditions
        UpdateIntensity();

        // Calculate bob parameters
        float speedRatio = Mathf.Clamp01((cachedSpeed - minSpeedThreshold) / (sprintSpeed - minSpeedThreshold));
        float currentFrequency = baseFrequency * Mathf.Lerp(1f, sprintFrequencyMultiplier, speedRatio);
        float currentAmplitude = baseAmplitude * Mathf.Lerp(1f, sprintAmplitudeMultiplier, speedRatio);

        // Apply intensity modulation
        currentAmplitude *= currentIntensity;

        // Advance bob timer (frame-rate independent)
        bobTimer += Time.deltaTime * currentFrequency;

        // Calculate bob offset using sine/cosine waves
        float verticalBob = Mathf.Sin(bobTimer * Mathf.PI * 2f) * currentAmplitude;
        float horizontalBob = Mathf.Cos(bobTimer * Mathf.PI) * currentAmplitude * horizontalRatio;

        // Apply to cached output (NO ALLOCATION)
        bobOffset.Set(horizontalBob, verticalBob, 0f);

        // Apply to camera (local space)
        transform.localPosition = bobOffset;
    }

    // ===== INTENSITY CALCULATION =====
    private void UpdateIntensity()
    {
        // Calculate directional alignment (moving toward where we're looking?)
        float alignment = CalculateDirectionalAlignment();

        // Calculate turn rate (how fast are we rotating the camera?)
        float turnRate = CalculateTurnRate();

        // Determine target intensity based on conditions
        bool isMovingStraight = alignment > 0.966f; // ~15 degree cone
        bool isTurningSharply = turnRate > sharpTurnThreshold;

        // STATE 1: Moving straight → build up bonus intensity
        if (isMovingStraight)
        {
            straightLineTimer += Time.deltaTime;
            float bonusProgress = Mathf.Clamp01(straightLineTimer / bonusRampTime);
            targetIntensity = Mathf.Lerp(1f, straightLineBonus, bonusProgress);
            wasMovingStraight = true;
        }
        // STATE 2: Sharp turn → dampen intensity
        else if (isTurningSharply)
        {
            straightLineTimer = 0f;
            targetIntensity = turnDampening;
            wasMovingStraight = false;
        }
        // STATE 3: Normal movement → neutral intensity
        else
        {
            // Hysteresis: decay straight-line bonus gradually
            if (wasMovingStraight)
            {
                straightLineTimer = Mathf.Max(0f, straightLineTimer - Time.deltaTime * 2f);
                float bonusProgress = Mathf.Clamp01(straightLineTimer / bonusRampTime);
                targetIntensity = Mathf.Lerp(1f, straightLineBonus, bonusProgress);
                
                if (straightLineTimer <= 0f)
                {
                    wasMovingStraight = false;
                }
            }
            else
            {
                targetIntensity = 1f;
            }
        }

        // Smoothly transition to target intensity (exponential decay)
        float lerpFactor = 1f - Mathf.Exp(-intensityTransitionSpeed * Time.deltaTime);
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, lerpFactor);
    }

    // ===== HELPER: Calculate how aligned velocity is with camera forward =====
    private float CalculateDirectionalAlignment()
    {
        if (cachedSpeed < 0.1f) return 0f;

        // Get camera forward direction (ignore Y)
        cachedForward.Set(cameraTransform.forward.x, 0f, cameraTransform.forward.z);
        cachedForward.Normalize();

        // Get velocity direction (ignore Y)
        cachedVelocityDirection = cachedHorizontalVelocity.normalized;

        // Dot product gives alignment (-1 to 1)
        return Vector3.Dot(cachedForward, cachedVelocityDirection);
    }

    // ===== HELPER: Calculate camera rotation speed =====
    private float CalculateTurnRate()
    {
        float currentYRotation = cameraTransform.eulerAngles.y;
        
        // Handle 360° wrap-around (359° → 1° should be +2°, not -358°)
        float deltaRotation = Mathf.DeltaAngle(lastYRotation, currentYRotation);
        
        // Convert to degrees per second
        float turnRate = Mathf.Abs(deltaRotation / Time.deltaTime);
        
        // Update for next frame
        lastYRotation = currentYRotation;
        
        return turnRate;
    }

    // ===== RESET: Smoothly return to zero when stopped =====
    private void ResetBob()
    {
        // Decay intensity back to neutral
        targetIntensity = 1f;
        float lerpFactor = 1f - Mathf.Exp(-intensityTransitionSpeed * Time.deltaTime);
        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, lerpFactor);

        // Reset timers
        straightLineTimer = Mathf.Max(0f, straightLineTimer - Time.deltaTime * 2f);
        
        // Smoothly bring bob offset back to zero
        bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, Time.deltaTime * 10f);
        transform.localPosition = bobOffset;

        // Slow down bob timer (creates smooth stop)
        bobTimer += Time.deltaTime * baseFrequency * 0.3f;
    }

    // ===== PUBLIC API =====
    /// <summary>
    /// Get the current bob offset (for debugging or external use)
    /// </summary>
    public Vector3 GetBobOffset()
    {
        return bobOffset;
    }

    /// <summary>
    /// Get current intensity multiplier (for debugging)
    /// </summary>
    public float GetCurrentIntensity()
    {
        return currentIntensity;
    }

    /// <summary>
    /// Reset bob state immediately (useful for teleports, cutscenes, etc.)
    /// </summary>
    public void ResetBobImmediate()
    {
        bobTimer = 0f;
        currentIntensity = 1f;
        targetIntensity = 1f;
        straightLineTimer = 0f;
        bobOffset = Vector3.zero;
        transform.localPosition = Vector3.zero;
    }

    // ===== DEBUG VISUALIZATION =====
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || cameraTransform == null) return;

        // Visualize forward direction
        Gizmos.color = Color.blue;
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        Gizmos.DrawRay(transform.position, forward.normalized * 2f);

        // Visualize velocity direction
        if (cachedSpeed > 0.1f)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, cachedVelocityDirection * 2f);
        }

        // Draw intensity indicator
        Gizmos.color = Color.Lerp(Color.red, Color.yellow, currentIntensity / 2f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, currentIntensity * 0.1f);
    }
    #endif
}
