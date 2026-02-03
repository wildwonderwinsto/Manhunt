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

    private Camera cam;
    private Vector3 horizontalVelocity; // Cached to avoid allocation

    private void Start()
    {
        cam = GetComponent<Camera>();
        
        // Validate references
        if (playerRigidbody == null)
        {
            Debug.LogError("DynamicFOV: Player Rigidbody reference is missing! Assign in Inspector.");
        }
        
        if (playerMovement == null)
        {
            Debug.LogWarning("DynamicFOV: PlayerMovement reference is missing.");
        }
    }

    private void LateUpdate()
    {
        if (cam == null || playerRigidbody == null) return;

        // Calculate horizontal speed (ignore Y velocity) - NO ALLOCATION
        horizontalVelocity.Set(playerRigidbody.linearVelocity.x, 0f, playerRigidbody.linearVelocity.z);
        float speed = horizontalVelocity.magnitude;

        // Determine target FOV based on speed thresholds
        float targetFOV;
        if (speed < 7f)
            targetFOV = baseFOV;
        else if (speed <= 10f)
            targetFOV = sprintFOV;
        else
            targetFOV = maxFOV;

        // Smoothly lerp to target FOV (frame-rate independent)
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
    }
}