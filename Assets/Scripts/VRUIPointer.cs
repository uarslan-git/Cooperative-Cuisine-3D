using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple VR pointer for interacting with UI elements
/// Attach this to your VR controller or hand
/// </summary>
public class VRUIPointer : MonoBehaviour
{
    [Header("Pointer Settings")]
    public Transform pointer; // Assign your hand transform here
    public LineRenderer lineRenderer;
    public float maxDistance = 10f;
    public LayerMask uiLayerMask = -1;
    
    [Header("Visual Settings")]
    public Color normalColor = Color.blue;
    public Color hoverColor = Color.green;
    public Color clickColor = Color.red;
    public float lineWidth = 0.005f;
    
    [Header("Hand Input")]
    public XRNode handNode = XRNode.RightHand;
    public bool showDebugLogs = true;
    
    private Camera vrCamera;
    private EventSystem eventSystem;
    private GameObject currentTarget;
    private Canvas targetCanvas;
    private GraphicRaycaster currentRaycaster;
    
    // Input tracking
    private bool triggerPressed = false;
    private bool triggerWasPressed = false;
    
    // Quest keyboard system
    private TouchScreenKeyboard overlayKeyboard;
    private TMP_InputField activeInputField;
    private string keyboardText = "";
    
    void Start()
    {
        SetupComponents();
    }
    
    void SetupComponents()
    {
        // Find VR camera
        vrCamera = Camera.main;
        if (vrCamera == null)
        {
            vrCamera = FindFirstObjectByType<Camera>();
        }
        
        // Setup line renderer if not assigned
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
        }
        
        // Set initial color
        lineRenderer.material.color = normalColor;
        
        // Find EventSystem
        eventSystem = FindFirstObjectByType<EventSystem>();
        
        Debug.Log("VR UI Pointer initialized");
    }
    
    void Update()
    {
        HandlePointing();
        HandleInput();
        HandleKeyboard();
    }
    
    void HandlePointing()
    {
        if (pointer == null) 
        {
            // Try to auto-find hand pointer
            if (handNode == XRNode.RightHand)
                pointer = transform; // Assume this script is on the hand
            
            if (pointer == null) return;
        }
        
        // Cast ray from pointer
        Ray ray = new Ray(pointer.position, pointer.forward);
        RaycastHit hit;
        
        bool hitUI = false;
        
        // Check for UI hits using Physics raycast for World Space canvases
        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            Canvas canvas = hit.collider.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                hitUI = true;
                currentTarget = hit.collider.gameObject;
                targetCanvas = canvas;
                currentRaycaster = canvas.GetComponent<GraphicRaycaster>();
                
                // Update line renderer
                lineRenderer.SetPosition(0, pointer.position);
                lineRenderer.SetPosition(1, hit.point);
                lineRenderer.material.color = hoverColor;
                
                if (showDebugLogs)
                    Debug.Log($"VR Pointer hitting UI: {currentTarget.name}");
            }
        }
        
        if (!hitUI)
        {
            // No UI hit - draw line to max distance
            lineRenderer.SetPosition(0, pointer.position);
            lineRenderer.SetPosition(1, pointer.position + pointer.forward * maxDistance);
            lineRenderer.material.color = normalColor;
            currentTarget = null;
            targetCanvas = null;
            currentRaycaster = null;
        }
    }
    
    void HandleInput()
    {
        // Store previous trigger state
        triggerWasPressed = triggerPressed;
        triggerPressed = false;
        
        // Check for trigger press (VR controller input)
        // Method 1: Mouse for testing
        if (Input.GetButton("Fire1"))
        {
            triggerPressed = true;
        }
        
        // Method 2: XR Input
        InputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerValue))
            {
                if (triggerValue) triggerPressed = true;
            }
            
            // Also try trigger as float value
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerFloat))
            {
                if (triggerFloat > 0.7f) triggerPressed = true;
            }
        }
        
        // Handle trigger press (down)
        if (triggerPressed && !triggerWasPressed && currentTarget != null)
        {
            HandleUIClick();
        }
        
        // Visual feedback
        if (triggerPressed && currentTarget != null)
        {
            lineRenderer.material.color = clickColor;
        }
    }
    
    void HandleUIClick()
    {
        if (currentTarget == null) return;
        
        if (showDebugLogs)
            Debug.Log($"VR UI Click on: {currentTarget.name}");
        
        // Handle Button clicks
        var button = currentTarget.GetComponent<Button>();
        if (button != null && button.interactable)
        {
            button.onClick.Invoke();
            Debug.Log($"VR: Clicked button {button.name}");
            return;
        }
        
        // Handle InputField clicks (for text editing)
        var inputField = currentTarget.GetComponent<TMP_InputField>();
        if (inputField == null)
        {
            // Check parent objects for InputField
            inputField = currentTarget.GetComponentInParent<TMP_InputField>();
        }
        
        if (inputField != null && inputField.interactable)
        {
            // Use Meta Quest's official keyboard system
            OpenQuestKeyboard(inputField);
            return;
        }
        
        // Handle other UI elements
        var selectable = currentTarget.GetComponent<Selectable>();
        if (selectable != null && selectable.interactable)
        {
            Debug.Log($"VR: Interacted with {selectable.GetType().Name} {selectable.name}");
        }
    }
    
    void OpenQuestKeyboard(TMP_InputField inputField)
    {
        activeInputField = inputField;
        
        // Get current text from input field
        string currentText = inputField.text ?? "";
        
        // Open Meta Quest system keyboard
        overlayKeyboard = TouchScreenKeyboard.Open(currentText, TouchScreenKeyboardType.Default);
        
        Debug.Log($"VR: Opened Quest keyboard for {inputField.name} with text: '{currentText}'");
        
        if (showDebugLogs)
            Debug.Log("Meta Quest keyboard should appear now");
    }
    
    void HandleKeyboard()
    {
        // Handle Quest keyboard input
        if (overlayKeyboard != null && activeInputField != null)
        {
            // Check if keyboard is still active
            if (overlayKeyboard.status == TouchScreenKeyboard.Status.Done)
            {
                // Keyboard finished - get the text
                keyboardText = overlayKeyboard.text;
                
                // Update the input field
                activeInputField.text = keyboardText;
                
                Debug.Log($"VR: Keyboard finished. New text: '{keyboardText}'");
                
                // Trigger any OnValueChanged events
                activeInputField.onValueChanged.Invoke(keyboardText);
                
                // Clean up
                overlayKeyboard = null;
                activeInputField = null;
            }
            else if (overlayKeyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                // User canceled keyboard
                Debug.Log("VR: Keyboard canceled by user");
                
                overlayKeyboard = null;
                activeInputField = null;
            }
            else if (overlayKeyboard.status == TouchScreenKeyboard.Status.Visible)
            {
                // Keyboard is active - optionally update text in real-time
                if (overlayKeyboard.text != keyboardText)
                {
                    keyboardText = overlayKeyboard.text;
                    if (showDebugLogs)
                        Debug.Log($"VR: Keyboard text updated: '{keyboardText}'");
                }
            }
        }
    }
}
