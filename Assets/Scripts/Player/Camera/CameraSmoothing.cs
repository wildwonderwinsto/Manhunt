using UnityEngine;

/// <summary>
/// Optional smoothing layer for camera effects.
/// Applies spring damping to camera position and rotation to add "weight" and prevent jarring movements.
/// - Smooths combined output from all camera effects (bobbing, tilt, impact)
/// - Prevents sudden snaps during state transitions
/// - Acts as camera inertia/lag for more cinematic feel
/// - Completely optional - disable by setting smooth speeds to high values (20+)
/// - Frame-rate independent, zero allocations
/// </summary>
public class CameraSmoothing : MonoBehaviour
{
    [Header("Position Smoothing")]
    [SerializeField, Tooltip("Enable position smoothing?")]
    private bool enablePositionSmoothing = true;
    
    [SerializeField, Range(1f, 30f), Tooltip("Position smoothing speed (higher = less smoothing, 20+ = nearly instant)")]
    private float positionSmoothSpeed = 15f;
    
    [SerializeField, Range(0f, 0.1f), Tooltip("Max position offset smoothing can add (prevents excessive lag)")]
    private float maxPositionLag = 0.05f;

    [Header("Rotation Smoothing")]
    [SerializeField, Tooltip("Enable rotation smoothing?")]
    private bool enableRotationSmoothing = true;
    
    [SerializeField, Range(1f, 30f), Tooltip("Rotation smoothing speed (higher = less smoothing, 20+ = nearly instant)")]
    private float rotationSmoothSpeed = 20f;
    
    [SerializeField, Range(0f, 15f), Tooltip("Max rotation offset smoothing can add in degrees (prevents excessive lag)")]
    private float maxRotationLag = 5f;

    [Header("Adaptive Smoothing (Optional)")]
    [SerializeField, Tooltip("Enable adaptive smoothing based on movement?")]
    private bool useAdaptiveSmoothing = false;
    
    [SerializeField, Range(1f, 10f), Tooltip("Smoothing multiplier when moving fast (reduces lag during action)")]
    private float movementSpeedMultiplier = 2f;
    
    [SerializeField, Range(1f, 15f), Tooltip("Speed threshold for adaptive smoothing (m/s)")]
    private float movementSpeedThreshold = 8f;

    [Header("References")]
    [SerializeField, Tooltip("SpeedBasedEffects component (optional, for adaptive smoothing)")]
    private SpeedBasedEffects speedAnalyzer;

    // ===== TARGET TRANSFORM (what we're smoothing toward) =====
    private Vector3 targetLocalPosition;
    private Quaternion targetLocalRotation;

    // ===== SMOOTHED OUTPUT (our current smoothed values) =====
    private Vector3 smoothedLocalPosition;
    private Quaternion smoothedLocalRotation;

    // ===== CACHED CALCULATIONS (zero allocation) =====
    private Vector3 positionVelocity;        // For SmoothDamp (if we used it)
    private float currentPositionSpeed;      // Actual smoothing speed this frame
    private float currentRotationSpeed;      // Actual smoothing speed this frame

    // ===== CHILD TRANSFORM (the actual camera hierarchy below us) =====
    private Transform childTransform;
    private Vector3 childOriginalLocalPosition;
    private Quaternion childOriginalLocalRotation;

    // ===== INITIALIZATION =====
    private void Awake()
    {
        // Get the child transform (the next level down in hierarchy)
        if (transform.childCount > 0)
        {
            childTransform = transform.GetChild(0);
            childOriginalLocalPosition = childTransform.localPosition;
            childOriginalLocalRotation = childTransform.localRotation;
        }
        else
        {
            Debug.LogWarning("[CameraSmoothing] No child transform found! This component needs to be above the camera in hierarchy.", this);
        }

        // Initialize smoothed values to current transform
        smoothedLocalPosition = transform.localPosition;
        smoothedLocalRotation = transform.localRotation;

        // Try to find SpeedAnalyzer if not assigned and adaptive smoothing is enabled
        if (useAdaptiveSmoothing && speedAnalyzer == null)
        {
            speedAnalyzer = GetComponentInParent<SpeedBasedEffects>();
            if (speedAnalyzer == null)
            {
                Debug.LogWarning("[CameraSmoothing] Adaptive smoothing enabled but SpeedAnalyzer not found. Using fixed smoothing.", this);
                useAdaptiveSmoothing = false;
            }
        }
    }

