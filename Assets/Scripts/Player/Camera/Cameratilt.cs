using UnityEngine;

namespace Game.Player.Camera
{
    /// <summary>
    /// Handles camera roll/tilt effects for visual feedback.
    ///
    /// Creates "juice" (polish) by tilting the screen when:
    /// - Strafing (left/right movement input)
    /// - Turning quickly (large yaw rotations)
    ///
    /// This mimics head movement and creates a more responsive feel.
    /// </summary>
    public class CameraTilt : MonoBehaviour
    {
        #region Configuration

        [Header("Strafe Tilt")]
        [SerializeField]
        [Range(0f, 5f)]
        [Tooltip("How much the screen tilts when moving left/right")]
        private float _strafeTiltAmount = 2.0f;

        [Header("Turn Tilt")]
        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("How much the screen tilts when turning with mouse")]
        private float _turnTiltAmount = 0.5f;

        [Header("Recovery")]
        [SerializeField]
        [Range(1f, 30f)]
        [Tooltip("How quickly the tilt returns to center")]
        private float _tiltRecoverySpeed = 8.0f;

        #endregion

        #region Internal State

        /// <summary>Current tilt angle (Z-axis rotation).</summary>
        private float _currentTilt = 0f;

        #endregion

        #region Public API

        /// <summary>
        /// Calculate the camera tilt for this frame based on input and turning.
        /// </summary>
        /// <param name="lookInput">Current look input (for turn tilt)</param>
        /// <param name="moveInput">Current movement input (for strafe tilt)</param>
        /// <returns>Current tilt angle to apply to camera</returns>
        public float UpdateTilt(Vector2 lookInput, Vector2 moveInput)
        {
            float targetTilt = 0f;

            // Strafe tilt: tilt opposite to movement direction
            targetTilt += -moveInput.x * _strafeTiltAmount;

            // Turn tilt: tilt opposite to mouse turning direction
            targetTilt += -lookInput.x * _turnTiltAmount;

            // Smoothly recover to center
            _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, Time.deltaTime * _tiltRecoverySpeed);

            return _currentTilt;
        }

        #endregion
    }
}