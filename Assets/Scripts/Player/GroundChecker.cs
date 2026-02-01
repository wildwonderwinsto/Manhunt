using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Robust ground detection using Physics.SphereCast.
    /// 
    /// This is more reliable than raycasts because it:
    /// - Handles edges and uneven terrain smoothly
    /// - Detects the surface normal for slope calculations
    /// - Works on frames where the character might briefly leave the ground
    /// 
    /// The sphere is cast downward from above the player to find ground contact.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class GroundChecker : MonoBehaviour
    {
        #region Configuration
        [Header("Ground Detection Settings")]
        [SerializeField]
        [Tooltip("Which layers count as 'ground'?")]
        private LayerMask _groundLayers = 1 << 0; // Default: Layer 0 (ground)

        [SerializeField]
        [Range(0.1f, 0.5f)]
        [Tooltip("Radius of the sphere check at the player's feet")]
        private float _sphereRadius = 0.28f;

        [SerializeField]
        [Tooltip("Vertical offset from player position where the sphere is centered (negative = below player)")]
        private float _heightOffset = -0.14f;
        #endregion

        #region State Properties
        /// <summary>Is the player currently touching the ground?</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>Angle of the ground surface relative to level (0-90 degrees).</summary>
        public float SlopeAngle { get; private set; }

        /// <summary>Normal vector of the ground surface (perpendicular to surface).</summary>
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        #endregion

        #region Cached References
        private CharacterController _characterController;
        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (!TryGetComponent(out _characterController))
            {
                Debug.LogError($"GroundChecker on '{gameObject.name}' requires a CharacterController component!");
            }
        }

        private void Update()
        {
            CheckGround();
        }

        #endregion

        #region Ground Detection Logic

        /// <summary>
        /// Performs a sphere cast downward from the player's position to detect ground contact.
        /// Updates IsGrounded, SlopeAngle, and GroundNormal.
        /// </summary>
        private void CheckGround()
        {
            // Start the sphere cast slightly above the feet
            Vector3 castOrigin = transform.position + Vector3.up * 0.5f;

            // Distance the sphere must travel to reach the "feet + offset" height
            // The sphere's CENTER travels this distance
            float castDistance = 0.5f - _heightOffset;

            // Perform the downward sphere cast
            if (Physics.SphereCast(
                castOrigin,
                _sphereRadius,
                Vector3.down,
                out RaycastHit hit,
                castDistance,
                _groundLayers,
                QueryTriggerInteraction.Ignore))
            {
                IsGrounded = true;
                GroundNormal = hit.normal;
                SlopeAngle = Vector3.Angle(Vector3.up, GroundNormal);
            }
            else
            {
                IsGrounded = false;
                GroundNormal = Vector3.up;
                SlopeAngle = 0f;
            }
        }

        #endregion

        #region Debug Visualization

        private void OnDrawGizmosSelected()
        {
            // Visualize the ground detection sphere
            Vector3 sphereCenter = transform.position + Vector3.up * _heightOffset;
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawSphere(sphereCenter, _sphereRadius);

            // Visualize the surface normal when grounded
            if (IsGrounded)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(sphereCenter, GroundNormal * 0.5f);
            }
        }

        #endregion
    }
}
