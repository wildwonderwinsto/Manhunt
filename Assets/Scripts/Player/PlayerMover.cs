using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Handles horizontal movement physics using a Source Engine/Quake-inspired model.
    ///
    /// Key features:
    /// - Friction-based deceleration (applies deceleration proportional to current speed)
    /// - Acceleration-based speed gain (player accelerates toward target speed)
    /// - Air control (allows steering while airborne, but with reduced acceleration)
    /// - Supports optional bunnyhopping through external speed boosters
    ///
    /// The model is frame-rate independent through the use of Time.deltaTime.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMover : MonoBehaviour
    {
        #region Configuration - Ground Movement

        [Header("Ground Movement")]
        [SerializeField]
        [Tooltip("Base walking speed")]
        [Range(1f, 20f)]
        private float _walkSpeed = 8.0f;

        [SerializeField]
        [Tooltip("Running/sprint speed")]
        [Range(1f, 30f)]
        private float _runSpeed = 14.0f;

        [SerializeField]
        [Tooltip("How quickly the player accelerates on the ground")]
        [Range(1f, 30f)]
        private float _groundAcceleration = 14.0f;

        [SerializeField]
        [Tooltip("Deceleration multiplier when no input is given")]
        [Range(1f, 20f)]
        private float _friction = 6.0f;

        [SerializeField]
        [Tooltip("Below this speed, apply full friction")]
        [Range(0.1f, 5f)]
        private float _stopSpeed = 2.0f;

        #endregion

        #region Configuration - Air Movement

        [Header("Air Movement")]
        [SerializeField]
        [Tooltip("Maximum horizontal speed when airborne")]
        [Range(1f, 20f)]
        private float _maxAirSpeed = 10.0f;

        [SerializeField]
        [Tooltip("How quickly player accelerates in the air (should be much lower than ground)")]
        [Range(0.1f, 5f)]
        private float _airAcceleration = 2.0f;

        #endregion

        #region Internal State

        /// <summary>Current horizontal velocity (XZ plane only). Y is handled by gravity system.</summary>
        private Vector3 _horizontalVelocity = Vector3.zero;

        #endregion

        #region Public API

        /// <summary>
        /// Calculates the new horizontal velocity for this frame.
        /// Accounts for input, ground state, and physics parameters.
        /// Call once per frame in the movement system.
        /// </summary>
        /// <param name="currentVelocity">The player's current horizontal velocity (Y component ignored)</param>
        /// <param name="input">Raw movement input (X=strafe, Y=forward)</param>
        /// <param name="cameraTransform">Camera reference for orientation</param>
        /// <param name="isGrounded">Is the player touching the ground?</param>
        /// <param name="isSprinting">Is the player sprinting?</param>
        /// <param name="speedBoostMultiplier">Optional speed multiplier from bunnyhopping (default 1.0)</param>
        /// <returns>New horizontal velocity for this frame</returns>
        public Vector3 CalculateVelocity(
            Vector3 currentVelocity,
            Vector2 input,
            Transform cameraTransform,
            bool isGrounded,
            bool isSprinting,
            float speedBoostMultiplier = 1.0f)
        {
            // Remove vertical component (gravity is handled elsewhere)
            _horizontalVelocity = currentVelocity;
            _horizontalVelocity.y = 0;

            if (isGrounded)
            {
                // Apply friction when grounded to slow down naturally
                _horizontalVelocity = ApplyFriction(_horizontalVelocity);

                // Determine target speed based on sprint state and bunnyhopping
                float baseSpeed = isSprinting ? _runSpeed : _walkSpeed;
                float targetSpeed = baseSpeed * speedBoostMultiplier;

                // Accelerate toward target in the desired direction
                _horizontalVelocity = ApplyAcceleration(
                    _horizontalVelocity,
                    input,
                    cameraTransform,
                    targetSpeed,
                    _groundAcceleration);
            }
            else
            {
                // In air: reduced acceleration, no friction
                _horizontalVelocity = ApplyAcceleration(
                    _horizontalVelocity,
                    input,
                    cameraTransform,
                    _maxAirSpeed,
                    _airAcceleration);
            }

            return _horizontalVelocity;
        }

        #endregion

        #region Physics Calculations

        /// <summary>
        /// Apply friction to reduce speed when on the ground.
        /// Uses the Quake friction model.
        /// </summary>
        private Vector3 ApplyFriction(Vector3 velocity)
        {
            float speed = velocity.magnitude;
            if (speed < 0.001f) return Vector3.zero; // Already stopped

            // Calculate how much speed to lose this frame
            float control = (speed < _stopSpeed) ? _stopSpeed : speed;
            float drop = control * _friction * Time.deltaTime;

            float newSpeed = speed - drop;
            if (newSpeed < 0) newSpeed = 0;

            // Normalize and scale by the new speed
            if (speed > 0) newSpeed /= speed;
            return velocity * newSpeed;
        }

        /// <summary>
        /// Accelerate the player toward a target speed in a desired direction.
        /// This is the Quake acceleration model.
        /// </summary>
        private Vector3 ApplyAcceleration(
            Vector3 velocity,
            Vector2 input,
            Transform cameraTransform,
            float targetSpeed,
            float accelerationRate)
        {
            // 1. Determine the direction we want to move
            Vector3 wishDirection = CalculateWishDirection(input, cameraTransform);

            // No input = no acceleration
            if (wishDirection.sqrMagnitude < 0.001f)
                return velocity;

            // 2. How fast do we want to go in that direction?
            float wishSpeed = wishDirection.magnitude * targetSpeed;

            // Normalize the direction
            wishDirection.Normalize();

            // 3. How fast are we already going in that direction?
            float currentSpeed = Vector3.Dot(velocity, wishDirection);

            // 4. How much faster can we go?
            float addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0)
                return velocity; // Already going fast enough in this direction

            // 5. Accelerate, but cap it to not exceed the desired speed gain
            float accelAmount = accelerationRate * Time.deltaTime * wishSpeed;
            if (accelAmount > addSpeed)
                accelAmount = addSpeed;

            return velocity + (wishDirection * accelAmount);
        }

        /// <summary>
        /// Convert 2D input into a 3D direction relative to camera orientation.
        /// </summary>
        private Vector3 CalculateWishDirection(Vector2 input, Transform cameraTransform)
        {
            if (cameraTransform == null)
                return Vector3.zero;

            // Get camera axes
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            // Flatten to horizontal plane (remove vertical component)
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            // Combine input with camera orientation
            Vector3 wishDirection = (forward * input.y) + (right * input.x);

            // Normalize if over-extended (prevents speed boost from diagonal input)
            if (wishDirection.sqrMagnitude > 1.0f)
                wishDirection.Normalize();

            return wishDirection;
        }

        #endregion
    }
}
