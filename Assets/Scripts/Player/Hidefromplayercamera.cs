using UnityEngine;

public class HideFromPlayerCamera : MonoBehaviour
{
    [Header("Layer Settings")]
    [Tooltip("Layer to use for hiding from player camera")]
    [SerializeField] private string hiddenLayer = "HiddenFromPlayer";

    [Header("References")]
    [Tooltip("Assign the PlayerCamera in the Inspector")]
    [SerializeField] private Camera playerCamera; // ✅ Direct reference

    [Header("Options")]
    [Tooltip("Should we set the layer on all children too?")]
    [SerializeField] private bool applyToChildren = true;

    private int layerIndex;

    private void Start()
    {
        // Validate camera reference
        if (playerCamera == null)
        {
            Debug.LogError("[HideFromPlayerCamera] PlayerCamera reference is missing! Assign in Inspector.");
            return;
        }

        SetupLayer();
        ConfigureCameraCulling();
        EnsureMeshRenderersEnabled();
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