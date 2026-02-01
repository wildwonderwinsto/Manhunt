using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Manages vertical movement and gravity for the player character.
    /// 
    /// Responsibilities:
    /// - Apply gravitational acceleration when airborne
    /// - Manage jump impulse and variable jump height
    /// - Prevent velocity runaway with terminal velocity
    /// - Keep player grounded on slopes/stairs with small downward bias
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerGravity : MonoBehaviour
    {
        #region Configuration

        [Header("Jump Settings")]
        [SerializeField]
        [Tooltip("How high the player jumps (in units)")]
        private float _jumpHeight = 1.5f;

        [Header("Gravity Settings")]
        [SerializeField]
        [Tooltip("Downward acceleration when airborne (negative value)")]
        private float _gravity = -24.0f;

        [SerializeField]
        [Tooltip("Maximum downward velocity when falling")]
        private float _terminalVelocity = -53.0f;

        [SerializeField]
        [Tooltip("Small downward velocity when grounded to prevent walking off small ledges")]
        private float _groundStickVelocity = -2.0f;

        [Header("Head Bonk")]
        [SerializeField]
        [Tooltip("Velocity applied when jumping into a low ceiling")]
        private float _headBonkVelocity = -5.0f;

        [Header("Bunnyhopping")]
        [SerializeField]
        [Tooltip("Enable jump spam / bunnyhopping")]
        private bool _enableBunnyhopping = true;

        [SerializeField]
        [Tooltip("Speed multiplier for consecutive jumps")]
        private float _bunnyhopeachJumpBoost = 0.1f;

        [SerializeField]
        [Tooltip("Maximum speed multiplier from bunnyhopping")]
        private float _maxBunnyhopMultiplier = 2.0f;

        [SerializeField]
        [Tooltip("Time before bunnyhop boost resets")]
        private float _bunnyhopResetTime = 0.3f;

        [SerializeField]
        [Tooltip("Speed boost when jumping while sprinting")]
        private float _sprintJumpBoostMultiplier = 1.05f;

        #endregion

        #region State Properties

        /// <summary>Current vertical velocity (positive = upward, negative = downward).</summary>
        public float VerticalVelocity { get; private set; }

        /// <summary>Current speed boost multiplier from bunnyhopping.</summary>
        public float CurrentSpeedBoost { get; private set; } = 1.0f;

        #endregion

        #region Internal State

        private float _timeSinceLanding = 0f;
        private bool _wasGroundedLastFrame = false;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            // Start with ground stick velocity so we land properly
            VerticalVelocity = _groundStickVelocity;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Calculates vertical velocity for this frame.
        /// Call once per frame in the movement update.
        /// </summary>
        /// <param name="isGrounded">Is the player touching the ground?</param>
        /// <returns>Vertical velocity to apply this frame.</returns>
        public float CalculateGravity(bool isGrounded)
        {
            // Track landing for bunnyhop reset
            if (!_wasGroundedLastFrame && isGrounded)
            {
                _timeSinceLanding = 0f;
            }

            if (isGrounded)
            {
                // Player is grounded - clamp downward velocity
                if (VerticalVelocity < 0.0f)
                {
                    VerticalVelocity = _groundStickVelocity;
                }

                // Track time since landing for bunnyhop reset
                _timeSinceLanding += Time.deltaTime;

                // Reset bunnyhop boost if player has been grounded too long
                if (_timeSinceLanding > _bunnyhopResetTime)
                {
                    CurrentSpeedBoost = 1.0f;
                }
            }
            else
            {
                // Player is in the air - apply gravity
                if (VerticalVelocity > _terminalVelocity)
                {
                    VerticalVelocity += _gravity * Time.deltaTime;
                }
            }

            _wasGroundedLastFrame = isGrounded;
            return VerticalVelocity;
        }

        /// <summary>
        /// Apply jump impulse to the player.
        /// Uses physics formula: v = sqrt(h * -2 * g)
        /// Optionally applies bunnyhop boost and sprint boost.
        /// </summary>
        /// <param name="isSprinting">Is the player currently sprinting?</param>
        public void HandleJump(bool isSprinting = false)
        {
            // Base jump velocity
            float jumpVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);

            // Apply sprint jump boost if sprinting
            if (isSprinting)
            {
                jumpVelocity *= _sprintJumpBoostMultiplier;
            }

            // Apply bunnyhop boost if available
            if (_enableBunnyhopping && _timeSinceLanding < _bunnyhopResetTime)
            {
                CurrentSpeedBoost = Mathf.Min(CurrentSpeedBoost + _bunnyhopeachJumpBoost, _maxBunnyhopMultiplier);
            }
            else if (_enableBunnyhopping)
            {
                // First jump - reset boost
                CurrentSpeedBoost = 1.0f;
            }

            VerticalVelocity = jumpVelocity;
        }

        /// <summary>
        /// Handle head collision when jumping into ceilings.
        /// </summary>
        /// <param name="collisionFlags">Collision flags from CharacterController.Move()</param>
        public void HandleHeadCollision(CollisionFlags collisionFlags)
        {
            // Check if we hit something above (ceiling)
            if ((collisionFlags & CollisionFlags.Above) != 0)
            {
                // Only apply bonk if we were moving upward
                if (VerticalVelocity > 0)
                {
                    VerticalVelocity = _headBonkVelocity;
                }
            }
        }

        /// <summary>
        /// Directly set the vertical velocity (for external forces like bounce pads).
        /// </summary>
        public void SetVerticalVelocity(float velocity)
        {
            VerticalVelocity = velocity;
        }

        #endregion
    }
}
