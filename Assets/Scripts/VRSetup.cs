using UnityEngine;

/// <summary>
/// VR Setup script that creates and configures the VR rig for Meta Quest 3
/// This should be attached to an empty GameObject in your scene
/// </summary>
public class VRSetup : MonoBehaviour
{
    [Header("VR Configuration")]
    public bool enableVROnStart = true;
    public bool createVRRigOnStart = true;
    
    [Header("Camera Settings")]
    public float eyeHeight = 1.7f; // Typical eye height for standing VR
    public float nearClipPlane = 0.1f;
    public float farClipPlane = 1000f;
    
    [Header("Hand Tracking")]
    public bool enableHandTracking = true;
    public GameObject leftHandPrefab;
    public GameObject rightHandPrefab;
    
    private GameObject vrRig;
    private Camera vrCamera;
    private VRCameraController cameraController;
    private VRInputController inputController;
    
    void Start()
    {
        if (enableVROnStart)
        {
            SetupVR();
        }
    }
    
    [ContextMenu("Setup VR")]
    public void SetupVR()
    {
        if (createVRRigOnStart)
        {
            CreateVRRig();
        }
        
        EnableVR();
    }
    
    private void CreateVRRig()
    {
        // Create main VR rig
        vrRig = new GameObject("VR Camera Rig");
        vrRig.transform.position = Vector3.zero;
        
        // Create camera object
        GameObject cameraObject = new GameObject("VR Camera");
        cameraObject.transform.SetParent(vrRig.transform);
        cameraObject.transform.localPosition = new Vector3(0, eyeHeight, 0);
        
        // Setup camera component
        vrCamera = cameraObject.AddComponent<Camera>();
        vrCamera.nearClipPlane = nearClipPlane;
        vrCamera.farClipPlane = farClipPlane;
        vrCamera.tag = "MainCamera";
        
        // Add audio listener
        cameraObject.AddComponent<AudioListener>();
        
        // Create hand controllers if prefabs are provided
        if (enableHandTracking)
        {
            CreateHandControllers();
        }
        
        // Add VR Camera Controller
        cameraController = vrRig.AddComponent<VRCameraController>();
        
        // Add VR Input Controller to this object
        inputController = gameObject.AddComponent<VRInputController>();
        
        // Configure components
        ConfigureVRComponents();
        
        Debug.Log("VR Rig created successfully!");
    }
    
    private void CreateHandControllers()
    {
        // Create left hand
        GameObject leftHand = leftHandPrefab != null ? 
            Instantiate(leftHandPrefab, vrRig.transform) : 
            new GameObject("Left Hand Controller");
        
        leftHand.name = "Left Hand Controller";
        leftHand.transform.SetParent(vrRig.transform);
        
        // Create right hand
        GameObject rightHand = rightHandPrefab != null ? 
            Instantiate(rightHandPrefab, vrRig.transform) : 
            new GameObject("Right Hand Controller");
        
        rightHand.name = "Right Hand Controller";
        rightHand.transform.SetParent(vrRig.transform);
        
        // Set references in input controller
        if (inputController != null)
        {
            inputController.leftHandController = leftHand.transform;
            inputController.rightHandController = rightHand.transform;
        }
    }
    
    private void ConfigureVRComponents()
    {
        // Find game components
        var gameManager = FindFirstObjectByType<GameManager>();
        var playerInputController = FindFirstObjectByType<PlayerInputController>();
        
        // Configure VR Camera Controller
        if (cameraController != null)
        {
            cameraController.gameManager = gameManager;
            cameraController.playerInputController = playerInputController;
            
            // Try to set initial player transform if available
            if (playerInputController != null && playerInputController.controlledPlayerGameObject != null)
            {
                cameraController.SetPlayerTransform(playerInputController.controlledPlayerGameObject.transform);
                Debug.Log("VR Camera Controller initialized with current controlled player");
            }
        }
        
        // Configure VR Input Controller
        if (inputController != null)
        {
            inputController.playerInputController = playerInputController;
        }
        
        // Update PlayerInputController camera reference
        if (playerInputController != null && vrCamera != null)
        {
            playerInputController.mainCamera = vrCamera;
        }
    }
    
    private void EnableVR()
    {
        // VR setup completed
        Debug.Log("VR Mode Enabled");
    }
    
    public void DisableVR()
    {
        // VR disabled
        Debug.Log("VR Mode Disabled");
    }
    
    public GameObject GetVRRig()
    {
        return vrRig;
    }
    
    public Camera GetVRCamera()
    {
        return vrCamera;
    }
}
