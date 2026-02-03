using UnityEngine;

/// <summary>
/// Central speed and movement analyzer for camera effects.
/// Provides consistent, frame-rate independent data about player movement state.
/// Other camera components query this instead of reading physics directly.
/// - Caches velocity data from physics step
/// - Analyzes movement direction and alignment
/// - Detects turning behavior
/// - Zero allocations, deterministic output
/// </summary>
public class SpeedBasedEffects : MonoBehaviour
{
    [Header("Speed References")]
    [SerializeField, Range(1f, 10f), Tooltip("Speed considered 'walking' (reference point)")]
    private float walkSpeed = 7f;
    
    [SerializeField, Range(5f, 20f), Tooltip("Speed considered 'sprinting' (max reference)")]
    private float sprintSpeed = 10f;
    
    [SerializeField, Range(0.1f, 2f), Tooltip("Minimum speed to register as 'moving'")]
    private float minMovementSpeed = 0.5f;

    [Header("Directional Analysis")]
    [SerializeField, Range(5f, 30f), Tooltip("Angle (degrees) within which movement is 'straight ahead'")]
    private float straightAngleThreshold = 15f;
    
    [SerializeField, Range(0.05f, 0.5f), Tooltip("How long to maintain 'straight' state after breaking alignment")]
    private float straightStateHysteresis = 0.2f;

    [Header("Turn Detection")]
    [SerializeField, Range(20f, 120f), Tooltip("Turn rate (deg/sec) to register as 'turning'")]
    private float turnRateThreshold = 45f;
    
    [SerializeField, Range(0.05f, 0.3f), Tooltip("Time window for turn rate calculation")]
    private float turnRateWindow = 0.1f;

    [Header("References")]
    [SerializeField, Tooltip("Player Rigidbody (for velocity)")]
    private Rigidbody playerRigidbody;
    
    [SerializeField, Tooltip("Camera transform (for forward direction)")]
    private Transform cameraTransform;

    // ===== PUBLIC PROPERTIES (read by other components) =====
    /// <summary>Current horizontal speed in m/s</summary>
    public float CurrentSpeed { get; private set; }
    
    /// <summary>Speed as ratio (0 = stopped, 1 = sprint speed or higher)</summary>
    public float SpeedRatio { get; private set; }
    
    /// <summary>How aligned is movement with camera forward? (0 = perpendicular, 1 = straight ahead, -1 = backward)</summary>
    public float DirectionalAlignment { get; private set; }
    
    /// <summary>Current turn rate in degrees/second</summary>
    public float TurnRate { get; private set; }
    
    /// <summary>Is the player moving straight ahead? (within threshold angle)</summary>
    public bool IsMovingStraight { get; private set; }
    
    /// <summary>Is the player turning sharply?</summary>
    public bool IsTurning { get; private set; }
    
    /// <summary>Is the player moving at all?</summary>
    public bool IsMoving { get; private set; }
    
    /// <summary>Is the player moving backward?</summary>
    public bool IsMovingBackward { get; private set; }
    
    /// <summary>Lateral movement ratio (-1 = left, 0 = forward/back, 1 = right)</summary>
    public float LateralMovement { get; private set; }

    // ===== CACHED PHYSICS DATA (updated in FixedUpdate) =====
    private Vector3 cachedVelocity;
    private Vector3 cachedHorizontalVelocity;
    private float cachedSpeed;

    // ===== RUNTIME STATE =====
    private float straightStateTimer;          // Time spent in straight state (for hysteresis)
    private bool wasStraightLastFrame;         // Previous frame state
    private float lastCameraYRotation;         // Previous rotation for delta calculation
    private float[] rotationHistory;           // Circular buffer for turn rate smoothing
    private int rotationHistoryIndex;          // Current position in buffer
    private float rotationHistorySum;          // Sum of values in buffer (for average)
    private int rotationHistoryCount;          // Number of valid entries

    // ===== CACHED CALCULATIONS (zero allocation) =====
    private Vector3 cameraForwardFlat;         // Camera forward with Y=0
    private Vector3 cameraRightFlat;           // Camera right with Y=0
    private Vector3 velocityDirection;         // Normalized horizontal velocity

    // ===== CONSTANTS =====
    private float straightAlignmentThreshold;  // Cached cosine of straight angle

    // ===== INITIALIZATION =====
    private void Awake()
    {
        // Validate references
        if (playerRigidbody == null)
        {
            Debug.LogError("[SpeedBasedEffects] PlayerRigidbody reference missing! Assign in Inspector.", this);
        }
        
        if (cameraTransform == null)
        {
            // Try to find camera in children
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cameraTransform = cam.transform;
                Debug.LogWarning("[SpeedBasedEffects] CameraTransform auto-assigned from children.", this);
            }
            else
            {
                Debug.LogError("[SpeedBasedEffects] CameraTransform reference missing! Assign in Inspector.", this);
            }
        }

