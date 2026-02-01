using UnityEngine;
using Game.Core.Input;

namespace Game.Player
{
    /// <summary>
    /// The Central Brain of the Player.
    /// Manages references and high-level state.
    /// Routes Input signals to appropriate sub-components (Movement, Camera, etc).
    /// 
    /// Responsibilities:
    /// - Combine horizontal and vertical movement
    /// - Handle slope sliding with friction
    /// - Route input events to handlers
    /// - Apply CharacterController movement
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Drag the PlayerInputReader asset here")]
        [SerializeField] private PlayerInputReader _inputReader;
        [SerializeField] private Transform _cameraTransform;

        [Header("Slope Settings")]
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Friction applied to movement on slopes (0 = no friction, 1 = full friction)")]
        private float _slopeFriction = 0.5f;

        // Sub-components
        private CharacterController _characterController;
        private PlayerMover _mover;
        private PlayerGravity _gravity;
        private GroundChecker _groundChecker;

        // State
        private bool _isSprinting;
        private Vector3 _currentMomentum;
        private bool _wasOnSteepSlopeLastFrame = false;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _mover = GetComponent<PlayerMover>();
            _gravity = GetComponent<PlayerGravity>();
            _groundChecker = GetComponent<GroundChecker>();

            if (_cameraTransform == null)
            {
                UnityEngine.Camera mainCam = UnityEngine.Camera.main;
                if (mainCam != null)
                {
                    _cameraTransform = mainCam.transform;
                }
            }
        }

        private void OnEnable()
        {
            if (_inputReader == null) return;
            
            _inputReader.JumpPressed += HandleJump;
            _inputReader.SprintPressed += HandleSprintStart;
            _inputReader.SprintReleased += HandleSprintEnd;
        }

        private void OnDisable()
        {
            if (_inputReader == null) return;

            _inputReader.JumpPressed -= HandleJump;
            _inputReader.SprintPressed -= HandleSprintStart;
            _inputReader.SprintReleased -= HandleSprintEnd;
        }

        private void Update()
        {
            Move();
        }

        private void Move()
        {
            if (_inputReader == null || _characterController == null) return;

            // 1. Gather Data
            bool isGrounded = _groundChecker.IsGrounded;
            Vector2 input = _inputReader.MoveInput;
            bool isOnSteepSlope = isGrounded && _groundChecker.SlopeAngle > _characterController.slopeLimit;

            // Debugging: Check if we have input and camera
            if (input != Vector2.zero && _cameraTransform == null)
            {
                Debug.LogError("PlayerController: Trying to move but Camera Transform is NULL! Drag your camera into the slot.");
            }

            // 2. Calculate Vertical Velocity (Gravity)
            float verticalSpeed = _gravity.CalculateGravity(isGrounded);
            
            // 3. Calculate Horizontal Velocity
            if (isOnSteepSlope && verticalSpeed <= 0)
            {
                // --- STEEP SLOPE: SLIDING ---
                // Player is on a slope steeper than controller's slope limit
                // AND not jumping (verticalSpeed <= 0)
                // Calculate slide direction and apply friction
                
                Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, _groundChecker.GroundNormal).normalized;
                
                // Calculate slide speed based on slope angle
                float slideSpeed = 15f * (_groundChecker.SlopeAngle / 90f); 
                
                // Apply friction to slide (so player doesn't accelerate forever)
                float friction = Mathf.Lerp(1f, 0.3f, _slopeFriction);
                _currentMomentum = Vector3.Lerp(_currentMomentum, slideDir * slideSpeed, friction * Time.deltaTime);
                
                // Debug visualization
                Debug.DrawRay(transform.position, _currentMomentum, Color.blue);
                
                _wasOnSteepSlopeLastFrame = true;
            }
            else
            {
                // --- NORMAL MOVEMENT: WALKING, RUNNING, IN AIR ---
                
                // If we just left a steep slope, RESET momentum instead of carrying it over
                if (_wasOnSteepSlopeLastFrame)
                {
                    // Completely reset horizontal momentum when leaving slope
                    _currentMomentum = Vector3.zero;
                }
                
                // Use normal movement physics with optional speed boost from bunnyhopping
                _currentMomentum = _mover.CalculateVelocity(
                    _currentMomentum, 
                    input, 
                    _cameraTransform, 
                    isGrounded, 
                    _isSprinting,
                    _gravity.CurrentSpeedBoost  // Pass bunnyhop boost multiplier
                );
                
                _wasOnSteepSlopeLastFrame = false;
            }
            
            // 4. Combine Horizontal + Vertical
            Vector3 finalVelocity = _currentMomentum;
            finalVelocity.y = verticalSpeed;

            // 5. Apply to Character Controller and get collision info
            CollisionFlags collisions = _characterController.Move(finalVelocity * Time.deltaTime);

            // 6. Handle collisions (head bonking)
            _gravity.HandleHeadCollision(collisions);
        }

        /// <summary>
        /// Handle jump input. Passes sprint state for sprint jump boost.
        /// </summary>
        private void HandleJump()
        {
            if (_groundChecker.IsGrounded)
            {
                _gravity.HandleJump(_isSprinting);
            }
        }

        private void HandleSprintStart() => _isSprinting = true;

        private void HandleSprintEnd() => _isSprinting = false;
    }
}
