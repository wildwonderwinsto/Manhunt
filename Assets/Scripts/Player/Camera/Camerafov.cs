using UnityEngine;

namespace Game.Player.Camera
{
    /// <summary>
    /// Handles dynamic field of view (FOV) based on movement speed.
    ///
    /// As the player moves faster, the FOV increases slightly, creating a sense of acceleration.
    /// This is a subtle but effective visual feedback mechanism.
    /// </summary>
    public class CameraFOV : MonoBehaviour
    {
        #region Configuration

        [Header("FOV Settings")]
        [SerializeField]
        [Range(40f, 120f)]
        [Tooltip("Base FOV when standing still")]
        private float _baseFOV = 90f;

        [SerializeField]
        [Range(50f, 130f)]
        [Tooltip("Maximum FOV when moving at high speed")]
        private float _maxFOV = 110f;

        [SerializeField]
        [Range(1f, 30f)]
        [Tooltip("Speed required to reach maximum FOV")]
        private float _speedForMaxFOV = 15f;

        [Header("Smoothing")]
        [SerializeField]
        [Range(1f, 20f)]
        [Tooltip("How quickly FOV changes (higher = faster)")]
        private float _fovChangeSpeed = 5f;

        #endregion

        #region Cached References

        private UnityEngine.Camera _cameraComponent;
        private CharacterController _characterController;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            _cameraComponent = GetComponentInParent<UnityEngine.Camera>();
            _characterController = GetComponentInParent<CharacterController>();

            if (_cameraComponent == null)
                Debug.LogError($"CameraFOV on '{gameObject.name}' requires a Camera component in parent!");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Update the camera FOV based on current movement speed.
        /// Should be called once per frame.
        /// </summary>
        public void UpdateFOV()
        {
            if (_cameraComponent == null || _characterController == null)
                return;

            // Get horizontal velocity
            Vector3 velocity = _characterController.velocity;
            velocity.y = 0;
            float speed = velocity.magnitude;

            // Linear interpolation: speed -> FOV
            float targetFOV = Mathf.Lerp(_baseFOV, _maxFOV, speed / _speedForMaxFOV);

            // Smooth the transition
            _cameraComponent.fieldOfView = Mathf.Lerp(
                _cameraComponent.fieldOfView,
                targetFOV,
                Time.deltaTime * _fovChangeSpeed);
        }

        #endregion
    }
}