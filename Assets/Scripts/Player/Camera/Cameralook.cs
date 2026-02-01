using UnityEngine;

namespace Game.Player.Camera
{
    /// <summary>
    /// Handles first-person camera look (pitch and yaw rotations).
    ///
    /// Pitch (X-axis): Camera looks up/down, clamped to [-89, 89] degrees to prevent looking backwards.
    /// Yaw (Y-axis): Player body rotates left/right.
    ///
    /// Optional input smoothing can reduce mouse jitter and provide more responsive feel.
    /// </summary>
    public class CameraLook : MonoBehaviour
    {
        #region Configuration

        [Header("Sensitivity")]
        [SerializeField]
        [Range(0.01f, 1f)]
        [Tooltip("Horizontal look sensitivity (yaw)")]
        private float _sensitivityX = 0.1f;

        [SerializeField]
        [Range(0.01f, 1f)]
        [Tooltip("Vertical look sensitivity (pitch)")]
        private float _sensitivityY = 0.1f;

        [SerializeField]
        [Tooltip("Invert the Y-axis? (some players prefer this)")]
        private bool _invertY = false;

        [Header("Smoothing")]
        [SerializeField]
        [Range(0f, 0.2f)]
        [Tooltip("Input smoothing duration. 0 = raw input, higher = smoother but more laggy")]
        private float _smoothTime = 0.02f;

        #endregion

        #region Internal State

        /// <summary>Current pitch angle (up/down rotation of camera).</summary>
        private float _currentPitch = 0f;

        /// <summary>Smoothed input for mouse look.</summary>
        private Vector2 _smoothedLookInput = Vector2.zero;

        /// <summary>Velocity reference for SmoothDamp algorithm.</summary>
        private Vector2 _lookVelocity = Vector2.zero;

        #endregion

        #region Public API

        /// <summary>
        /// Update camera look based on input.
        /// Should be called once per frame in LateUpdate or similar.
        /// </summary>
        /// <param name="playerBody">The player body transform (for yaw rotation)</param>
        /// <param name="rawLookInput">Raw mouse/controller look input from input system</param>
        /// <returns>Current pitch angle for camera rotation</returns>
        public float UpdateLook(Transform playerBody, Vector2 rawLookInput)
        {
            // 1. Smooth the input if enabled
            _smoothedLookInput = Vector2.SmoothDamp(
                _smoothedLookInput,
                rawLookInput,
                ref _lookVelocity,
                _smoothTime
            );

            // 2. Apply sensitivity
            float yawInput = _smoothedLookInput.x * _sensitivityX;
            float pitchInput = _smoothedLookInput.y * _sensitivityY;

            // 3. Rotate player body (yaw)
            playerBody.Rotate(Vector3.up * yawInput);

            // 4. Rotate camera (pitch)
            if (_invertY)
                pitchInput = -pitchInput;

            _currentPitch -= pitchInput;
            _currentPitch = Mathf.Clamp(_currentPitch, -89f, 89f);

            return _currentPitch;
        }

        /// <summary>Get the currently smoothed look input (useful for other systems like camera tilt).</summary>
        public Vector2 GetSmoothedLookInput() => _smoothedLookInput;

        /// <summary>Manually set pitch angle (useful for resetting or cutscenes).</summary>
        public void SetPitch(float pitch) => _currentPitch = Mathf.Clamp(pitch, -89f, 89f);

        #endregion
    }
}