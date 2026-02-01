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
        private Vector2 _lastLookInput = Vector2.zero;
        private float _timeSinceCameraMove = 0f;

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
            bool isTurning = Mathf.Abs(lookInput.x) > 0.1f; // Significant horizontal mouse movement
            
            if (!isTurning && Time.deltaTime > 0)
            {
                _timeSinceCameraMove += Time.deltaTime;
            }
            else if (isTurning)
            {
                _timeSinceCameraMove = 0f; // Reset when turning
            }

            // Detect input direction change (WASD keys)
            if (moveInput != _lastMoveInput)
            {
                _timeSinceInputChange = 0f;
                _lastMoveInput = moveInput;
                _bobIntensity = 1f; // Full intensity on actual input change
                _timeSinceCameraMove = 0f; // Reset fade timer
            }
            else if (isMoving && !isTurning)
            {
                // Same input AND not turning - increase fade timer
                _timeSinceInputChange += Time.deltaTime;
                
                // Start fading bob after 2 seconds of straight movement (no turning)
                if (_timeSinceInputChange > 2f)
                {
                    // Fade out bob intensity over 5 seconds
                    float fadeProgress = Mathf.Clamp01((_timeSinceInputChange - 2f) / 5f);
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
                _lastLookInput = Vector2.zero;
                _timeSinceInputChange = 0f;
                _timeSinceCameraMove = 0f;
                return Vector3.Lerp(_currentBobOffset, Vector3.zero, Time.deltaTime * 5f);
            }

            // Update look history
            _lastLookInput = lookInput;

            // Calculate bob multipliers
            float speedMult = isSprinting ? 1.3f : 1f;
            float amountMult = isSprinting ? 1.5f : 1f;

            // Update bob timer
            _bobTimer += Time.deltaTime * 14f * speedMult;

            // Calculate bob offsets
            float vertBob = Mathf.Sin(_bobTimer) * 0.05f * amountMult * _bobIntensity;
            float horizBob = Mathf.Cos(_bobTimer * 0.5f) * 0.03f * amountMult * _bobIntensity;

            _currentBobOffset = new Vector3(horizBob, Mathf.Abs(vertBob), 0f);
            return _currentBobOffset;
        }
    }
}