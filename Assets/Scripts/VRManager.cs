using UnityEngine;

/// <summary>
/// VR Manager that coordinates all VR functionality for the Cooperative Cuisine 3D game
/// This script manages the overall VR setup and coordinates between VR components
/// </summary>
public class VRManager : MonoBehaviour
{
    [Header("VR Configuration")]
    public bool startInVRMode = true;
    public bool allowVRToggle = true;
    
    [Header("VR Components")]
    public VRCameraController vrCameraController;
    public VRInputController vrInputController;
    
    [Header("VR Rig Setup")]
    public GameObject vrRigPrefab; // Assign this in inspector if you have a custom VR rig
    public Transform cameraRig;
    public Camera vrCamera;
    
    [Header("Fallback Camera")]
    public Camera desktopCamera;
    
    private bool isVRActive = false;
    private GameManager gameManager;
    private PlayerInputController playerInputController;
    
    void Awake()
    {
        // Find required components
        gameManager = FindFirstObjectByType<GameManager>();
        playerInputController = FindFirstObjectByType<PlayerInputController>();
        
        // Initialize VR
        InitializeVR();
    }
    
    void Start()
    {
        if (startInVRMode)
        {
            EnableVR();
        }
        else
        {
            EnableDesktop();
        }
    }
    
    private void InitializeVR()
    {
        // Create VR rig if it doesn't exist
        if (cameraRig == null)
        {
            CreateVRRig();
        }
        
        // Set up VR camera controller
        if (vrCameraController == null)
        {
            vrCameraController = cameraRig.GetComponent<VRCameraController>();
            if (vrCameraController == null)
            {
                vrCameraController = cameraRig.gameObject.AddComponent<VRCameraController>();
            }
        }
        
        // Set up VR input controller
        if (vrInputController == null)
        {
            vrInputController = GetComponent<VRInputController>();
            if (vrInputController == null)
            {
                vrInputController = gameObject.AddComponent<VRInputController>();
            }
        }
        
        // Configure components
        if (vrCameraController != null)
        {
            vrCameraController.gameManager = gameManager;
            vrCameraController.playerInputController = playerInputController;
        }
        
        if (vrInputController != null)
        {
            vrInputController.playerInputController = playerInputController;
        }
    }
    
    private void CreateVRRig()
    {
        GameObject rigObject;
        
        if (vrRigPrefab != null)
        {
            // Use custom VR rig prefab
            rigObject = Instantiate(vrRigPrefab);
        }
        else
        {
            // Create basic VR rig
            rigObject = new GameObject("VR Camera Rig");
            
            // Create camera
            GameObject cameraObject = new GameObject("VR Camera");
            cameraObject.transform.SetParent(rigObject.transform);
            
            vrCamera = cameraObject.AddComponent<Camera>();
            vrCamera.nearClipPlane = 0.1f;
            vrCamera.farClipPlane = 1000f;
            
            // Add audio listener
            cameraObject.AddComponent<AudioListener>();
            
            // Create hand controller objects
            GameObject leftHand = new GameObject("Left Hand Controller");
            GameObject rightHand = new GameObject("Right Hand Controller");
            
            leftHand.transform.SetParent(rigObject.transform);
            rightHand.transform.SetParent(rigObject.transform);
            
            // Set up VR input controller references
            if (vrInputController != null)
            {
                vrInputController.leftHandController = leftHand.transform;
                vrInputController.rightHandController = rightHand.transform;
            }
        }
        
        cameraRig = rigObject.transform;
        
        // Position the rig
        cameraRig.position = new Vector3(0, 0, 0);
        
        DontDestroyOnLoad(rigObject);
    }
    
    public void EnableVR()
    {
        if (isVRActive) return;
        
        Debug.Log("Enabling VR Mode...");
        
        // Enable VR camera rig
        if (cameraRig != null)
        {
            cameraRig.gameObject.SetActive(true);
        }
        
        // Disable desktop camera
        if (desktopCamera != null)
        {
            desktopCamera.gameObject.SetActive(false);
        }
        
        // Enable VR components
        if (vrCameraController != null)
        {
            vrCameraController.enabled = true;
            vrCameraController.ToggleVR(true);
        }
        
        if (vrInputController != null)
        {
            vrInputController.enabled = true;
            vrInputController.enableVRInput = true;
        }
        
        // Update camera reference in PlayerInputController
        if (playerInputController != null && vrCamera != null)
        {
            playerInputController.mainCamera = vrCamera;
        }
        
        isVRActive = true;
        Debug.Log("VR Mode Enabled");
    }
    
    public void EnableDesktop()
    {
        if (!isVRActive) return;
        
        Debug.Log("Enabling Desktop Mode...");
        
        // Disable VR camera rig
        if (cameraRig != null)
        {
            cameraRig.gameObject.SetActive(false);
        }
        
        // Enable desktop camera
        if (desktopCamera != null)
        {
            desktopCamera.gameObject.SetActive(true);
        }
        
        // Disable VR components
        if (vrCameraController != null)
        {
            vrCameraController.ToggleVR(false);
            vrCameraController.enabled = false;
        }
        
        if (vrInputController != null)
        {
            vrInputController.enableVRInput = false;
            vrInputController.enabled = false;
        }
        
        // Update camera reference in PlayerInputController
        if (playerInputController != null && desktopCamera != null)
        {
            playerInputController.mainCamera = desktopCamera;
        }
        
        isVRActive = false;
        Debug.Log("Desktop Mode Enabled");
    }
    
    public void ToggleVRMode()
    {
        if (!allowVRToggle) return;
        
        if (isVRActive)
        {
            EnableDesktop();
        }
        else
        {
            EnableVR();
        }
    }
    
    public bool IsVRActive()
    {
        return isVRActive;
    }
    
    // Called when the controlled player changes (e.g., level transitions)
    public void OnPlayerChanged(Transform newPlayerTransform)
    {
        if (vrCameraController != null)
        {
            vrCameraController.SetPlayerTransform(newPlayerTransform);
        }
    }
    
    void Update()
    {
        // Check for VR toggle input (for testing)
        if (allowVRToggle && Input.GetKeyDown(KeyCode.V))
        {
            ToggleVRMode();
        }
    }
}
