using UnityEngine;

public class DynamicFOV : MonoBehaviour
{
    [Header("FOV Settings")]
    [SerializeField] private float baseFOV = 60f;
    [SerializeField] private float sprintFOV = 70f;
    [SerializeField] private float maxFOV = 80f;
    [SerializeField] private float fovLerpSpeed = 10f; // Increased for smoother high-fps

    private Camera cam;
    private Rigidbody playerRigidbody;
    private PlayerMovement playerMovement;
    private Vector3 horizontalVelocity; // Cached to avoid allocation

    private void Start()
    {
        cam = GetComponent<Camera>();
        
        // Find player by tag
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerRigidbody = player.GetComponent<Rigidbody>();
            playerMovement = player.GetComponent<PlayerMovement>();
        }
        else
        {
            Debug.LogWarning("DynamicFOV: Player GameObject not found! Ensure Player has 'Player' tag.");
        }
    }

    private void LateUpdate()
    {
        if (cam == null || playerRigidbody == null) return;

        // Calculate horizontal speed (ignore Y velocity) - NO ALLOCATION
        horizontalVelocity.Set(playerRigidbody.linearVelocity.x, 0f, playerRigidbody.linearVelocity.z);
        float speed = horizontalVelocity.magnitude;

        // Determine target FOV based on speed thresholds
        // Note: These match PlayerMovement walkSpeed=7, sprintSpeed=10, momentum adds up to 1.5
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
