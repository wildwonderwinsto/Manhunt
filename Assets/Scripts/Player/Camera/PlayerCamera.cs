using UnityEngine;
using Game.Player.Camera;
using Game.Core.Input;

namespace Game.Player.Camera
{
    /// <summary>
    /// Main camera controller that orchestrates look, tilt, and FOV effects.
    /// Directly uses PlayerInputReader for input.
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The actual Camera component (usually a child).")]
        public Transform cameraTransform;

        [SerializeField]
        [Tooltip("The player body root (for yaw rotation). If null, uses parent.")]
        private Transform _playerBody;

        [SerializeField]
        [Tooltip("Input Reader asset")]
        private PlayerInputReader _inputReader;

        [Header("View Bob Settings")]
        [SerializeField, Range(0.05f, 0.5f)]
        [Tooltip("Camera movement threshold to trigger 'turning' state")]
        private float _turnSensitivityThreshold = 0.1f;

        [SerializeField, Range(0.5f, 5f)]
        [Tooltip("Seconds of straight movement before view bob starts fading")]
        private float _bobFadeDelaySeconds = 2f;

        [SerializeField, Range(1f, 10f)]
        [Tooltip("Duration in seconds for view bob to fade from full to minimal")]
        private float _bobFadeDurationSeconds = 5f;

        [SerializeField, Range(0f, 0.2f)]
        [Tooltip("Maximum vertical head bob distance")]
        private float _walkBobVerticalAmount = 0.05f;

        [SerializeField, Range(0f, 0.1f)]
        [Tooltip("Maximum horizontal head bob sway")]
        private float _walkBobHorizontalAmount = 0.03f;

        [SerializeField, Range(5f, 20f)]
        [Tooltip("Speed of head bob cycle")]
        private float _bobFrequency = 14f;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Horizontal bob frequency relative to vertical")]
        private float _bobHorizontalFrequencyRatio = 0.5f;

        private CameraLook _cameraLook;
        private CameraTilt _cameraTilt;
        private CameraFOV _cameraFOV;
        private CharacterController _characterController;
        private GroundChecker _groundChecker;
        private Vector3 _baseCameraPosition = Vector3.zero;
        private float _bobTimer = 0f;
        private Vector3 _currentBobOffset = Vector3.zero;
        private Vector2 _lastMoveInput = Vector2.zero;
        private float _timeSinceInputChange = 0f;
        private float _bobIntensity = 1f;

        private void Start()
        {
            if (cameraTransform == null)
            {
                UnityEngine.Camera main = UnityEngine.Camera.main;
                if (main) cameraTransform = main.transform;
            }

            // If player body not assigned, try to find the player root
            if (_playerBody == null)
            {
                // Assume the camera is a child of the player body
                _playerBody = transform.parent ?? transform;
            }

            // Find InputReader if not assigned
            if (_inputReader == null)
            {
                _inputReader = FindFirstObjectByType<PlayerInputReader>();
            }

            // Initialize components
            _cameraLook = GetComponent<CameraLook>();
            _cameraTilt = GetComponent<CameraTilt>();
            _cameraFOV = GetComponent<CameraFOV>();
            _characterController = GetComponentInParent<CharacterController>();
            _groundChecker = GetComponentInParent<GroundChecker>();

            // Store base camera position
            _baseCameraPosition = cameraTransform.localPosition;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            if (!cameraTransform || _inputReader == null || _playerBody == null) return;

            // 1. Handle Look (Pitch and Yaw)
            // IMPORTANT: Pass _playerBody (root), not transform (camera holder)
            float pitch = _cameraLook != null ? _cameraLook.UpdateLook(_playerBody, _inputReader.LookInput) : 0f;

            // 2. Handle Tilt (Strafe and Turn)
            float tilt = _cameraTilt != null ? _cameraTilt.UpdateTilt(_inputReader.LookInput, _inputReader.MoveInput) : 0f;

            // 3. Handle FOV (Speed-based)
            if (_cameraFOV != null) _cameraFOV.UpdateFOV();

            // 4. Handle View Bob (Head motion during movement)
            Vector3 bobOffset = UpdateViewBob();

            // 5. Apply camera transform (pitch, tilt, and bob)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, tilt);
            
            // Apply bob position offset to BASE position (don't accumulate)
            cameraTransform.localPosition = _baseCameraPosition + bobOffset;
        }

        /// <summary>Calculate head bob effect during movement.</summary>
        private Vector3 UpdateViewBob()
        {
            if (_characterController == null)
                return Vector3.zero;

            Vector3 velocity = _characterController.velocity;
            velocity.y = 0;
            float speed = velocity.magnitude;

            Vector2 moveInput = _inputReader != null ? _inputReader.MoveInput : Vector2.zero;
            Vector2 lookInput = _inputReader != null ? _inputReader.LookInput : Vector2.zero;
            bool isSprinting = _inputReader != null && _inputReader.IsSprintHeld;
            bool isMoving = speed > 0.5f && moveInput.magnitude > 0.1f;

            // Detect if player is turning camera (look input magnitude)
            bool isTurning = Mathf.Abs(lookInput.x) > _turnSensitivityThreshold;

            // Detect input direction change (WASD keys)
            if (moveInput != _lastMoveInput)
            {
                _timeSinceInputChange = 0f;
                _lastMoveInput = moveInput;
                _bobIntensity = 1f; // Full intensity on actual input change
            }
            else if (isMoving && !isTurning)
            {
                // Same input AND not turning - increase fade timer
                _timeSinceInputChange += Time.deltaTime;
                
                // Start fading bob after configured delay
                if (_timeSinceInputChange > _bobFadeDelaySeconds)
                {
                    // Fade out bob intensity over configured duration
                    float fadeProgress = Mathf.Clamp01((_timeSinceInputChange - _bobFadeDelaySeconds) / _bobFadeDurationSeconds);
                    _bobIntensity = Mathf.Lerp(1f, 0.1f, fadeProgress);
                }
            }
            else if (isTurning)
            {
                // Player is turning - keep bob at full intensity, don't fade
                _bobIntensity = 1f;
            }

            if (!isMoving || !_groundChecker.IsGrounded)
            {
                _bobTimer = 0f;
                _bobIntensity = 1f; // Reset intensity when stopping
                _lastMoveInput = Vector2.zero;
                _timeSinceInputChange = 0f;
                return Vector3.Lerp(_currentBobOffset, Vector3.zero, Time.deltaTime * 5f);
            }

            // Calculate bob multipliers
            float speedMult = isSprinting ? 1.3f : 1f;
            float amountMult = isSprinting ? 1.5f : 1f;

            // Update bob timer
            _bobTimer += Time.deltaTime * _bobFrequency * speedMult;

            // Calculate bob offsets
            float vertBob = Mathf.Sin(_bobTimer) * _walkBobVerticalAmount * amountMult * _bobIntensity;
            float horizBob = Mathf.Cos(_bobTimer * _bobHorizontalFrequencyRatio) * _walkBobHorizontalAmount * amountMult * _bobIntensity;

            _currentBobOffset = new Vector3(horizBob, Mathf.Abs(vertBob), 0f);
            return _currentBobOffset;
        }
    }
}