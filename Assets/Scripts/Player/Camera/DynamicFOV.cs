using UnityEngine;

public class DynamicFOV : MonoBehaviour
{
    [Header("FOV Settings")]
    [SerializeField] private float baseFOV = 60f;
    [SerializeField] private float sprintFOV = 70f;
    [SerializeField] private float maxFOV = 80f;
    [SerializeField] private float fovLerpSpeed = 10f;

    [Header("References")]
    [SerializeField] private Rigidbody playerRigidbody; // ✅ Assign in Inspector
    [SerializeField] private PlayerMovement playerMovement; // ✅ Assign in Inspector
    [SerializeField] private CameraImpact cameraImpact; // ✅ ADD THIS

    private Camera cam;
    
    private float currentSpeed;

    private void Start()
    {
        cam = GetComponent<Camera>();
        
        if (cam == null)
        {
            Debug.LogError("[DynamicFOV] This script must be attached to a GameObject with a Camera component!", this);
            enabled = false; // Disable script to prevent errors
            return;
        }

        // Validate references
        if (playerRigidbody == null)
        {
            Debug.LogError("DynamicFOV: Player Rigidbody reference is missing! Assign in Inspector.");
            enabled = false;
            return;
        }
        
        if (playerMovement == null)
        {
            Debug.LogWarning("DynamicFOV: PlayerMovement reference is missing.");
        }
    }

    void FixedUpdate()
    {
        Vector3 hVel = new Vector3(playerRigidbody.linearVelocity.x, 0f, playerRigidbody.linearVelocity.z);
        currentSpeed = hVel.magnitude;
    }

    void LateUpdate()
    {
        if (cam == null || playerRigidbody == null) return;

        // Use ONLY the cached speed from FixedUpdate
        float targetFOV;
        if (currentSpeed < 7f)
            targetFOV = baseFOV;
        else if (currentSpeed <= 10f)  // Changed from 'speed' to 'currentSpeed'
            targetFOV = sprintFOV;
        else
            targetFOV = maxFOV;

        // Apply impact offset if CameraImpact exists
        float impactOffset = 0f;
        if (cameraImpact != null)
        {
            impactOffset = cameraImpact.CurrentFOVOffset;
        }

        // Lerp to target, then apply impact offset
        float smoothedFOV = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
        cam.fieldOfView = smoothedFOV + impactOffset;
    }
}