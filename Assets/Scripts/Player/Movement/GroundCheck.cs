using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    [Header("Ground Detection")]
    [SerializeField] private float checkDistance = 0.3f;
    [SerializeField] private LayerMask groundLayer = ~0;

    private CapsuleCollider capsuleCollider;
    private float capsuleRadius;
    private Vector3 capsuleBottom;
    private bool isGroundedCached; // Cache for gizmos
    private Vector3 physicsPosition;

    private void Awake()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            capsuleRadius = capsuleCollider.radius * 0.9f; // Slightly smaller for better detection
        }
    }

    void FixedUpdate() // Add to GroundCheck
{
    physicsPosition = transform.position;
}

    public bool IsGrounded()
    {
        if (capsuleCollider == null) return false;

        // Calculate bottom center of capsule
        capsuleBottom = physicsPosition + Vector3.down * (capsuleCollider.height / 2f - capsuleCollider.radius);

        // Perform sphere cast downward and cache result
        isGroundedCached = Physics.SphereCast(capsuleBottom, capsuleRadius, Vector3.down, out _, checkDistance, groundLayer);
        return isGroundedCached;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying && capsuleCollider == null)
        {
            capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                capsuleRadius = capsuleCollider.radius * 0.9f;
            }
        }

        if (capsuleCollider == null) return;

        capsuleBottom = transform.position + Vector3.down * (capsuleCollider.height / 2f - capsuleCollider.radius);
        
        // Use cached grounded state instead of calling physics
        Gizmos.color = isGroundedCached ? Color.green : Color.red;
        Gizmos.DrawWireSphere(capsuleBottom + Vector3.down * checkDistance, capsuleRadius);
    }
}
