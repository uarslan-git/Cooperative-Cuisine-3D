using UnityEngine;

/// <summary>
/// VR Camera Controller that positions the VR camera rig on top of the controlled player
/// </summary>
public class VRCameraController : MonoBehaviour 
{
    [Header("VR Settings")]
    public Transform playerTransform;
    public Vector3 cameraOffset = new Vector3(0, -0.5f, 1); // Height offset for VR camera (almost at ground level)
    public bool followPlayerRotation = true; // Whether camera should follow player rotation
    
    [Header("References")]
    public GameManager gameManager;
    public PlayerInputController playerInputController;
    
    private bool isVREnabled = false;
    
    void Start()
    {
        // Check if VR is available and enabled
        CheckVRStatus();
        
        // Find GameManager if not assigned
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
        
        // Find PlayerInputController if not assigned
        if (playerInputController == null)
        {
            playerInputController = FindFirstObjectByType<PlayerInputController>();
        }
    }
    
    void Update()
    {
        if (!isVREnabled) return;
        
        // Get the controlled player from GameManager or PlayerInputController
        if (playerTransform == null)
        {
            if (playerInputController != null && playerInputController.controlledPlayerGameObject != null)
            {
                SetPlayerTransform(playerInputController.controlledPlayerGameObject.transform);
                Debug.Log("VR Camera Controller found controlled player GameObject");
            }
        }
        
        // Position VR camera rig on the player
        if (playerTransform != null)
        {
            UpdateCameraPosition();
        }
    }
    
    private void CheckVRStatus()
    {
        // For now, assume VR is enabled if this component exists
        isVREnabled = true;
        Debug.Log("VR Camera Controller enabled");
        
        // Ensure UI canvases render to VR camera
        SetupUIForVR();
    }
    
    private void SetupUIForVR()
    {
        // Find all Canvas components in the scene
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        
        foreach (Canvas canvas in canvases)
        {
            // If canvas is set to Screen Space - Overlay, change it to Screen Space - Camera
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                
                // Set the VR camera as the render camera for this canvas
                Camera vrCamera = GetComponent<Camera>();
                if (vrCamera == null)
                {
                    vrCamera = GetComponentInChildren<Camera>();
                }
                
                if (vrCamera != null)
                {
                    canvas.worldCamera = vrCamera;
                    canvas.planeDistance = 2f; // Place UI 2 meters in front of camera
                    Debug.Log($"Set VR camera for canvas: {canvas.name}");
                }
            }
        }
    }
    
    private void UpdateCameraPosition()
    {
        // Position camera inside the player (with offset applied in player's local space)
        Vector3 localOffset = playerTransform.TransformDirection(cameraOffset);
        Vector3 targetPosition = playerTransform.position + localOffset;
        
        // Smoothly move the camera rig to the target position
        transform.position = Vector3.Lerp(transform.position, targetPosition, 10f * Time.deltaTime);
        
        // Always match player rotation so camera always looks where player is facing
        if (followPlayerRotation)
        {
            Quaternion targetRotation = playerTransform.rotation;
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 5f * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Call this when the controlled player changes (e.g., level transitions)
    /// </summary>
    public void SetPlayerTransform(Transform newPlayerTransform)
    {
        playerTransform = newPlayerTransform;
        if (playerTransform != null && isVREnabled)
        {
            // Immediately snap to new position (using player's local space)
            Vector3 localOffset = playerTransform.TransformDirection(cameraOffset);
            transform.position = playerTransform.position + localOffset;
            
            // Immediately snap to new rotation
            if (followPlayerRotation)
            {
                transform.rotation = playerTransform.rotation;
            }
        }
    }
    
    /// <summary>
    /// Toggle VR mode on/off
    /// </summary>
    public void ToggleVR(bool enable)
    {
        isVREnabled = enable;
        Debug.Log($"VR Camera Controller {(enable ? "Enabled" : "Disabled")}");
        
        if (enable)
        {
            SetupUIForVR();
        }
    }
    
    /// <summary>
    /// Call this after VR rig is created to setup UI rendering
    /// </summary>
    public void SetupVRUI(Camera vrCamera)
    {
        SetupAllCanvasesForVR(vrCamera);
        
        // Start a coroutine to periodically check for new canvases
        StartCoroutine(MonitorForNewCanvases(vrCamera));
    }
    
    private void SetupAllCanvasesForVR(Camera vrCamera)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        
        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = vrCamera;
                canvas.planeDistance = 2f;
                Debug.Log($"Configured canvas '{canvas.name}' for VR camera");
            }
        }
    }
    
    private System.Collections.IEnumerator MonitorForNewCanvases(Camera vrCamera)
    {
        while (isVREnabled)
        {
            yield return new UnityEngine.WaitForSeconds(0.5f); // Check every 0.5 seconds
            
            // Check for any new canvases that might have appeared
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            
            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.worldCamera != vrCamera)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = vrCamera;
                    canvas.planeDistance = 2f;
                    Debug.Log($"Dynamically configured new canvas '{canvas.name}' for VR camera");
                }
            }
        }
    }
}
