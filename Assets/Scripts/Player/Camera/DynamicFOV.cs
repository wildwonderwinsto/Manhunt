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
    private float currentSpeed;

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

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
    }
}