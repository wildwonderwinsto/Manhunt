using UnityEngine;

public class PlayerCrouch : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Tooltip("Camera Y position when standing")]
    private float standingCameraHeight = 0.6f;
    
    [SerializeField, Tooltip("Camera Y position when crouching")]
    private float crouchingCameraHeight = 0f;
    
    [SerializeField, Tooltip("Crouch transition speed")]
    private float crouchSpeed = 10f;

    private PlayerInputReader inputReader;
    private Transform cameraHolder;
    private bool isCrouching;
    private float targetCameraHeight;
    private float currentCameraHeight;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        cameraHolder = transform.Find("CameraHolder");
        
        currentCameraHeight = standingCameraHeight;
        targetCameraHeight = standingCameraHeight;
    }

    private void LateUpdate()
    {
        if (inputReader == null || cameraHolder == null) return;

        // Toggle crouch when button pressed
        if (inputReader.CrouchPressed)
        {
            inputReader.ConsumeCrouch();
            isCrouching = !isCrouching;
            targetCameraHeight = isCrouching ? crouchingCameraHeight : standingCameraHeight;
        }

        // Smoothly lerp camera height
        // Better (frame-rate independent):
        float lerpFactor = 1f - Mathf.Exp(-crouchSpeed * Time.deltaTime);
        currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, lerpFactor);
        cameraHolder.localPosition = new Vector3(0f, currentCameraHeight, 0f);
    }

    public bool IsCrouching()
    {
        return isCrouching;
    }
}

