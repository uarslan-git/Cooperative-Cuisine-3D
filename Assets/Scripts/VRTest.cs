using UnityEngine;

/// <summary>
/// Simple VR Test script for debugging VR functionality
/// Attach this to an empty GameObject in your scene to test VR features
/// </summary>
public class VRTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool enableDebugOutput = true;
    public KeyCode toggleVRKey = KeyCode.V;
    
    private VRSetup vrSetup;
    private bool isVRActive = false;
    
    void Start()
    {
        // Find or create VR setup
        vrSetup = FindFirstObjectByType<VRSetup>();
        if (vrSetup == null)
        {
            Debug.Log("No VRSetup found in scene. Creating one...");
            GameObject setupObject = new GameObject("VR Setup");
            vrSetup = setupObject.AddComponent<VRSetup>();
        }
        
        if (enableDebugOutput)
        {
            Debug.Log("VR Test initialized. Press 'V' to toggle VR mode.");
        }
    }
    
    void Update()
    {
        // Toggle VR with keyboard input (for testing)
        if (Input.GetKeyDown(toggleVRKey))
        {
            ToggleVR();
        }
        
        // Debug VR status
        if (enableDebugOutput && Input.GetKeyDown(KeyCode.I))
        {
            PrintVRInfo();
        }
    }
    
    public void ToggleVR()
    {
        isVRActive = !isVRActive;
        
        if (isVRActive)
        {
            Debug.Log("Activating VR Mode...");
            if (vrSetup != null)
            {
                vrSetup.SetupVR();
            }
        }
        else
        {
            Debug.Log("Deactivating VR Mode...");
            if (vrSetup != null)
            {
                vrSetup.DisableVR();
            }
        }
    }
    
    private void PrintVRInfo()
    {
        Debug.Log("=== VR Status Info ===");
        Debug.Log($"VR Active: {isVRActive}");
        Debug.Log($"VR Setup Present: {vrSetup != null}");
        
        if (vrSetup != null)
        {
            Debug.Log($"VR Rig: {vrSetup.GetVRRig()}");
            Debug.Log($"VR Camera: {vrSetup.GetVRCamera()}");
        }
        
        // Find VR components
        var vrCamera = FindFirstObjectByType<VRCameraController>();
        var vrInput = FindFirstObjectByType<VRInputController>();
        
        Debug.Log($"VR Camera Controller: {vrCamera != null}");
        Debug.Log($"VR Input Controller: {vrInput != null}");
        
        // Find game components
        var gameManager = FindFirstObjectByType<GameManager>();
        var playerInput = FindFirstObjectByType<PlayerInputController>();
        
        Debug.Log($"Game Manager: {gameManager != null}");
        Debug.Log($"Player Input Controller: {playerInput != null}");
        
        Debug.Log("=== End VR Status ===");
    }
    
    void OnGUI()
    {
        if (!enableDebugOutput) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("VR Test Controls:");
        GUILayout.Label($"Press '{toggleVRKey}' to toggle VR");
        GUILayout.Label("Press 'I' for VR info");
        GUILayout.Label($"VR Status: {(isVRActive ? "Active" : "Inactive")}");
        
        if (GUILayout.Button("Toggle VR"))
        {
            ToggleVR();
        }
        
        if (GUILayout.Button("Print VR Info"))
        {
            PrintVRInfo();
        }
        
        GUILayout.EndArea();
    }
}
