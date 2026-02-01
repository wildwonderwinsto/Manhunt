using UnityEngine;

/// <summary>
/// Hides this GameObject AND all children from the camera tagged "PlayerCamera" using layers
/// Also ensures MeshRenderer is enabled if found
/// 
/// SETUP:
/// 1. Create a new Layer called "HiddenFromPlayer"
/// 2. Attach this script to ANY object in your hierarchy (parent, child, or the mesh object itself)
/// 3. Set the hiddenLayer to "HiddenFromPlayer" in the inspector
/// 4. Make sure your camera has the tag "PlayerCamera"
/// 
/// IMPORTANT: This will set the layer on the GameObject AND all its children recursively
/// </summary>
public class HideFromPlayerCamera : MonoBehaviour
{
    [Header("Layer Settings")]
    [Tooltip("Layer to use for hiding from player camera")]
    [SerializeField] private string hiddenLayer = "HiddenFromPlayer";

    [Header("Options")]
    [Tooltip("Should we set the layer on all children too?")]
    [SerializeField] private bool applyToChildren = true;

    private Camera playerCamera;
    private int layerIndex;

    private void Awake()
    {
        FindPlayerCamera();
    }

    private void Start()
    {
        SetupLayer();
        ConfigureCameraCulling();
        EnsureMeshRenderersEnabled();
    }

    private void FindPlayerCamera()
    {
        GameObject cameraObj = GameObject.FindGameObjectWithTag("PlayerCamera");
        if (cameraObj != null)
        {
            playerCamera = cameraObj.GetComponent<Camera>();
            if (playerCamera == null)
            {
                Debug.LogError("[HideFromPlayerCamera] 'PlayerCamera' tag found but no Camera component!");
            }
        }
        else
        {
            Debug.LogError("[HideFromPlayerCamera] No GameObject with tag 'PlayerCamera' found!");
        }
    }

    private void SetupLayer()
    {
        layerIndex = LayerMask.NameToLayer(hiddenLayer);

        if (layerIndex == -1)
        {
            Debug.LogError($"[HideFromPlayerCamera] Layer '{hiddenLayer}' doesn't exist! Create it in Project Settings > Tags and Layers");
            return;
        }

        // Set layer on this GameObject
        gameObject.layer = layerIndex;

        // Recursively set layer on all children if enabled
        if (applyToChildren)
        {
            SetLayerRecursively(transform, layerIndex);
        }

        Debug.Log($"[HideFromPlayerCamera] Set layer '{hiddenLayer}' on {gameObject.name}" +
                  (applyToChildren ? " and all children" : ""));
    }

    private void SetLayerRecursively(Transform parent, int layer)
    {
        parent.gameObject.layer = layer;

        foreach (Transform child in parent)
        {
            SetLayerRecursively(child, layer);
        }
    }

    private void ConfigureCameraCulling()
    {
        if (playerCamera == null || layerIndex == -1) return;

        // Remove layer from PlayerCamera's culling mask
        playerCamera.cullingMask &= ~(1 << layerIndex);

        Debug.Log($"[HideFromPlayerCamera] Hidden from PlayerCamera");
    }

    private void EnsureMeshRenderersEnabled()
    {
        // Find all MeshRenderers in this object and children
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            if (!renderer.enabled)
            {
                renderer.enabled = true;
                Debug.Log($"[HideFromPlayerCamera] Enabled MeshRenderer on {renderer.gameObject.name}");
            }
        }

        if (renderers.Length > 0)
        {
            Debug.Log($"[HideFromPlayerCamera] Checked {renderers.Length} MeshRenderer(s)");
        }
    }

    // Show to PlayerCamera again
    public void ShowToPlayerCamera()
    {
        if (playerCamera != null && layerIndex != -1)
        {
            playerCamera.cullingMask |= (1 << layerIndex);
        }
    }

    // Hide from PlayerCamera again
    public void HideFromPlayerCameraAgain()
    {
        ConfigureCameraCulling();
    }
}