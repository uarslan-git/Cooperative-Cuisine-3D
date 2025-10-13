using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// VR Scene Manager that coordinates VR initialization and scene setup
/// This should be attached to a GameObject in your main scene
/// </summary>
public class VRSceneManager : MonoBehaviour
{
    [Header("VR Configuration")]
    public GameObject vrRigPrefab;
    public GameObject mainMenuCanvasPrefab;
    
    [Header("Scene References")]
    public Transform vrRigSpawnPoint;
    public Transform menuSpawnPoint;
    
    [Header("VR Settings")]
    public bool enableVROnStart = true;
    public float menuDistance = 2.0f;
    public float menuHeight = 0.0f;
    
    private GameObject vrRigInstance;
    private GameObject menuInstance;
    private Camera vrCamera;
    
    void Awake()
    {
        // Initialize VR
        if (enableVROnStart)
        {
            InitializeVR();
        }
    }
    
    void Start()
    {
        StartCoroutine(SetupVRScene());
    }
    
    System.Collections.IEnumerator SetupVRScene()
    {
        // Wait for VR to initialize
        yield return new WaitForSeconds(0.5f);
        
        SetupVRRig();
        SetupMainMenu();
        
        Debug.Log("VR Scene setup complete");
    }
    
    void InitializeVR()
    {
        // Check if VR is supported and available
        if (XRSettings.supportedDevices.Length == 0)
        {
            Debug.LogWarning("No VR devices supported");
            return;
        }
        
        // Enable VR
        XRSettings.enabled = true;
        
        Debug.Log("VR initialized");
    }
    
    void SetupVRRig()
    {
        // Destroy any existing main camera (non-VR fallback)
        Camera[] cameras = FindObjectsOfType<Camera>();
        foreach (Camera cam in cameras)
        {
            if (cam.CompareTag("MainCamera") && !cam.name.Contains("VR") && !cam.name.Contains("Center"))
            {
                Debug.Log($"Destroying non-VR camera: {cam.name}");
                Destroy(cam.gameObject);
            }
        }
        
        // Create VR Rig if prefab is provided
        if (vrRigPrefab != null)
        {
            Vector3 spawnPos = vrRigSpawnPoint != null ? vrRigSpawnPoint.position : Vector3.zero;
            vrRigInstance = Instantiate(vrRigPrefab, spawnPos, Quaternion.identity);
            vrRigInstance.name = "VR Rig";
            
            // Find the VR camera
            vrCamera = vrRigInstance.GetComponentInChildren<Camera>();
            if (vrCamera != null)
            {
                vrCamera.tag = "MainCamera";
            }
        }
        else
        {
            Debug.LogWarning("VR Rig prefab not assigned. Please create a VR rig manually.");
        }
    }
    
    void SetupMainMenu()
    {
        // Create main menu canvas
        if (mainMenuCanvasPrefab != null)
        {
            Vector3 menuPos;
            
            if (menuSpawnPoint != null)
            {
                menuPos = menuSpawnPoint.position;
            }
            else if (vrCamera != null)
            {
                // Position menu in front of VR camera
                menuPos = vrCamera.transform.position + vrCamera.transform.forward * menuDistance;
                menuPos.y += menuHeight;
            }
            else
            {
                // Fallback position
                menuPos = new Vector3(0, 1.5f, 2);
            }
            
            menuInstance = Instantiate(mainMenuCanvasPrefab, menuPos, Quaternion.identity);
            menuInstance.name = "VR Main Menu";
            
            // Make menu face the VR camera
            if (vrCamera != null)
            {
                Vector3 lookDirection = menuInstance.transform.position - vrCamera.transform.position;
                lookDirection.y = 0; // Keep menu upright
                menuInstance.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
        else
        {
            Debug.LogWarning("Main Menu Canvas prefab not assigned");
        }
    }
    
    public void HideMainMenu()
    {
        if (menuInstance != null)
        {
            menuInstance.SetActive(false);
        }
    }
    
    public void ShowMainMenu()
    {
        if (menuInstance != null)
        {
            menuInstance.SetActive(true);
        }
    }
    
    public Camera GetVRCamera()
    {
        return vrCamera;
    }
    
    public GameObject GetVRRig()
    {
        return vrRigInstance;
    }
}