    // ===== LATE UPDATE - Apply smoothing AFTER all other camera effects =====
    private void LateUpdate()
    {
        if (childTransform == null) return;

        // Capture target transform (what child components want)
        CaptureTargetTransform();

        // Calculate adaptive smoothing speed (if enabled)
        CalculateSmoothingSpeeds();

        // Apply smoothing
        SmoothPosition();
        SmoothRotation();

        // Apply smoothed values to our transform
        transform.localPosition = smoothedLocalPosition;
        transform.localRotation = smoothedLocalRotation;

        // Reset child to original local transform (so effects below us work correctly)
        childTransform.localPosition = childOriginalLocalPosition;
        childTransform.localRotation = childOriginalLocalRotation;
    }

    // ===== CAPTURE TARGET TRANSFORM =====
    private void CaptureTargetTransform()
    {
        // The child's world transform is what we want to smooth toward
        // We need to convert it to our local space
        
        // Get child's desired world position/rotation (already affected by all effects below)
        Vector3 childWorldPosition = childTransform.position;
        Quaternion childWorldRotation = childTransform.rotation;

        // Convert to our local space (what our localPosition/localRotation should be)
        Transform parent = transform.parent;
        if (parent != null)
        {
            targetLocalPosition = parent.InverseTransformPoint(childWorldPosition);
            targetLocalRotation = Quaternion.Inverse(parent.rotation) * childWorldRotation;
        }
        else
        {
            targetLocalPosition = childWorldPosition;
            targetLocalRotation = childWorldRotation;
        }
    }

    // ===== CALCULATE ADAPTIVE SMOOTHING SPEEDS =====
    private void CalculateSmoothingSpeeds()
    {
        currentPositionSpeed = positionSmoothSpeed;
        currentRotationSpeed = rotationSmoothSpeed;

        if (!useAdaptiveSmoothing || speedAnalyzer == null) return;

        // If moving fast, reduce smoothing (make camera more responsive)
        if (speedAnalyzer.CurrentSpeed >= movementSpeedThreshold)
        {
            float speedRatio = speedAnalyzer.CurrentSpeed / movementSpeedThreshold;
            float multiplier = Mathf.Lerp(1f, movementSpeedMultiplier, Mathf.Clamp01(speedRatio - 1f));
            
            currentPositionSpeed *= multiplier;
            currentRotationSpeed *= multiplier;
        }
    }

    // ===== SMOOTH POSITION =====
    private void SmoothPosition()
    {
        if (!enablePositionSmoothing)
        {
            smoothedLocalPosition = targetLocalPosition;
            return;
        }

        // Exponential decay smoothing (frame-rate independent)
        float lerpFactor = 1f - Mathf.Exp(-currentPositionSpeed * Time.deltaTime);
        smoothedLocalPosition = Vector3.Lerp(smoothedLocalPosition, targetLocalPosition, lerpFactor);

        // Clamp maximum lag distance (prevents excessive camera lag)
        Vector3 lagOffset = smoothedLocalPosition - targetLocalPosition;
        float lagDistance = lagOffset.magnitude;
        
        if (lagDistance > maxPositionLag)
        {
            smoothedLocalPosition = targetLocalPosition + lagOffset.normalized * maxPositionLag;
        }
    }

    // ===== SMOOTH ROTATION =====
    private void SmoothRotation()
    {
        if (!enableRotationSmoothing)
        {
            smoothedLocalRotation = targetLocalRotation;
            return;
        }

        // Exponential decay smoothing for rotation (frame-rate independent)
        float lerpFactor = 1f - Mathf.Exp(-currentRotationSpeed * Time.deltaTime);
        smoothedLocalRotation = Quaternion.Slerp(smoothedLocalRotation, targetLocalRotation, lerpFactor);

        // Clamp maximum rotation lag (prevents excessive delay)
        float angularDistance = Quaternion.Angle(smoothedLocalRotation, targetLocalRotation);
        
        if (angularDistance > maxRotationLag)
        {
            float clampedAngle = maxRotationLag / angularDistance;
            smoothedLocalRotation = Quaternion.Slerp(targetLocalRotation, smoothedLocalRotation, clampedAngle);
        }
    }

    // ===== PUBLIC API =====

    /// <summary>
    /// Get current smoothing lag in position (world units)
    /// </summary>
    public float GetPositionLag()
    {
        return Vector3.Distance(smoothedLocalPosition, targetLocalPosition);
    }

    /// <summary>
    /// Get current smoothing lag in rotation (degrees)
    /// </summary>
    public float GetRotationLag()
    {
        return Quaternion.Angle(smoothedLocalRotation, targetLocalRotation);
    }

