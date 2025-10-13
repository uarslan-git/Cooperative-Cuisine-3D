using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// Simple VR test script to debug VR initialization and detect issues
/// Attach this to an empty GameObject in your test scene
/// </summary>
public class VRDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool logVRStatus = true;
    public bool logEverySecond = true;
    
    void Start()
    {
        if (logVRStatus)
        {
            LogVRStatus();
        }
        
        if (logEverySecond)
        {
            InvokeRepeating(nameof(LogVRStatusPeriodic), 1f, 1f);
        }
    }
    
    void LogVRStatus()
    {
        Debug.Log("=== VR Debug Status ===");
        Debug.Log($"XR Enabled: {XRSettings.enabled}");
        Debug.Log($"XR Device: {XRSettings.loadedDeviceName}");
        Debug.Log($"XR Active: {XRSettings.isDeviceActive}");
        
        // List supported devices
        string[] supportedDevices = XRSettings.supportedDevices;
        Debug.Log($"Supported XR Devices: {string.Join(", ", supportedDevices)}");
        
        // Check for cameras
        Camera[] cameras = FindObjectsOfType<Camera>();
        Debug.Log($"Total Cameras in Scene: {cameras.Length}");
        
        foreach (Camera cam in cameras)
        {
            Debug.Log($"Camera: {cam.name} - Tag: {cam.tag} - Enabled: {cam.enabled}");
        }
        
        // Check for Input Devices
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);
        Debug.Log($"Input Devices Found: {devices.Count}");
        
        foreach (var device in devices)
        {
            Debug.Log($"Device: {device.name} - Role: {device.role} - Connected: {device.isValid}");
        }
    }
    
    void LogVRStatusPeriodic()
    {
        if (XRSettings.enabled)
        {
            Debug.Log($"[{Time.time:F1}s] VR Active - Device: {XRSettings.loadedDeviceName}");
        }
        else
        {
            Debug.Log($"[{Time.time:F1}s] VR Not Active");
        }
    }
    
    void Update()
    {
        // Check for any VR state changes
        if (Input.GetKeyDown(KeyCode.Space))
        {
            LogVRStatus();
        }
    }
}
