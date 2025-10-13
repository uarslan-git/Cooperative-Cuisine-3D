using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Scene Analysis Tool to identify flickering causes in Cooperative Cuisine scene
/// Attach this to an empty GameObject and run it to get a detailed scene report
/// </summary>
public class FlickerDebugger : MonoBehaviour
{
    [Header("Analysis Settings")]
    public bool analyzeOnStart = true;
    public bool continuousMonitoring = false;
    public float monitoringInterval = 2f;
    
    void Start()
    {
        if (analyzeOnStart)
        {
            AnalyzeScene();
        }
        
        if (continuousMonitoring)
        {
            InvokeRepeating(nameof(MonitorChanges), monitoringInterval, monitoringInterval);
        }
    }
    
    public void AnalyzeScene()
    {
        Debug.Log("=== FLICKER ANALYSIS REPORT ===");
        
        AnalyzeCameras();
        AnalyzeVRComponents();
        AnalyzeCanvases();
        AnalyzeLighting();
        AnalyzeActiveScripts();
        AnalyzeRenderingSettings();
        
        Debug.Log("=== END ANALYSIS ===");
    }
    
    void AnalyzeCameras()
    {
        Debug.Log("\n--- CAMERA ANALYSIS ---");
        Camera[] cameras = FindObjectsOfType<Camera>(true); // Include inactive
        
        Debug.Log($"Total Cameras Found: {cameras.Length}");
        
        List<Camera> activeCameras = new List<Camera>();
        List<Camera> vrCameras = new List<Camera>();
        List<Camera> legacyCameras = new List<Camera>();
        
        foreach (Camera cam in cameras)
        {
            Debug.Log($"Camera: {cam.name} | Active: {cam.gameObject.activeInHierarchy} | Enabled: {cam.enabled} | Tag: {cam.tag}");
            Debug.Log($"  └─ Depth: {cam.depth} | ClearFlags: {cam.clearFlags} | Parent: {(cam.transform.parent ? cam.transform.parent.name : "None")}");
            
            if (cam.gameObject.activeInHierarchy && cam.enabled)
            {
                activeCameras.Add(cam);
            }
            
            // Detect VR cameras (usually have XR-related parent names)
            string hierarchy = GetFullHierarchy(cam.transform);
            if (hierarchy.ToLower().Contains("xr") || hierarchy.ToLower().Contains("rig") || hierarchy.ToLower().Contains("center"))
            {
                vrCameras.Add(cam);
            }
            else if (cam.name.ToLower().Contains("main") || cam.tag == "MainCamera")
            {
                legacyCameras.Add(cam);
            }
        }
        
        Debug.Log($"Active Cameras: {activeCameras.Count}");
        Debug.Log($"VR Cameras: {vrCameras.Count}");
        Debug.Log($"Legacy Cameras: {legacyCameras.Count}");
        
        if (activeCameras.Count > 1)
        {
            Debug.LogError("⚠️ MULTIPLE ACTIVE CAMERAS DETECTED - LIKELY CAUSE OF FLICKERING!");
        }
        
        if (legacyCameras.Count > 0 && vrCameras.Count > 0)
        {
            Debug.LogError("⚠️ BOTH VR AND LEGACY CAMERAS PRESENT - CONFLICT DETECTED!");
        }
    }
    
    void AnalyzeVRComponents()
    {
        Debug.Log("\n--- VR COMPONENT ANALYSIS ---");
        
        // Check for VR-related components
        var vrSetups = FindObjectsOfType<VRSetup>();
        var vrCameraControllers = FindObjectsOfType<VRCameraController>();
        var vrManagers = FindObjectsOfType<VRManager>();
        
        Debug.Log($"VRSetup components: {vrSetups.Length}");
        Debug.Log($"VRCameraController components: {vrCameraControllers.Length}");
        Debug.Log($"VRManager components: {vrManagers.Length}");
        
        foreach (var setup in vrSetups)
        {
            Debug.Log($"VRSetup on {setup.name}: Enabled={setup.enabled}, Active={setup.gameObject.activeInHierarchy}");
        }
    }
    
    void AnalyzeCanvases()
    {
        Debug.Log("\n--- CANVAS ANALYSIS ---");
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        
        foreach (Canvas canvas in canvases)
        {
            Debug.Log($"Canvas: {canvas.name} | RenderMode: {canvas.renderMode} | Active: {canvas.gameObject.activeInHierarchy}");
            if (canvas.worldCamera != null)
            {
                Debug.Log($"  └─ WorldCamera: {canvas.worldCamera.name}");
            }
        }
    }
    
    void AnalyzeLighting()
    {
        Debug.Log("\n--- LIGHTING ANALYSIS ---");
        Light[] lights = FindObjectsOfType<Light>(true);
        Debug.Log($"Total Lights: {lights.Length}");
        
        foreach (Light light in lights)
        {
            Debug.Log($"Light: {light.name} | Type: {light.type} | Intensity: {light.intensity} | Active: {light.gameObject.activeInHierarchy}");
        }
    }
    
    void AnalyzeActiveScripts()
    {
        Debug.Log("\n--- ACTIVE SCRIPT ANALYSIS ---");
        
        // Check for potentially problematic scripts
        var studyClients = FindObjectsOfType<StudyClient>();
        var gameManagers = FindObjectsOfType<GameManager>();
        
        Debug.Log($"StudyClient instances: {studyClients.Length}");
        Debug.Log($"GameManager instances: {gameManagers.Length}");
        
        foreach (var client in studyClients)
        {
            Debug.Log($"StudyClient on {client.name}: Enabled={client.enabled}");
        }
    }
    
    void AnalyzeRenderingSettings()
    {
        Debug.Log("\n--- RENDERING SETTINGS ---");
        Debug.Log($"Quality Level: {QualitySettings.GetQualityLevel()}");
        Debug.Log($"VSync Count: {QualitySettings.vSyncCount}");
        Debug.Log($"Target Frame Rate: {Application.targetFrameRate}");
    }
    
    void MonitorChanges()
    {
        Debug.Log($"[{Time.time:F1}s] Monitoring - Active Cameras: {FindObjectsOfType<Camera>().Length}");
    }
    
    string GetFullHierarchy(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
    
    void Update()
    {
        // Manual trigger with F key
        if (Input.GetKeyDown(KeyCode.F))
        {
            AnalyzeScene();
        }
    }
}