        // Pre-calculate alignment threshold (angle to cosine)
        straightAlignmentThreshold = Mathf.Cos(straightAngleThreshold * Mathf.Deg2Rad);

        // Initialize turn rate history buffer
        int bufferSize = Mathf.CeilToInt(turnRateWindow / Time.fixedDeltaTime) + 1;
        rotationHistory = new float[bufferSize];
        rotationHistoryIndex = 0;
        rotationHistoryCount = 0;
        rotationHistorySum = 0f;

        // Initialize camera rotation tracking
        if (cameraTransform != null)
        {
            lastCameraYRotation = cameraTransform.eulerAngles.y;
        }
    }

    // ===== PHYSICS STEP - Cache velocity data =====
    private void FixedUpdate()
    {
        if (playerRigidbody == null) return;

        // Cache velocity (read once per physics step)
        cachedVelocity = playerRigidbody.linearVelocity;
        
        // Calculate horizontal velocity (ignore Y component)
        cachedHorizontalVelocity.Set(cachedVelocity.x, 0f, cachedVelocity.z);
        cachedSpeed = cachedHorizontalVelocity.magnitude;

        // Update public speed properties
        CurrentSpeed = cachedSpeed;
        SpeedRatio = Mathf.Clamp01((cachedSpeed - minMovementSpeed) / (sprintSpeed - minMovementSpeed));
        IsMoving = cachedSpeed >= minMovementSpeed;
    }

    // ===== ANALYSIS - Calculate movement characteristics =====
    private void LateUpdate()
    {
        if (playerRigidbody == null || cameraTransform == null) return;

        // Update directional analysis
        UpdateDirectionalAlignment();
        
        // Update turn rate
        UpdateTurnRate();
        
        // Update movement state flags
        UpdateMovementStates();
    }

    // ===== DIRECTIONAL ALIGNMENT CALCULATION =====
    private void UpdateDirectionalAlignment()
    {
        if (!IsMoving)
        {
            DirectionalAlignment = 0f;
            IsMovingStraight = false;
            straightStateTimer = 0f;
            wasStraightLastFrame = false;
            return;
        }

        // Get camera forward direction (ignore Y, normalize)
        cameraForwardFlat.Set(cameraTransform.forward.x, 0f, cameraTransform.forward.z);
        cameraForwardFlat.Normalize();

        // Get camera right direction (for lateral movement calculation)
        cameraRightFlat.Set(cameraTransform.right.x, 0f, cameraTransform.right.z);
        cameraRightFlat.Normalize();

        // Get velocity direction
        velocityDirection = cachedHorizontalVelocity.normalized;

        // Calculate alignment (dot product)
        DirectionalAlignment = Vector3.Dot(cameraForwardFlat, velocityDirection);

        // Calculate lateral movement (-1 = left, +1 = right)
        LateralMovement = Vector3.Dot(cameraRightFlat, velocityDirection);

        // Determine if moving straight (with hysteresis for stability)
        bool isCurrentlyStraight = DirectionalAlignment >= straightAlignmentThreshold;

        if (isCurrentlyStraight)
        {
            straightStateTimer += Time.deltaTime;
            IsMovingStraight = true;
            wasStraightLastFrame = true;
        }
        else
        {
            // Hysteresis: maintain "straight" state briefly after breaking alignment
            if (wasStraightLastFrame)
            {
                straightStateTimer -= Time.deltaTime;
                
                if (straightStateTimer <= 0f)
                {
                    IsMovingStraight = false;
                    wasStraightLastFrame = false;
                    straightStateTimer = 0f;
                }
                else
                {
                    IsMovingStraight = true; // Still in hysteresis window
                }
            }
            else
            {
                IsMovingStraight = false;
                straightStateTimer = 0f;
            }
        }

        // Clamp timer to hysteresis duration
        straightStateTimer = Mathf.Clamp(straightStateTimer, 0f, straightStateHysteresis);
    }

    // ===== TURN RATE CALCULATION =====
    private void UpdateTurnRate()
    {
        float currentYRotation = cameraTransform.eulerAngles.y;
        
        // Calculate rotation delta (handle 360° wrap-around)
        float deltaRotation = Mathf.DeltaAngle(lastCameraYRotation, currentYRotation);
        
        // Add to circular buffer
        if (rotationHistoryCount < rotationHistory.Length)
        {
            // Buffer not full yet
            rotationHistorySum += deltaRotation;
            rotationHistory[rotationHistoryIndex] = deltaRotation;
            rotationHistoryCount++;
        }
        else
        {
            // Buffer full, replace oldest value
            rotationHistorySum -= rotationHistory[rotationHistoryIndex];
            rotationHistory[rotationHistoryIndex] = deltaRotation;
            rotationHistorySum += deltaRotation;
        }

        // Advance circular buffer index
        rotationHistoryIndex = (rotationHistoryIndex + 1) % rotationHistory.Length;

        // Calculate average rotation delta over time window
        float averageDelta = rotationHistorySum / rotationHistoryCount;
        
        // Convert to degrees per second
        TurnRate = Mathf.Abs(averageDelta / Time.deltaTime);
        
        // Determine if turning sharply
        IsTurning = TurnRate >= turnRateThreshold;

        // Update for next frame
        lastCameraYRotation = currentYRotation;
    }

    // ===== MOVEMENT STATE FLAGS =====
    private void UpdateMovementStates()
    {
        // Determine if moving backward (alignment < -0.5 means more than 120° away from forward)
        IsMovingBackward = IsMoving && DirectionalAlignment < -0.5f;
    }

    // ===== PUBLIC UTILITY METHODS =====
    
    /// <summary>
    /// Get speed as a percentage of walk speed (0 = stopped, 1 = walk speed, >1 = faster than walk)
    /// </summary>
    public float GetWalkSpeedRatio()
    {
        return CurrentSpeed / walkSpeed;
    }

    /// <summary>
    /// Get speed as a percentage of sprint speed (0 = stopped, 1 = sprint speed, >1 = faster than sprint)
    /// </summary>
    public float GetSprintSpeedRatio()
    {
        return CurrentSpeed / sprintSpeed;
    }

    /// <summary>
    /// Is player strafing? (moving perpendicular to camera forward)
    /// </summary>
    public bool IsStrafing()
    {
        return IsMoving && Mathf.Abs(LateralMovement) > 0.7f; // ~45° cone on each side
    }

    /// <summary>
    /// Get absolute lateral movement (0 = forward/back, 1 = pure strafe)
    /// </summary>
    public float GetStrafeIntensity()
    {
        return Mathf.Abs(LateralMovement);
    }

    /// <summary>
    /// Reset turn rate history (useful after teleports, cutscenes, etc.)
    /// </summary>
    public void ResetTurnRateHistory()
    {
        rotationHistorySum = 0f;
        rotationHistoryCount = 0;
        rotationHistoryIndex = 0;
        TurnRate = 0f;
        IsTurning = false;
        
        if (cameraTransform != null)
        {
            lastCameraYRotation = cameraTransform.eulerAngles.y;
        }
    }

    // ===== DEBUG VISUALIZATION =====
    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        // Create debug display (top-right corner)
        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(10, 10, 10, 10);

        float x = Screen.width - 250;
        float y = 10;
        float lineHeight = 20;

        GUI.Label(new Rect(x, y, 240, lineHeight), $"Speed: {CurrentSpeed:F2} m/s", style);
        y += lineHeight;
        
        GUI.Label(new Rect(x, y, 240, lineHeight), $"Speed Ratio: {SpeedRatio:F2}", style);
        y += lineHeight;
        
        style.normal.textColor = IsMovingStraight ? Color.green : Color.white;
        GUI.Label(new Rect(x, y, 240, lineHeight), $"Alignment: {DirectionalAlignment:F2}", style);
        y += lineHeight;
        
        style.normal.textColor = IsTurning ? Color.yellow : Color.white;
        GUI.Label(new Rect(x, y, 240, lineHeight), $"Turn Rate: {TurnRate:F1} °/s", style);
        y += lineHeight;
        
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y, 240, lineHeight), $"Lateral: {LateralMovement:F2}", style);
        y += lineHeight;

        // Status flags
        string flags = "";
        if (IsMoving) flags += "[Moving] ";
        if (IsMovingStraight) flags += "[Straight] ";
        if (IsTurning) flags += "[Turning] ";
        if (IsMovingBackward) flags += "[Backward] ";
        if (IsStrafing()) flags += "[Strafing] ";
        
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(x, y, 240, lineHeight * 2), flags, style);
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !IsMoving || cameraTransform == null) return;

        Vector3 origin = transform.position;

        // Draw camera forward (blue)
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(origin, cameraForwardFlat * 2f);

        // Draw velocity direction (green for straight, red for not)
        Gizmos.color = IsMovingStraight ? Color.green : Color.red;
        Gizmos.DrawRay(origin, velocityDirection * 2f);

        // Draw lateral movement indicator (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, cameraRightFlat * LateralMovement * 1.5f);

        // Draw turn rate indicator (sphere size = turn speed)
        Gizmos.color = IsTurning ? Color.magenta : new Color(1f, 0.5f, 0f, 0.5f);
        float sphereSize = Mathf.Clamp(TurnRate / 100f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(origin + Vector3.up * 1.5f, sphereSize);
    }
    #endif
}