    /// <summary>
    /// Reset smoothing immediately (for teleports, cutscenes)
    /// </summary>
    public void ResetSmoothingImmediate()
    {
        if (childTransform != null)
        {
            CaptureTargetTransform();
        }
        
        smoothedLocalPosition = targetLocalPosition;
        smoothedLocalRotation = targetLocalRotation;
        
        transform.localPosition = smoothedLocalPosition;
        transform.localRotation = smoothedLocalRotation;
    }

    /// <summary>
    /// Set position smoothing enabled/disabled at runtime
    /// </summary>
    public void SetPositionSmoothingEnabled(bool enabled)
    {
        enablePositionSmoothing = enabled;
        if (!enabled)
        {
            smoothedLocalPosition = targetLocalPosition;
        }
    }

    /// <summary>
    /// Set rotation smoothing enabled/disabled at runtime
    /// </summary>
    public void SetRotationSmoothingEnabled(bool enabled)
    {
        enableRotationSmoothing = enabled;
        if (!enabled)
        {
            smoothedLocalRotation = targetLocalRotation;
        }
    }

    /// <summary>
    /// Set smoothing speeds at runtime
    /// </summary>
    public void SetSmoothingSpeeds(float posSpeed, float rotSpeed)
    {
        positionSmoothSpeed = Mathf.Max(1f, posSpeed);
        rotationSmoothSpeed = Mathf.Max(1f, rotSpeed);
    }

    /// <summary>
    /// Enable/disable adaptive smoothing at runtime
    /// </summary>
    public void SetAdaptiveSmoothingEnabled(bool enabled)
    {
        if (enabled && speedAnalyzer == null)
        {
            Debug.LogWarning("[CameraSmoothing] Cannot enable adaptive smoothing - SpeedAnalyzer not found.");
            return;
        }
        useAdaptiveSmoothing = enabled;
    }

    // ===== DEBUG VISUALIZATION =====
    #if UNITY_EDITOR
    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        if (!enablePositionSmoothing && !enableRotationSmoothing) return;

        // Debug display (top-left corner)
        GUIStyle style = new GUIStyle();
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.padding = new RectOffset(10, 10, 10, 10);

        float x = 10;
        float y = 10;
        float lineHeight = 20;

        GUI.Label(new Rect(x, y, 240, lineHeight), "=== Camera Smoothing ===", style);
        y += lineHeight;

        if (enablePositionSmoothing)
        {
            float posLag = GetPositionLag();
            style.normal.textColor = posLag > maxPositionLag * 0.8f ? Color.yellow : Color.white;
            GUI.Label(new Rect(x, y, 240, lineHeight), $"Position Lag: {posLag * 1000f:F2}mm", style);
            y += lineHeight;
        }

        if (enableRotationSmoothing)
        {
            float rotLag = GetRotationLag();
            style.normal.textColor = rotLag > maxRotationLag * 0.8f ? Color.yellow : Color.white;
            GUI.Label(new Rect(x, y, 240, lineHeight), $"Rotation Lag: {rotLag:F2}°", style);
            y += lineHeight;
        }

        if (useAdaptiveSmoothing && speedAnalyzer != null)
        {
            style.normal.textColor = Color.cyan;
            GUI.Label(new Rect(x, y, 240, lineHeight), $"Speed: {speedAnalyzer.CurrentSpeed:F1} m/s", style);
            y += lineHeight;
            
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, 240, lineHeight), $"Smooth Speed: {currentPositionSpeed:F1}", style);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || childTransform == null) return;

        // Draw target position (where effects want camera to be)
        Gizmos.color = Color.green;
        Vector3 targetWorldPos = transform.parent != null 
            ? transform.parent.TransformPoint(targetLocalPosition)
            : targetLocalPosition;
        Gizmos.DrawWireSphere(targetWorldPos, 0.05f);

        // Draw smoothed position (where camera actually is)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.03f);

        // Draw lag vector
        if (enablePositionSmoothing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetWorldPos);
        }

        // Draw rotation difference
        if (enableRotationSmoothing)
        {
            Gizmos.color = Color.cyan;
            Vector3 smoothedForward = transform.forward;
            Vector3 targetForward = (transform.parent != null 
                ? transform.parent.rotation * targetLocalRotation 
                : targetLocalRotation) * Vector3.forward;
            
            Gizmos.DrawRay(transform.position, smoothedForward * 0.3f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, targetForward * 0.3f);
        }
    }
    #endif
}
